namespace Drop.Api.Models;

public class Collection
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Visibility { get; set; } = "public"; // public, unlisted, private

    public List<CollectionClip> CollectionClips { get; set; } = [];
}
