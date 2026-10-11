using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Drop.Api.Data;
using Drop.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Drop.Tests.Integration;

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAnonymousClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    private async Task<(HttpClient Client, string Token)> CreateAdminClientAsync()
    {
        var authClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var loginResponse = await authClient.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "testpass123"
        });

        loginResponse.EnsureSuccessStatusCode();
        var content = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = content.GetProperty("token").GetString()!;

        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (authClient, token);
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var client = CreateAnonymousClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", text);
    }

    [Fact]
    public async Task SystemStatus_ReturnsSystemInformation()
    {
        var client = CreateAnonymousClient();
        var response = await client.GetAsync("/api/system/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("drop", json.GetProperty("name").GetString());
        Assert.Equal("admin", json.GetProperty("adminUsername").GetString());
        Assert.Equal("public", json.GetProperty("defaultIngestVisibility").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "incorrect-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthStatus_Unauthenticated_ReturnsFalse()
    {
        var client = CreateAnonymousClient();
        var response = await client.GetAsync("/api/auth/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task AuthStatus_WithBearerToken_ReturnsTrue()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var response = await adminClient.GetAsync("/api/auth/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("admin", json.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Collections_VisibilityRules_AreStrictlyEnforced()
    {
        var anonClient = CreateAnonymousClient();
        var (adminClient, _) = await CreateAdminClientAsync();

        // 1. Unauthenticated creation should be rejected (401)
        var unauthCreate = await anonClient.PostAsJsonAsync("/api/collections", new
        {
            name = "Forbidden",
            visibility = "public"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthCreate.StatusCode);

        // 2. Admin creates 3 collections: public, unlisted, private
        var pubResp = await adminClient.PostAsJsonAsync("/api/collections", new { name = "Public Collection", visibility = "public" });
        pubResp.EnsureSuccessStatusCode();
        var pubJson = await pubResp.Content.ReadFromJsonAsync<JsonElement>();
        var pubId = pubJson.GetProperty("id").GetString()!;

        var unlistedResp = await adminClient.PostAsJsonAsync("/api/collections", new { name = "Unlisted Collection", visibility = "unlisted" });
        unlistedResp.EnsureSuccessStatusCode();
        var unlistedJson = await unlistedResp.Content.ReadFromJsonAsync<JsonElement>();
        var unlistedId = unlistedJson.GetProperty("id").GetString()!;

        var privResp = await adminClient.PostAsJsonAsync("/api/collections", new { name = "Private Collection", visibility = "private" });
        privResp.EnsureSuccessStatusCode();
        var privJson = await privResp.Content.ReadFromJsonAsync<JsonElement>();
        var privId = privJson.GetProperty("id").GetString()!;

        // 3. Public GET /api/collections -> only contains public collection
        var publicList = await anonClient.GetFromJsonAsync<List<JsonElement>>("/api/collections");
        Assert.NotNull(publicList);
        Assert.Contains(publicList, c => c.GetProperty("id").GetString() == pubId);
        Assert.DoesNotContain(publicList, c => c.GetProperty("id").GetString() == unlistedId);
        Assert.DoesNotContain(publicList, c => c.GetProperty("id").GetString() == privId);

        // 4. Admin GET /api/collections -> contains all collections
        var adminList = await adminClient.GetFromJsonAsync<List<JsonElement>>("/api/collections");
        Assert.NotNull(adminList);
        Assert.Contains(adminList, c => c.GetProperty("id").GetString() == pubId);
        Assert.Contains(adminList, c => c.GetProperty("id").GetString() == unlistedId);
        Assert.Contains(adminList, c => c.GetProperty("id").GetString() == privId);

        // 5. Public direct link to unlisted collection -> 200 OK
        var unlistedDirect = await anonClient.GetAsync($"/api/collections/{unlistedId}");
        Assert.Equal(HttpStatusCode.OK, unlistedDirect.StatusCode);

        // 6. Public direct link to private collection -> 404 NotFound
        var privPublicDirect = await anonClient.GetAsync($"/api/collections/{privId}");
        Assert.Equal(HttpStatusCode.NotFound, privPublicDirect.StatusCode);

        // 7. Admin direct link to private collection -> 200 OK
        var adminPrivResp = await adminClient.GetAsync($"/api/collections/{privId}");
        Assert.Equal(HttpStatusCode.OK, adminPrivResp.StatusCode);
    }

    [Fact]
    public async Task Clips_VisibilityRules_AreStrictlyEnforced()
    {
        var anonClient = CreateAnonymousClient();
        var (adminClient, _) = await CreateAdminClientAsync();

        var pubId = "pub-" + Guid.NewGuid().ToString("N")[..6];
        var unlId = "unl-" + Guid.NewGuid().ToString("N")[..6];
        var prvId = "prv-" + Guid.NewGuid().ToString("N")[..6];

        // Seed clips directly into DB scope
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DropDbContext>();
            db.Clips.AddRange(
                new Clip { Id = pubId, Title = "Public Video", OriginalFileName = "pub.mp4", StoredFileName = $"{pubId}.mp4", FileSizeBytes = 10, ContentType = "video/mp4", Visibility = "public" },
                new Clip { Id = unlId, Title = "Unlisted Video", OriginalFileName = "unl.mp4", StoredFileName = $"{unlId}.mp4", FileSizeBytes = 10, ContentType = "video/mp4", Visibility = "unlisted" },
                new Clip { Id = prvId, Title = "Private Video", OriginalFileName = "prv.mp4", StoredFileName = $"{prvId}.mp4", FileSizeBytes = 10, ContentType = "video/mp4", Visibility = "private" }
            );
            await db.SaveChangesAsync();
        }

        // Public GET /api/clips: Only returns public clips
        var pubClipsResp = await anonClient.GetFromJsonAsync<JsonElement>("/api/clips");
        var pubItems = pubClipsResp.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(pubItems, c => c.GetProperty("id").GetString() == pubId);
        Assert.DoesNotContain(pubItems, c => c.GetProperty("id").GetString() == unlId);
        Assert.DoesNotContain(pubItems, c => c.GetProperty("id").GetString() == prvId);

        // Public direct link to unlisted clip: 200 OK
        var unlDirect = await anonClient.GetAsync($"/api/clips/{unlId}");
        Assert.Equal(HttpStatusCode.OK, unlDirect.StatusCode);

        // Public direct link to private clip: 404 NotFound
        var prvPublicDirect = await anonClient.GetAsync($"/api/clips/{prvId}");
        Assert.Equal(HttpStatusCode.NotFound, prvPublicDirect.StatusCode);

        // Admin direct link to private clip: 200 OK
        var prvAdminResp = await adminClient.GetAsync($"/api/clips/{prvId}");
        Assert.Equal(HttpStatusCode.OK, prvAdminResp.StatusCode);
    }
}
