namespace Drop.Api.Models;

public class CollectionClip
{
    public string CollectionId { get; set; } = string.Empty;
    public Collection Collection { get; set; } = null!;

    public string ClipId { get; set; } = string.Empty;
    public Clip Clip { get; set; } = null!;

    public int OrderIndex { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
