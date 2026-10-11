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
        Assert.Equal("sqlite", json.GetProperty("databaseProvider").GetString());
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

    [Fact]
    public async Task ClipTrim_Unauthenticated_ReturnsUnauthorized()
    {
        var anonClient = CreateAnonymousClient();
        var response = await anonClient.PostAsJsonAsync("/api/clips/some-id/trim", new
        {
            startSeconds = 5.0,
            endSeconds = 15.0
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClipTrim_ValidatesTimeInputs()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var clipId = "clip-trim-val-" + Guid.NewGuid().ToString("N")[..6];

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DropDbContext>();
            db.Clips.Add(new Clip
            {
                Id = clipId,
                Title = "Source Video",
                OriginalFileName = "src.mp4",
                StoredFileName = $"{clipId}.mp4",
                FileSizeBytes = 100,
                DurationSeconds = 60.0,
                Visibility = "public"
            });
            await db.SaveChangesAsync();
        }

        // 1. Negative start time
        var negResp = await adminClient.PostAsJsonAsync($"/api/clips/{clipId}/trim", new
        {
            startSeconds = -2.0,
            endSeconds = 10.0
        });
        Assert.Equal(HttpStatusCode.BadRequest, negResp.StatusCode);

        // 2. End time <= start time
        var endBeforeStartResp = await adminClient.PostAsJsonAsync($"/api/clips/{clipId}/trim", new
        {
            startSeconds = 15.0,
            endSeconds = 10.0
        });
        Assert.Equal(HttpStatusCode.BadRequest, endBeforeStartResp.StatusCode);

        // 3. Start time exceeding duration
        var startExceedResp = await adminClient.PostAsJsonAsync($"/api/clips/{clipId}/trim", new
        {
            startSeconds = 65.0,
            endSeconds = 70.0
        });
        Assert.Equal(HttpStatusCode.BadRequest, startExceedResp.StatusCode);
    }

    [Fact]
    public async Task ClipParentChild_Relationships_AreIncludedAndPopulated()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var parentId = "parent-" + Guid.NewGuid().ToString("N")[..6];
        var childId = "child-" + Guid.NewGuid().ToString("N")[..6];

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DropDbContext>();
            var parent = new Clip
            {
                Id = parentId,
                Title = "Parent Long Video",
                OriginalFileName = "parent.mp4",
                StoredFileName = $"{parentId}.mp4",
                FileSizeBytes = 2048,
                DurationSeconds = 120.0,
                Visibility = "public"
            };
            var child = new Clip
            {
                Id = childId,
                Title = "Child Short Video",
                OriginalFileName = "child.mp4",
                StoredFileName = $"{childId}.mp4",
                FileSizeBytes = 512,
                DurationSeconds = 15.0,
                Visibility = "public",
                ParentClipId = parentId
            };
            db.Clips.AddRange(parent, child);
            await db.SaveChangesAsync();
        }

        // Fetch child clip -> must show parentClip
        var childResp = await adminClient.GetFromJsonAsync<JsonElement>($"/api/clips/{childId}");
        Assert.True(childResp.TryGetProperty("parentClip", out var parentObj));
        Assert.Equal(parentId, parentObj.GetProperty("id").GetString());
        Assert.Equal("Parent Long Video", parentObj.GetProperty("title").GetString());

        // Fetch parent clip -> must show childClips
        var parentResp = await adminClient.GetFromJsonAsync<JsonElement>($"/api/clips/{parentId}");
        Assert.True(parentResp.TryGetProperty("childClips", out var childrenArr));
        var childrenList = childrenArr.EnumerateArray().ToList();
        Assert.Contains(childrenList, c => c.GetProperty("id").GetString() == childId);

        // Deleting the parent clip must NOT delete the child clip
        var deleteParentResp = await adminClient.DeleteAsync($"/api/clips/{parentId}");
        Assert.Equal(HttpStatusCode.OK, deleteParentResp.StatusCode);

        // Child clip still exists and can be retrieved
        var childAfterDeleteResp = await adminClient.GetAsync($"/api/clips/{childId}");
        Assert.Equal(HttpStatusCode.OK, childAfterDeleteResp.StatusCode);

        // Parent clip reference on child is now null
        var childAfterDeleteJson = await childAfterDeleteResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(childAfterDeleteJson.TryGetProperty("parentClip", out var parentAfterDelete));
        Assert.Equal(JsonValueKind.Null, parentAfterDelete.ValueKind);
    }

    [Fact]
    public async Task Clip_TagsUpdateAndFiltering_WorksCorrectly()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var clipId = "tagstest" + Guid.NewGuid().ToString("N")[..6];

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DropDbContext>();
            db.Clips.Add(new Clip
            {
                Id = clipId,
                Title = "Sniper Ace Clip",
                OriginalFileName = "sniper.mp4",
                StoredFileName = $"{clipId}.mp4",
                FileSizeBytes = 100,
                DurationSeconds = 30.0,
                Visibility = "public",
                Tags = "sniper,ace"
            });
            await db.SaveChangesAsync();
        }

        // Verify tags returned in DTO
        var clipResp = await adminClient.GetFromJsonAsync<JsonElement>($"/api/clips/{clipId}");
        Assert.True(clipResp.TryGetProperty("tags", out var tagsArr));
        var tags = tagsArr.EnumerateArray().Select(t => t.GetString()).ToList();
        Assert.Contains("sniper", tags);
        Assert.Contains("ace", tags);

        // Update tags
        var updateResp = await adminClient.PutAsJsonAsync($"/api/clips/{clipId}", new
        {
            title = "Sniper Ace Clip Updated",
            description = "Great play",
            visibility = "public",
            tags = new[] { "clutch", "val" }
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updatedJson = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        var updatedTags = updatedJson.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList();
        Assert.Contains("clutch", updatedTags);
        Assert.Contains("val", updatedTags);
        Assert.DoesNotContain("sniper", updatedTags);

        // Filter by tag in list endpoint
        var filterResp = await adminClient.GetFromJsonAsync<JsonElement>("/api/clips?tag=clutch");
        var items = filterResp.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, c => c.GetProperty("id").GetString() == clipId);

        // Check ai-status endpoint (admin required)
        var aiStatusResp = await adminClient.GetAsync("/api/clips/ai-status");
        Assert.Equal(HttpStatusCode.OK, aiStatusResp.StatusCode);
        var aiStatusJson = await aiStatusResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(aiStatusJson.TryGetProperty("isConfigured", out _));
        Assert.True(aiStatusJson.TryGetProperty("availableModels", out var modelsArr));
        Assert.Contains(modelsArr.EnumerateArray(), m => m.GetString() == "gemini-3.8-flash");

        // Anonymous call to ai-status returns Unauthorized
        var anonClient = CreateAnonymousClient();
        var anonAiResp = await anonClient.GetAsync("/api/clips/ai-status");
        Assert.Equal(HttpStatusCode.Unauthorized, anonAiResp.StatusCode);

        // Verify persisting AI analysis onto clip
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DropDbContext>();
            var clip = await db.Clips.FindAsync(clipId);
            clip!.AiAnalysisJson = "{\"tags\":[\"clutch\",\"sniper\"],\"subClips\":[{\"title\":\"Headshot\",\"description\":\"Crisp shot\",\"startSeconds\":10.5,\"endSeconds\":18.0}],\"summary\":\"Great clutch moment\"}";
            await db.SaveChangesAsync();
        }

        var clipWithAiResp = await adminClient.GetFromJsonAsync<JsonElement>($"/api/clips/{clipId}");
        Assert.True(clipWithAiResp.TryGetProperty("latestAiAnalysis", out var latestAi));
        Assert.Equal(JsonValueKind.Object, latestAi.ValueKind);
        Assert.Equal("Great clutch moment", latestAi.GetProperty("summary").GetString());
        Assert.Equal("Headshot", latestAi.GetProperty("subClips")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Clip_OpenGraphBotEndpoints_ReturnVideoEmbedMetaTags()
    {
        var clipId = "og-test-" + Guid.NewGuid().ToString("N")[..6];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DropDbContext>();
            db.Clips.Add(new Clip
            {
                Id = clipId,
                Title = "Discord Embed Victory",
                Description = "Watch this epic play",
                OriginalFileName = "victory.mp4",
                StoredFileName = $"{clipId}.mp4",
                FileSizeBytes = 5000,
                DurationSeconds = 25.0,
                Visibility = "public"
            });
            await db.SaveChangesAsync();
        }

        var anonClient = CreateAnonymousClient();

        // 1. Request to /clip/{id} with Discordbot User-Agent
        var discordReq = new HttpRequestMessage(HttpMethod.Get, $"/clip/{clipId}");
        discordReq.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Discordbot/2.0; +https://discord.app)");
        discordReq.Headers.Add("X-Forwarded-Proto", "https");
        discordReq.Headers.Add("X-Forwarded-Host", "drop.mavm.nl");

        var discordResp = await anonClient.SendAsync(discordReq);
        Assert.Equal(HttpStatusCode.OK, discordResp.StatusCode);
        var html = await discordResp.Content.ReadAsStringAsync();

        Assert.Contains("<meta property=\"og:type\" content=\"video.other\">", html);
        Assert.Contains("<meta property=\"og:title\" content=\"Discord Embed Victory\">", html);
        Assert.Contains("<meta property=\"og:video\" content=\"https://drop.mavm.nl/api/clips/" + clipId + "/stream\">", html);
        Assert.Contains("<meta property=\"og:video:secure_url\" content=\"https://drop.mavm.nl/api/clips/" + clipId + "/stream\">", html);
        Assert.Contains("<meta property=\"og:image\" content=\"https://drop.mavm.nl/api/clips/" + clipId + "/thumbnail\">", html);
        Assert.Contains("<meta name=\"twitter:card\" content=\"player\">", html);

        // 2. Request to /c/{id} with Discordbot User-Agent
        var shortReq = new HttpRequestMessage(HttpMethod.Get, $"/c/{clipId}");
        shortReq.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Discordbot/2.0; +https://discord.app)");
        shortReq.Headers.Add("X-Forwarded-Proto", "https");
        shortReq.Headers.Add("X-Forwarded-Host", "drop.mavm.nl");

        var shortResp = await anonClient.SendAsync(shortReq);
        Assert.Equal(HttpStatusCode.OK, shortResp.StatusCode);
        var shortHtml = await shortResp.Content.ReadAsStringAsync();
        Assert.Contains("<meta property=\"og:video\" content=\"https://drop.mavm.nl/api/clips/" + clipId + "/stream\">", shortHtml);

        // 3. Request to /c/{id} as regular browser user -> 302 Redirect to /clip/{id}
        var noRedirectClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var browserReq = new HttpRequestMessage(HttpMethod.Get, $"/c/{clipId}");
        browserReq.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0.0.0");
        var browserResp = await noRedirectClient.SendAsync(browserReq);
        Assert.Equal(HttpStatusCode.Redirect, browserResp.StatusCode);
        Assert.Equal($"/clip/{clipId}", browserResp.Headers.Location?.ToString());
    }
}
