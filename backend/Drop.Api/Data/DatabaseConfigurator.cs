using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Drop.Api.Data;

public static class DatabaseConfigurator
{
    public static string GetDatabaseProvider(IConfiguration configuration)
    {
        var provider = configuration["DROP_DB_PROVIDER"]?.Trim().ToLowerInvariant();
        if (provider is "postgres" or "postgresql")
        {
            return "postgres";
        }

        if (provider is "sqlite")
        {
            return "sqlite";
        }

        // Auto-detect based on connection string or host setting
        var pgRaw = configuration["DROP_POSTGRES_CONNECTION_STRING"]
                    ?? configuration["DATABASE_URL"]
                    ?? configuration["DROP_POSTGRES_HOST"];

        if (!string.IsNullOrWhiteSpace(pgRaw))
        {
            return "postgres";
        }

        var connStr = configuration["DROP_DB_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(connStr) &&
            (connStr.StartsWith("Host=", StringComparison.OrdinalIgnoreCase) ||
             connStr.StartsWith("Server=", StringComparison.OrdinalIgnoreCase) ||
             connStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
             connStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
        {
            return "postgres";
        }

        return "sqlite";
    }

    public static string BuildPostgresConnectionString(IConfiguration configuration)
    {
        var raw = configuration["DROP_DB_CONNECTION_STRING"]
                  ?? configuration["DROP_POSTGRES_CONNECTION_STRING"]
                  ?? configuration["DATABASE_URL"]
                  ?? configuration.GetConnectionString("Postgres");

        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(raw);
                var userInfo = uri.UserInfo.Split(':');
                var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
                var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var dbName = uri.AbsolutePath.TrimStart('/');
                if (string.IsNullOrWhiteSpace(dbName)) dbName = "drop";

                return $"Host={host};Port={port};Database={dbName};Username={user};Password={pass};";
            }

            return raw;
        }

        var hostName = configuration["DROP_POSTGRES_HOST"] ?? "localhost";
        var portNum = configuration["DROP_POSTGRES_PORT"] ?? "5432";
        var db = configuration["DROP_POSTGRES_DB"] ?? "drop";
        var username = configuration["DROP_POSTGRES_USER"] ?? "postgres";
        var password = configuration["DROP_POSTGRES_PASSWORD"] ?? "postgres";

        return $"Host={hostName};Port={portNum};Database={db};Username={username};Password={password};";
    }

    public static void ConfigureDatabase(
        IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var provider = GetDatabaseProvider(configuration);

        if (provider == "postgres")
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            var connectionString = BuildPostgresConnectionString(configuration);
            services.AddDbContext<DropDbContext>(options =>
                options.UseNpgsql(connectionString));
        }
        else
        {
            var baseDataDir = AppDataPaths.ResolveBaseDataDirectory(environment, configuration);
            Directory.CreateDirectory(baseDataDir);

            var dbPath = Path.GetFullPath(AppDataPaths.GetDatabaseFilePath(baseDataDir));

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
            services.AddDbContext<DropDbContext>(options =>
                options.UseSqlite(connectionString));
        }
    }

    public static async Task InitializeDatabaseAsync(DropDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (db.Database.IsSqlite())
        {
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

            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Clips ADD COLUMN ParentClipId TEXT NULL;");
            }
            catch { /* Column already exists */ }

            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Clips ADD COLUMN Tags TEXT NULL;");
            }
            catch { /* Column already exists */ }

            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Clips ADD COLUMN AiAnalysisJson TEXT NULL;");
            }
            catch { /* Column already exists */ }

            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Clips ADD COLUMN LikesCount INTEGER NOT NULL DEFAULT 0;");
            }
            catch { /* Column already exists */ }
        }
        else if (db.Database.IsNpgsql())
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Clips\" ADD COLUMN IF NOT EXISTS \"Visibility\" text NOT NULL DEFAULT 'public';");
            }
            catch { /* Column already exists */ }

            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Collections\" ADD COLUMN IF NOT EXISTS \"Visibility\" text NOT NULL DEFAULT 'public';");
            }
            catch { /* Column already exists */ }

            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Clips\" ADD COLUMN IF NOT EXISTS \"ParentClipId\" text NULL;");
            }
            catch { /* Column already exists */ }

            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Clips\" ADD COLUMN IF NOT EXISTS \"Tags\" text NULL;");
            }
            catch { /* Column already exists */ }

            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Clips\" ADD COLUMN IF NOT EXISTS \"AiAnalysisJson\" text NULL;");
            }
            catch { /* Column already exists */ }

            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Clips\" ADD COLUMN IF NOT EXISTS \"LikesCount\" bigint NOT NULL DEFAULT 0;");
            }
            catch { /* Column already exists */ }
        }
    }
}
