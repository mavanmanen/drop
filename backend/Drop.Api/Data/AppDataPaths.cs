namespace Drop.Api.Data;

public static class AppDataPaths
{
    public const string DefaultContainerDataDir = "/data";

    public static string ResolveBaseDataDirectory(IWebHostEnvironment? environment = null, IConfiguration? configuration = null)
    {
        // Testing environment support for isolated test execution
        if (environment != null && environment.IsEnvironment("Testing"))
        {
            var testDir = configuration?["TEST_DATA_DIR"];
            if (!string.IsNullOrWhiteSpace(testDir))
            {
                return Path.GetFullPath(testDir);
            }
        }

        // In container / Linux environment where /data exists
        if (Directory.Exists(DefaultContainerDataDir))
        {
            return DefaultContainerDataDir;
        }

        // Local development environments: check for repo-level or content-level data directory
        if (environment != null)
        {
            var repoDataDir = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../data"));
            if (Directory.Exists(repoDataDir))
            {
                return repoDataDir;
            }

            var contentDataDir = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "data"));
            if (Directory.Exists(contentDataDir))
            {
                return contentDataDir;
            }
        }

        var cwdDataDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data"));
        if (Directory.Exists(cwdDataDir))
        {
            return cwdDataDir;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data"));
    }

    public static string GetIngestDirectory(string baseDataDir) => Path.Combine(baseDataDir, "ingest");
    public static string GetClipsDirectory(string baseDataDir) => Path.Combine(baseDataDir, "clips");
    public static string GetThumbnailsDirectory(string baseDataDir) => Path.Combine(baseDataDir, "thumbnails");
    public static string GetDatabaseFilePath(string baseDataDir) => Path.Combine(baseDataDir, "db", "drop.db");
    public static string GetDataProtectionKeysDirectory(string baseDataDir) => Path.Combine(baseDataDir, "keys");
}

