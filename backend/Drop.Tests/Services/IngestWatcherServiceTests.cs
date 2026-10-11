using Drop.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Drop.Tests.Services;

public class IngestWatcherServiceTests
{
    private class FakeMediaStorage : IMediaStorageService
    {
        public string IngestDirectory { get; set; } = Path.GetTempPath();
        public string ClipsDirectory { get; set; } = Path.GetTempPath();
        public string ThumbnailsDirectory { get; set; } = Path.GetTempPath();

        public void DeleteClipFiles(string storedFileName, string? thumbnailFileName) { }
        public string GenerateId(int length = 8) => "test1234";
        public string GetClipFilePath(string storedFileName) => storedFileName;
        public string GetContentType(string fileName) => "video/mp4";
        public string? GetThumbnailFilePath(string? thumbnailFileName) => thumbnailFileName;
        public Task<(string storedFileName, string? thumbnailFileName, double? durationSeconds, long fileSize)> ProcessAndStoreVideoAsync(string sourceFilePath, string clipId, string originalFileName, bool isMove = true)
            => Task.FromResult(("stored.mp4", (string?)"thumb.jpg", (double?)10.0, 1024L));
        public Task<(string storedFileName, string? thumbnailFileName, double? durationSeconds, long fileSize)> SaveUploadedVideoAsync(Microsoft.AspNetCore.Http.IFormFile file, string clipId)
            => Task.FromResult(("stored.mp4", (string?)"thumb.jpg", (double?)10.0, 1024L));
    }

    [Theory]
    [InlineData("unlisted", "unlisted")]
    [InlineData("UNLISTED", "unlisted")]
    [InlineData("private", "private")]
    [InlineData("PRIVATE", "private")]
    [InlineData("public", "public")]
    [InlineData("PUBLIC", "public")]
    [InlineData("invalid_value", "public")]
    [InlineData("", "public")]
    [InlineData(null, "public")]
    public void DefaultVisibility_ResolvesCorrectlyFromConfiguration(string? configuredValue, string expectedVisibility)
    {
        var dict = new Dictionary<string, string?>();
        if (configuredValue != null)
        {
            dict["DROP_INGEST_DEFAULT_VISIBILITY"] = configuredValue;
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();

        var services = new ServiceCollection().BuildServiceProvider();
        var fakeStorage = new FakeMediaStorage();

        var watcher = new IngestWatcherService(
            services,
            fakeStorage,
            config,
            NullLogger<IngestWatcherService>.Instance);

        Assert.Equal(expectedVisibility, watcher.DefaultVisibility);
    }
}
