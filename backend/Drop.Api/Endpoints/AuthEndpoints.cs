using System.Security.Claims;
using Drop.Api.Services;

namespace Drop.Api.Endpoints;

public static class AuthEndpoints
{
    public record LoginRequest(string Username, string Password);
    public record AuthStatusResponse(bool IsAuthenticated, string? Username);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", (LoginRequest req, IAuthService authService, HttpContext httpContext) =>
        {
            if (!authService.ValidateCredentials(req.Username, req.Password))
            {
                return Results.Json(new { error = "Invalid admin username or password" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var token = authService.GenerateToken(authService.AdminUsername);

            httpContext.Response.Cookies.Append("drop_auth", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Set to true if HTTPS termination is configured, lax allows localhost
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            return Results.Ok(new
            {
                token,
                username = authService.AdminUsername,
                isAuthenticated = true
            });
        });

        group.MapGet("/status", (ClaimsPrincipal user, IAuthService authService, HttpContext httpContext) =>
        {
            if (user.Identity?.IsAuthenticated == true)
            {
                return Results.Ok(new AuthStatusResponse(true, user.Identity.Name ?? authService.AdminUsername));
            }

            // Also check cookie if not set on principal
            if (httpContext.Request.Cookies.TryGetValue("drop_auth", out var cookieToken))
            {
                var principal = authService.ValidateToken(cookieToken);
                if (principal?.Identity?.IsAuthenticated == true)
                {
                    return Results.Ok(new AuthStatusResponse(true, principal.Identity.Name ?? authService.AdminUsername));
                }
            }

            return Results.Ok(new AuthStatusResponse(false, null));
        });

        group.MapPost("/logout", (HttpContext httpContext) =>
        {
            httpContext.Response.Cookies.Delete("drop_auth");
            return Results.Ok(new { message = "Logged out successfully" });
        });
    }
}
