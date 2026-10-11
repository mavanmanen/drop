using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Drop.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Drop.Tests.Services;

public class AuthServiceTests
{
    private static (AuthService Service, IConfiguration Config) CreateAuthService(
        string adminUsername = "testadmin",
        string adminPassword = "testpassword123!",
        string jwtSecret = "super_secret_testing_jwt_key_that_is_at_least_32_chars_long!")
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "DROP_ADMIN_USERNAME", adminUsername },
            { "DROP_ADMIN_PASSWORD", adminPassword },
            { "DROP_JWT_SECRET", jwtSecret }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        return (new AuthService(configuration, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance), configuration);
    }

    [Fact]
    public void ValidateCredentials_WithCorrectCredentials_ReturnsTrue()
    {
        var (authService, _) = CreateAuthService();

        var result = authService.ValidateCredentials("testadmin", "testpassword123!");

        Assert.True(result);
    }

    [Fact]
    public void ValidateCredentials_WithWrongPassword_ReturnsFalse()
    {
        var (authService, _) = CreateAuthService();

        var result = authService.ValidateCredentials("testadmin", "wrong_password");

        Assert.False(result);
    }

    [Fact]
    public void ValidateCredentials_WithWrongUsername_ReturnsFalse()
    {
        var (authService, _) = CreateAuthService();

        var result = authService.ValidateCredentials("unknown_user", "testpassword123!");

        Assert.False(result);
    }

    [Fact]
    public void GenerateToken_GeneratesValidJwtWithCorrectClaims()
    {
        var (authService, _) = CreateAuthService();

        var token = authService.GenerateToken("testadmin");

        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var principal = authService.ValidateToken(token);
        Assert.NotNull(principal);
        Assert.Equal("testadmin", principal.Identity?.Name);
        Assert.True(principal.IsInRole("Admin"));
    }

    [Fact]
    public void ValidateToken_WithTamperedToken_ReturnsNull()
    {
        var (authService, _) = CreateAuthService();
        var token = authService.GenerateToken("testadmin");
        var tamperedToken = token + "corrupted";

        var principal = authService.ValidateToken(tamperedToken);

        Assert.Null(principal);
    }

    [Fact]
    public void ValidateToken_WithGarbageString_ReturnsNull()
    {
        var (authService, _) = CreateAuthService();

        var principal = authService.ValidateToken("not-a-valid-jwt");

        Assert.Null(principal);
    }

    [Fact]
    public void IsAdmin_WithValidBearerHeader_ReturnsTrue()
    {
        var (authService, _) = CreateAuthService();
        var token = authService.GenerateToken("testadmin");

        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = $"Bearer {token}";

        var isAdmin = authService.IsAdmin(context);

        Assert.True(isAdmin);
    }

    [Fact]
    public void IsAdmin_WithValidCookie_ReturnsTrue()
    {
        var (authService, _) = CreateAuthService();
        var token = authService.GenerateToken("testadmin");

        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = $"drop_auth={token}";

        var isAdmin = authService.IsAdmin(context);

        Assert.True(isAdmin);
    }

    [Fact]
    public void IsAdmin_WithoutTokenOrCookie_ReturnsFalse()
    {
        var (authService, _) = CreateAuthService();
        var context = new DefaultHttpContext();

        var isAdmin = authService.IsAdmin(context);

        Assert.False(isAdmin);
    }

    [Fact]
    public void IsAdmin_WithInvalidBearerHeader_ReturnsFalse()
    {
        var (authService, _) = CreateAuthService();
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer invalid_token";

        var isAdmin = authService.IsAdmin(context);

        Assert.False(isAdmin);
    }
}
