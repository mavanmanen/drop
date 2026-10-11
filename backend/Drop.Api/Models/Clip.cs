namespace Drop.Api.Models;

public class Clip
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "video/mp4";
    public double? DurationSeconds { get; set; }
    public string? ThumbnailFileName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public long ViewsCount { get; set; } = 0;
    public string Visibility { get; set; } = "public"; // public, unlisted, private

    public string? ParentClipId { get; set; }
    public Clip? ParentClip { get; set; }
    public List<Clip> ChildClips { get; set; } = [];

    public List<CollectionClip> CollectionClips { get; set; } = [];
}
