using Drop.Api.Data;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Drop.Tests.Data;

public class DatabaseConfiguratorTests
{
    [Theory]
    [InlineData("postgres", "postgres")]
    [InlineData("POSTGRES", "postgres")]
    [InlineData("postgresql", "postgres")]
    [InlineData("sqlite", "sqlite")]
    [InlineData("SQLITE", "sqlite")]
    [InlineData("", "sqlite")]
    [InlineData(null, "sqlite")]
    public void GetDatabaseProvider_ResolvesProviderExplicitly(string? configuredValue, string expected)
    {
        var dict = new Dictionary<string, string?>();
        if (configuredValue != null)
        {
            dict["DROP_DB_PROVIDER"] = configuredValue;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var provider = DatabaseConfigurator.GetDatabaseProvider(config);
        Assert.Equal(expected, provider);
    }

    [Fact]
    public void GetDatabaseProvider_AutoDetectsPostgresFromDatabaseUrl()
    {
        var dict = new Dictionary<string, string?>
        {
            { "DATABASE_URL", "postgres://user:pass@localhost:5432/drop_db" }
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var provider = DatabaseConfigurator.GetDatabaseProvider(config);
        Assert.Equal("postgres", provider);
    }

    [Fact]
    public void GetDatabaseProvider_AutoDetectsPostgresFromPostgresHost()
    {
        var dict = new Dictionary<string, string?>
        {
            { "DROP_POSTGRES_HOST", "postgres-service" }
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var provider = DatabaseConfigurator.GetDatabaseProvider(config);
        Assert.Equal("postgres", provider);
    }

    [Fact]
    public void GetDatabaseProvider_AutoDetectsPostgresFromConnectionString()
    {
        var dict = new Dictionary<string, string?>
        {
            { "DROP_DB_CONNECTION_STRING", "Host=db.internal;Database=drop;Username=admin;Password=secret" }
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var provider = DatabaseConfigurator.GetDatabaseProvider(config);
        Assert.Equal("postgres", provider);
    }

    [Fact]
    public void BuildPostgresConnectionString_ParsesPostgresUrl()
    {
        var dict = new Dictionary<string, string?>
        {
            { "DATABASE_URL", "postgres://myuser:mypassword@db.remote.com:5433/custom_db" }
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var connStr = DatabaseConfigurator.BuildPostgresConnectionString(config);

        Assert.Contains("Host=db.remote.com;", connStr);
        Assert.Contains("Port=5433;", connStr);
        Assert.Contains("Database=custom_db;", connStr);
        Assert.Contains("Username=myuser;", connStr);
        Assert.Contains("Password=mypassword;", connStr);
    }

    [Fact]
    public void BuildPostgresConnectionString_ConstructsFromIndividualVariables()
    {
        var dict = new Dictionary<string, string?>
        {
            { "DROP_POSTGRES_HOST", "pg-cluster" },
            { "DROP_POSTGRES_PORT", "5439" },
            { "DROP_POSTGRES_DB", "prod_drop" },
            { "DROP_POSTGRES_USER", "drop_user" },
            { "DROP_POSTGRES_PASSWORD", "secret123" }
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var connStr = DatabaseConfigurator.BuildPostgresConnectionString(config);

        Assert.Equal("Host=pg-cluster;Port=5439;Database=prod_drop;Username=drop_user;Password=secret123;", connStr);
    }

    [Fact]
    public void BuildPostgresConnectionString_DefaultsCorrectly()
    {
        var dict = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var connStr = DatabaseConfigurator.BuildPostgresConnectionString(config);

        Assert.Equal("Host=localhost;Port=5432;Database=drop;Username=postgres;Password=postgres;", connStr);
    }
}
