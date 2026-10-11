using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Drop.Api.Services;

public interface IAuthService
{
    string AdminUsername { get; }
    bool ValidateCredentials(string username, string password);
    string GenerateToken(string username);
    ClaimsPrincipal? ValidateToken(string token);
}

public class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly string _adminUsername;
    private readonly string _adminPassword;
    private readonly byte[] _jwtKey;
    private readonly string _issuer = "drop";
    private readonly string _audience = "drop-users";

    public string AdminUsername => _adminUsername;

    public AuthService(IConfiguration configuration, ILogger<AuthService> logger)
    {
        _logger = logger;
        _adminUsername = configuration["DROP_ADMIN_USERNAME"] ?? configuration["ADMIN_USERNAME"] ?? "admin";
        _adminPassword = configuration["DROP_ADMIN_PASSWORD"] ?? configuration["ADMIN_PASSWORD"] ?? "admin123";

        var secret = configuration["DROP_JWT_SECRET"] ?? configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("DROP_JWT_SECRET not provided. A fallback session key is being used.");
            secret = "drop_default_super_secret_jwt_key_that_is_long_enough_2026_drop!";
        }

        _jwtKey = Encoding.UTF8.GetBytes(secret.PadRight(32, '!'));

        if (_adminPassword == "admin123")
        {
            _logger.LogWarning("Default admin password 'admin123' is active. Set DROP_ADMIN_PASSWORD environment variable for production.");
        }
    }

    public bool ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        var usernameMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(username.Trim()),
            Encoding.UTF8.GetBytes(_adminUsername.Trim()));

        var passwordMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(_adminPassword));

        return usernameMatches && passwordMatches;
    }

    public string GenerateToken(string username)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, "Admin")
            ]),
            Expires = DateTime.UtcNow.AddDays(30),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_jwtKey),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_jwtKey),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ClockSkew = TimeSpan.FromMinutes(5)
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
