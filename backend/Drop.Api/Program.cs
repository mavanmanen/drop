using System.Text;
using Drop.Api.Data;
using Drop.Api.Endpoints;
using Drop.Api.Models;
using Drop.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers from reverse proxies (Nginx, Traefik, Caddy, Cloudflare)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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

// Configure Database (SQLite or PostgreSQL)
DatabaseConfigurator.ConfigureDatabase(builder.Services, builder.Configuration, builder.Environment);

// Register Application Services
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IMediaStorageService, MediaStorageService>();
builder.Services.AddSingleton<IGeminiAnalysisService, GeminiAnalysisService>();
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

// Respect reverse proxy headers (HTTPS scheme, host, client IP)
app.UseForwardedHeaders();

// Ensure Database is initialized
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DropDbContext>();
    await DatabaseConfigurator.InitializeDatabaseAsync(db);
}

app.UseCors();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map API Endpoints
app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapClipEndpoints();
app.MapCollectionEndpoints();

// System Status endpoint
app.MapGet("/api/system/status", async (
    DropDbContext db,
    IMediaStorageService mediaStorage,
    IAuthService authService,
    IConfiguration config) =>
{
    var clipCount = await db.Clips.CountAsync();
    var collectionCount = await db.Collections.CountAsync();

    var rawVisibility = config["DROP_INGEST_DEFAULT_VISIBILITY"]?.Trim().ToLowerInvariant();
    var defaultIngestVisibility = rawVisibility switch
    {
        "unlisted" => "unlisted",
        "private" => "private",
        _ => "public"
    };

    return Results.Ok(new
    {
        name = "drop",
        version = "1.0.0",
        adminUsername = authService.AdminUsername,
        ingestDirectory = mediaStorage.IngestDirectory,
        defaultIngestVisibility,
        databaseProvider = DatabaseConfigurator.GetDatabaseProvider(config),
        clipsCount = clipCount,
        collectionsCount = collectionCount,
        serverTimeUtc = DateTime.UtcNow
    });
}).WithTags("System");

// Helper for OpenGraph embed rendering
static bool IsBotUserAgent(string userAgent)
{
    var ua = userAgent.ToLowerInvariant();
    return ua.Contains("discordbot") ||
           ua.Contains("twitterbot") ||
           ua.Contains("facebookexternalhit") ||
           ua.Contains("slackbot") ||
           ua.Contains("telegrambot") ||
           ua.Contains("whatsapp") ||
           ua.Contains("meta-externalagent");
}

static IResult GenerateOpenGraphHtml(Clip clip, HttpContext context)
{
    // Determine canonical scheme and host (respecting X-Forwarded-Proto / X-Forwarded-Host)
    var scheme = context.Request.Scheme;
    var host = context.Request.Host.Value;
    var streamUrl = $"{scheme}://{host}/api/clips/{clip.Id}/stream";
    var thumbUrl = $"{scheme}://{host}/api/clips/{clip.Id}/thumbnail";
    var pageUrl = $"{scheme}://{host}/clip/{clip.Id}";

    var title = System.Net.WebUtility.HtmlEncode(clip.Title);
    var description = System.Net.WebUtility.HtmlEncode(clip.Description ?? "Watch this video clip on drop");

    var html = $$"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="utf-8">
        <title>{{title}} - drop</title>
        <meta name="description" content="{{description}}">
        <meta property="og:site_name" content="drop">
        <meta property="og:type" content="video.other">
        <meta property="og:title" content="{{title}}">
        <meta property="og:description" content="{{description}}">
        <meta property="og:url" content="{{pageUrl}}">
        <meta property="og:image" content="{{thumbUrl}}">
        <meta property="og:video" content="{{streamUrl}}">
        <meta property="og:video:url" content="{{streamUrl}}">
        <meta property="og:video:secure_url" content="{{streamUrl}}">
        <meta property="og:video:type" content="{{clip.ContentType}}">
        <meta property="og:video:width" content="1920">
        <meta property="og:video:height" content="1080">
        <meta name="twitter:card" content="player">
        <meta name="twitter:title" content="{{title}}">
        <meta name="twitter:description" content="{{description}}">
        <meta name="twitter:image" content="{{thumbUrl}}">
        <meta name="twitter:player" content="{{pageUrl}}">
        <meta name="twitter:player:width" content="1920">
        <meta name="twitter:player:height" content="1080">
        <meta name="twitter:player:stream" content="{{streamUrl}}">
        <meta name="twitter:player:stream:content_type" content="{{clip.ContentType}}">
    </head>
    <body>
        <p>Redirecting to clip...</p>
        <script>window.location.href = '/clip/{{clip.Id}}';</script>
    </body>
    </html>
    """;

    return Results.Content(html, "text/html");
}

// Bot preview endpoint for /c/{id}
app.MapGet("/c/{id}", async (string id, HttpContext context, DropDbContext db, IAuthService authService) =>
{
    var userAgent = context.Request.Headers.UserAgent.ToString();
    var clip = await db.Clips.FirstOrDefaultAsync(c => c.Id == id);
    if (clip == null) return Results.NotFound();

    if (clip.Visibility == "private" && !authService.IsAdmin(context))
    {
        return Results.NotFound();
    }

    if (IsBotUserAgent(userAgent))
    {
        return GenerateOpenGraphHtml(clip, context);
    }

    // Default for real users: redirect to full SPA route
    return Results.Redirect($"/clip/{id}");
});

// Bot preview endpoint for /clip/{id} (standard SPA route, but renders OpenGraph if accessed by scrapers)
app.MapGet("/clip/{id}", async (string id, HttpContext context, DropDbContext db, IAuthService authService) =>
{
    var userAgent = context.Request.Headers.UserAgent.ToString();
    if (IsBotUserAgent(userAgent))
    {
        var clip = await db.Clips.FirstOrDefaultAsync(c => c.Id == id);
        if (clip == null) return Results.NotFound();

        if (clip.Visibility == "private" && !authService.IsAdmin(context))
        {
            return Results.NotFound();
        }

        return GenerateOpenGraphHtml(clip, context);
    }

    // Normal browser users fall through to SPA index.html
    return Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html");
});

// Serve frontend static files if present (wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

// SPA Fallback for client-side routing
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
