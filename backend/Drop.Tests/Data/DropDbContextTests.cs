using Drop.Api.Data;
using Drop.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Drop.Tests.Data;

public class DropDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DropDbContext> _options;

    public DropDbContextTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DropDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new DropDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private DropDbContext CreateContext() => new(_options);

    [Fact]
    public async Task Clip_DefaultValues_AreCorrectlySet()
    {
        using var db = CreateContext();

        var clip = new Clip
        {
            Id = "clip1",
            Title = "First Clip",
            OriginalFileName = "test.mp4",
            StoredFileName = "clip1.mp4",
            FileSizeBytes = 1024,
            ContentType = "video/mp4"
        };

        db.Clips.Add(clip);
        await db.SaveChangesAsync();

        using var verifyDb = CreateContext();
        var savedClip = await verifyDb.Clips.FindAsync("clip1");

        Assert.NotNull(savedClip);
        Assert.Equal("public", savedClip.Visibility);
        Assert.Equal(0, savedClip.ViewsCount);
        Assert.Equal("First Clip", savedClip.Title);
    }

    [Fact]
    public async Task Collection_WithClips_SavesRelationships()
    {
        using var db = CreateContext();

        var clip1 = new Clip
        {
            Id = "clipA",
            Title = "Clip Alpha",
            OriginalFileName = "alpha.mp4",
            StoredFileName = "clipA.mp4",
            FileSizeBytes = 1000,
            ContentType = "video/mp4"
        };
        var clip2 = new Clip
        {
            Id = "clipB",
            Title = "Clip Beta",
            OriginalFileName = "beta.mp4",
            StoredFileName = "clipB.mp4",
            FileSizeBytes = 2000,
            ContentType = "video/mp4"
        };

        var collection = new Collection
        {
            Id = "col1",
            Name = "Best Plays",
            Description = "Highlight reel",
            Visibility = "public"
        };

        collection.CollectionClips.Add(new CollectionClip { ClipId = "clipA", OrderIndex = 0 });
        collection.CollectionClips.Add(new CollectionClip { ClipId = "clipB", OrderIndex = 1 });

        db.Clips.AddRange(clip1, clip2);
        db.Collections.Add(collection);
        await db.SaveChangesAsync();

        using var verifyDb = CreateContext();
        var savedCollection = await verifyDb.Collections
            .Include(c => c.CollectionClips)
            .ThenInclude(cc => cc.Clip)
            .FirstOrDefaultAsync(c => c.Id == "col1");

        Assert.NotNull(savedCollection);
        Assert.Equal(2, savedCollection.CollectionClips.Count);
        Assert.Equal("clipA", savedCollection.CollectionClips.OrderBy(cc => cc.OrderIndex).First().ClipId);
        Assert.Equal("Clip Alpha", savedCollection.CollectionClips.OrderBy(cc => cc.OrderIndex).First().Clip.Title);
    }

    [Fact]
    public async Task Query_FilterByVisibility_ReturnsExpectedClips()
    {
        using var db = CreateContext();

        db.Clips.AddRange(
            new Clip { Id = "c1", Title = "Public 1", OriginalFileName = "1.mp4", StoredFileName = "1.mp4", FileSizeBytes = 1, ContentType = "video/mp4", Visibility = "public" },
            new Clip { Id = "c2", Title = "Public 2", OriginalFileName = "2.mp4", StoredFileName = "2.mp4", FileSizeBytes = 1, ContentType = "video/mp4", Visibility = "public" },
            new Clip { Id = "c3", Title = "Unlisted 1", OriginalFileName = "3.mp4", StoredFileName = "3.mp4", FileSizeBytes = 1, ContentType = "video/mp4", Visibility = "unlisted" },
            new Clip { Id = "c4", Title = "Private 1", OriginalFileName = "4.mp4", StoredFileName = "4.mp4", FileSizeBytes = 1, ContentType = "video/mp4", Visibility = "private" }
        );
        await db.SaveChangesAsync();

        using var queryDb = CreateContext();

        var publicClips = await queryDb.Clips.Where(c => c.Visibility == "public").ToListAsync();
        var unlistedClips = await queryDb.Clips.Where(c => c.Visibility == "unlisted").ToListAsync();
        var privateClips = await queryDb.Clips.Where(c => c.Visibility == "private").ToListAsync();

        Assert.Equal(2, publicClips.Count);
        Assert.Single(unlistedClips);
        Assert.Single(privateClips);
    }
}
