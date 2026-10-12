using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Drop.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string TempDataDir { get; } = Path.Combine(Path.GetTempPath(), "drop_itest_" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(TempDataDir);

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "TEST_DATA_DIR", TempDataDir },
                { "DROP_ADMIN_USERNAME", "admin" },
                { "DROP_ADMIN_PASSWORD", "testpass123" },
                { "DROP_INGEST_DEFAULT_VISIBILITY", "public" }
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (Directory.Exists(TempDataDir))
            {
                Directory.Delete(TempDataDir, true);
            }
        }
        catch { }
    }
}
