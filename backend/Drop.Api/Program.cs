using System.Text;
using Drop.Api.Data;
using Drop.Api.Endpoints;
using Drop.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configure maximum video upload size (2GB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 2L * 1024 * 1024 * 1024;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Configure Database
var configuredDataDir = builder.Configuration["DROP_DATA_DIR"];
string baseDataDir = !string.IsNullOrWhiteSpace(configuredDataDir)
    ? (Path.IsPathRooted(configuredDataDir) ? configuredDataDir : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, configuredDataDir)))
    : Path.Combine(AppContext.BaseDirectory, "data");

baseDataDir = Path.GetFullPath(baseDataDir);
Directory.CreateDirectory(baseDataDir);

var dbPath = builder.Configuration["DROP_DB_PATH"]
             ?? Path.Combine(baseDataDir, "db", "drop.db");
dbPath = Path.GetFullPath(dbPath);

var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

// Seamless migration: If db was previously located directly in baseDataDir/drop.db, move it to db/
var legacyDbPath = Path.Combine(baseDataDir, "drop.db");
if (File.Exists(legacyDbPath) && !File.Exists(dbPath))
{
    File.Move(legacyDbPath, dbPath);
    if (File.Exists(legacyDbPath + "-shm")) File.Move(legacyDbPath + "-shm", dbPath + "-shm", true);
    if (File.Exists(legacyDbPath + "-wal")) File.Move(legacyDbPath + "-wal", dbPath + "-wal", true);
}

var connectionString = $"Data Source={dbPath}";
builder.Services.AddDbContext<DropDbContext>(options =>
    options.UseSqlite(connectionString));

// Register Application Services
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IMediaStorageService, MediaStorageService>();
builder.Services.AddHostedService<IngestWatcherService>();

// Configure Authentication
var secret = builder.Configuration["DROP_JWT_SECRET"]
             ?? builder.Configuration["JWT_SECRET"]
             ?? "drop_default_super_secret_jwt_key_that_is_long_enough_2026_drop!";
var key = Encoding.UTF8.GetBytes(secret.PadRight(32, '!'));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = "drop",
            ValidateAudience = true,
            ValidAudience = "drop-users",
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        // Support token extracted from either Bearer header or drop_auth cookie
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token) &&
                    context.Request.Cookies.TryGetValue("drop_auth", out var cookieToken))
                {
                    context.Token = cookieToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Configure CORS for dev and production
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Ensure Database is initialized
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DropDbContext>();
    await db.Database.EnsureCreatedAsync();

    try
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Clips ADD COLUMN Visibility TEXT NOT NULL DEFAULT 'public';");
    }
    catch { /* Column already exists */ }

    try
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Collections ADD COLUMN Visibility TEXT NOT NULL DEFAULT 'public';");
    }
    catch { /* Column already exists */ }
}

app.UseCors();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map API Endpoints
app.MapAuthEndpoints();
app.MapClipEndpoints();
app.MapCollectionEndpoints();

// System Status endpoint
app.MapGet("/api/system/status", async (
    DropDbContext db,
    IMediaStorageService mediaStorage,
    IAuthService authService) =>
{
    var clipCount = await db.Clips.CountAsync();
    var collectionCount = await db.Collections.CountAsync();

    return Results.Ok(new
    {
        name = "drop",
        version = "1.0.0",
        adminUsername = authService.AdminUsername,
        ingestDirectory = mediaStorage.IngestDirectory,
        clipsCount = clipCount,
        collectionsCount = collectionCount,
        serverTimeUtc = DateTime.UtcNow
    });
}).WithTags("System");

// OpenGraph HTML for share bots (Discord, Twitter/X, Facebook, Telegram)
app.MapGet("/c/{id}", async (string id, HttpContext context, DropDbContext db, IAuthService authService) =>
{
    var userAgent = context.Request.Headers.UserAgent.ToString().ToLowerInvariant();
    var isBot = userAgent.Contains("discordbot") ||
                userAgent.Contains("twitterbot") ||
                userAgent.Contains("facebookexternalhit") ||
                userAgent.Contains("slackbot") ||
                userAgent.Contains("telegrambot");

    var clip = await db.Clips.FirstOrDefaultAsync(c => c.Id == id);
    if (clip == null) return Results.NotFound();

    // Private clips cannot be previewed by bots or unauthenticated users
    if (clip.Visibility == "private" && !authService.IsAdmin(context))
    {
        return Results.NotFound();
    }

    if (isBot)
    {
        var scheme = context.Request.Scheme;
        var host = context.Request.Host.Value;
        var streamUrl = $"{scheme}://{host}/api/clips/{clip.Id}/stream";
        var thumbUrl = $"{scheme}://{host}/api/clips/{clip.Id}/thumbnail";
        var pageUrl = $"{scheme}://{host}/clip/{clip.Id}";

        var html = $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <title>{{clip.Title}} - drop</title>
            <meta name="description" content="{{clip.Description ?? "Watch this gaming clip on drop."}}">
            <meta property="og:site_name" content="drop">
            <meta property="og:type" content="video.other">
            <meta property="og:title" content="{{clip.Title}}">
            <meta property="og:description" content="{{clip.Description ?? "Watch short video game clips on drop"}}">
            <meta property="og:url" content="{{pageUrl}}">
            <meta property="og:image" content="{{thumbUrl}}">
            <meta property="og:video" content="{{streamUrl}}">
            <meta property="og:video:type" content="{{clip.ContentType}}">
            <meta property="og:video:width" content="1920">
            <meta property="og:video:height" content="1080">
            <meta name="twitter:card" content="player">
            <meta name="twitter:title" content="{{clip.Title}}">
            <meta name="twitter:description" content="{{clip.Description ?? "Watch this video game clip on drop."}}">
            <meta name="twitter:image" content="{{thumbUrl}}">
            <meta name="twitter:player" content="{{pageUrl}}">
            <meta name="twitter:player:width" content="1920">
            <meta name="twitter:player:height" content="1080">
        </head>
        <body>
            <p>Redirecting to clip...</p>
            <script>window.location.href = '/clip/{{clip.Id}}';</script>
        </body>
        </html>
        """;

        return Results.Content(html, "text/html");
    }

    // Default: redirect to the SPA clip route
    return Results.Redirect($"/clip/{id}");
});

// Serve frontend static files if present (wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

// SPA Fallback for client-side routing
app.MapFallbackToFile("index.html");

app.Run();
