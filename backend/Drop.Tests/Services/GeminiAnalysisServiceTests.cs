using Drop.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Drop.Tests.Services;

public class GeminiAnalysisServiceTests
{
    [Fact]
    public void BuildPrompt_IncludesOriginalFileNameContext_WhenProvided()
    {
        var prompt = GeminiAnalysisService.BuildPrompt(
            mode: "tags",
            durationSeconds: 45.2,
            originalFileName: "Overwatch 2 2026-10-12 10-30-00.mp4");

        Assert.Contains("Overwatch 2 2026-10-12 10-30-00.mp4", prompt);
        Assert.Contains("Original recording filename:", prompt);
        Assert.Contains("accurately recognize the game", prompt);
        Assert.Contains("45.2 seconds", prompt);
    }

    [Fact]
    public void BuildPrompt_OmitsFileNameContext_WhenNullOrWhitespace()
    {
        var promptNull = GeminiAnalysisService.BuildPrompt(
            mode: "both",
            durationSeconds: 30.0,
            originalFileName: null);

        var promptEmpty = GeminiAnalysisService.BuildPrompt(
            mode: "both",
            durationSeconds: 30.0,
            originalFileName: "   ");

        Assert.DoesNotContain("Original recording filename:", promptNull);
        Assert.DoesNotContain("Original recording filename:", promptEmpty);
        Assert.Contains("30.0 seconds", promptNull);
    }

    [Theory]
    [InlineData("tags")]
    [InlineData("subclips")]
    [InlineData("both")]
    public void BuildPrompt_SupportsAllModes_WithFileNameContext(string mode)
    {
        var prompt = GeminiAnalysisService.BuildPrompt(
            mode: mode,
            durationSeconds: 60.0,
            originalFileName: "Valorant_Ace_Clutch.mp4");

        Assert.Contains("Valorant_Ace_Clutch.mp4", prompt);
        if (mode == "tags")
        {
            Assert.Contains("concise, relevant gaming tags", prompt);
        }
        else if (mode == "subclips")
        {
            Assert.Contains("suggest short sub-clips", prompt);
        }
        else
        {
            Assert.Contains("Generate between 3 to 8 concise, relevant gaming tags", prompt);
            Assert.Contains("suggest short sub-clips", prompt);
        }
    }

    [Fact]
    public async Task AnalyzeClipAsync_ThrowsInvalidOperationException_WhenApiKeyMissing()
    {
        var config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new GeminiAnalysisService(httpClient, config, NullLogger<GeminiAnalysisService>.Instance);

        Assert.False(service.IsConfigured);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnalyzeClipAsync("dummy.mp4", "video/mp4", 10.0, "both", "Apex_Legends.mp4"));

        Assert.Contains("GEMINI_API_KEY", ex.Message);
    }

    [Fact]
    public void AvailableModels_ContainsConfiguredFlashLiteAndFlashModels()
    {
        var config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new GeminiAnalysisService(httpClient, config, NullLogger<GeminiAnalysisService>.Instance);

        Assert.Contains("gemini-3.5-flash-lite", service.AvailableModels);
        Assert.Contains("gemini-3.1-flash-lite", service.AvailableModels);
        Assert.Contains("gemini-3.8-flash", service.AvailableModels);
    }
}
