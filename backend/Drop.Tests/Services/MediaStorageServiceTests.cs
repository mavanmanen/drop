using Drop.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Drop.Tests.Services;

public class MediaStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MediaStorageService _service;

    public MediaStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "drop_media_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DROP_DATA_DIR", _tempDir }
            })
            .Build();

        var fakeEnv = new FakeWebHostEnvironment { ContentRootPath = _tempDir };
        _service = new MediaStorageService(config, fakeEnv, NullLogger<MediaStorageService>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void GenerateId_ReturnsCorrectLength()
    {
        var id8 = _service.GenerateId(8);
        var id16 = _service.GenerateId(16);

        Assert.Equal(8, id8.Length);
        Assert.Equal(16, id16.Length);
    }

    [Fact]
    public void GenerateId_GeneratesUniqueValues()
    {
        var ids = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            var id = _service.GenerateId(8);
            Assert.DoesNotContain(id, ids);
            ids.Add(id);
        }
    }

    [Theory]
    [InlineData("video.mp4", "video/mp4")]
    [InlineData("clip.webm", "video/webm")]
    [InlineData("gameplay.mov", "video/quicktime")]
    [InlineData("recording.mkv", "video/x-matroska")]
    [InlineData("highlight.avi", "video/x-msvideo")]
    [InlineData("unknown.xyz", "application/octet-stream")]
    public void GetContentType_ReturnsExpectedMimeType(string fileName, string expectedMime)
    {
        var contentType = _service.GetContentType(fileName);
        Assert.Equal(expectedMime, contentType);
    }

    [Fact]
    public void StorageDirectories_AreCreatedOnInitialization()
    {
        Assert.True(Directory.Exists(_service.IngestDirectory));
        Assert.True(Directory.Exists(_service.ClipsDirectory));
        Assert.True(Directory.Exists(_service.ThumbnailsDirectory));
    }

    [Fact]
    public void GetClipFilePath_CombinesWithClipsDirectory()
    {
        var path = _service.GetClipFilePath("sample_clip.mp4");
        var expected = Path.Combine(_service.ClipsDirectory, "sample_clip.mp4");

        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetThumbnailFilePath_ReturnsNullWhenNullOrNonExistent_AndPathWhenFileExists()
    {
        Assert.Null(_service.GetThumbnailFilePath(null));
        Assert.Null(_service.GetThumbnailFilePath("nonexistent.jpg"));

        var thumbPath = Path.Combine(_service.ThumbnailsDirectory, "thumb.jpg");
        File.WriteAllText(thumbPath, "dummy");

        var path = _service.GetThumbnailFilePath("thumb.jpg");
        Assert.Equal(thumbPath, path);
    }

    private class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Drop.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
