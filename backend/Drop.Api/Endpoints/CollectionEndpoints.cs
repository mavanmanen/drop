using Drop.Api.Data;
using Drop.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Drop.Api.Endpoints;

public static class CollectionEndpoints
{
    public record CollectionDto(
        string Id,
        string Name,
        string? Description,
        DateTime CreatedAtUtc,
        int ClipCount,
        string? CoverThumbnailUrl,
        List<ClipEndpoints.ClipDto>? Clips
    );

    public record CreateCollectionRequest(string Name, string? Description);
    public record UpdateCollectionRequest(string Name, string? Description);
    public record AddClipToCollectionRequest(string ClipId);
    public record ReorderClipsRequest(List<string> OrderedClipIds);

    public static void MapCollectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/collections").WithTags("Collections");

        // Public: Get all collections
        group.MapGet("/", async (DropDbContext db) =>
        {
            var collections = await db.Collections
                .Include(c => c.CollectionClips)
                .ThenInclude(cc => cc.Clip)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync();

            var dtos = collections.Select(c =>
            {
                var firstClipWithThumb = c.CollectionClips
                    .OrderBy(cc => cc.OrderIndex)
                    .Select(cc => cc.Clip)
                    .FirstOrDefault(clip => !string.IsNullOrEmpty(clip.ThumbnailFileName));

                var coverUrl = firstClipWithThumb != null
                    ? $"/api/clips/{firstClipWithThumb.Id}/thumbnail"
                    : c.CollectionClips.FirstOrDefault()?.Clip != null
                        ? $"/api/clips/{c.CollectionClips.First().Clip.Id}/thumbnail"
                        : null;

                return new CollectionDto(
                    Id: c.Id,
                    Name: c.Name,
                    Description: c.Description,
                    CreatedAtUtc: c.CreatedAtUtc,
                    ClipCount: c.CollectionClips.Count,
                    CoverThumbnailUrl: coverUrl,
                    Clips: null
                );
            }).ToList();

            return Results.Ok(dtos);
        });

        // Public: Get single collection with clips
        group.MapGet("/{id}", async (string id, DropDbContext db) =>
        {
            var collection = await db.Collections
                .Include(c => c.CollectionClips)
                .ThenInclude(cc => cc.Clip)
                .ThenInclude(clip => clip.CollectionClips)
                .ThenInclude(cc => cc.Collection)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (collection == null) return Results.NotFound(new { error = "Collection not found" });

            var orderedClips = collection.CollectionClips
                .OrderBy(cc => cc.OrderIndex)
                .Select(cc => ClipEndpoints.ToDto(cc.Clip))
                .ToList();

            var coverUrl = orderedClips.FirstOrDefault(c => c.HasThumbnail)?.ThumbnailUrl
                           ?? orderedClips.FirstOrDefault()?.ThumbnailUrl;

            var dto = new CollectionDto(
                Id: collection.Id,
                Name: collection.Name,
                Description: collection.Description,
                CreatedAtUtc: collection.CreatedAtUtc,
                ClipCount: orderedClips.Count,
                CoverThumbnailUrl: coverUrl,
                Clips: orderedClips
            );

            return Results.Ok(dto);
        });

        // Admin: Create collection
        group.MapPost("/", async (CreateCollectionRequest req, DropDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest(new { error = "Collection name is required" });
            }

            var id = Guid.NewGuid().ToString("N")[..8];
            var collection = new Collection
            {
                Id = id,
                Name = req.Name.Trim(),
                Description = req.Description?.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Collections.Add(collection);
            await db.SaveChangesAsync();

            return Results.Created($"/api/collections/{collection.Id}", new CollectionDto(
                Id: collection.Id,
                Name: collection.Name,
                Description: collection.Description,
                CreatedAtUtc: collection.CreatedAtUtc,
                ClipCount: 0,
                CoverThumbnailUrl: null,
                Clips: []
            ));
        })
        .RequireAuthorization();

        // Admin: Update collection
        group.MapPut("/{id}", async (string id, UpdateCollectionRequest req, DropDbContext db) =>
        {
            var collection = await db.Collections.FindAsync(id);
            if (collection == null) return Results.NotFound(new { error = "Collection not found" });

            if (!string.IsNullOrWhiteSpace(req.Name))
            {
                collection.Name = req.Name.Trim();
            }
            collection.Description = req.Description?.Trim();

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                id = collection.Id,
                name = collection.Name,
                description = collection.Description
            });
        })
        .RequireAuthorization();

        // Admin: Delete collection
        group.MapDelete("/{id}", async (string id, DropDbContext db) =>
        {
            var collection = await db.Collections.FindAsync(id);
            if (collection == null) return Results.NotFound(new { error = "Collection not found" });

            db.Collections.Remove(collection);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Collection deleted", id });
        })
        .RequireAuthorization();

        // Admin: Add clip to collection
        group.MapPost("/{id}/clips", async (string id, AddClipToCollectionRequest req, DropDbContext db) =>
        {
            var collection = await db.Collections.FindAsync(id);
            if (collection == null) return Results.NotFound(new { error = "Collection not found" });

            var clip = await db.Clips.FindAsync(req.ClipId);
            if (clip == null) return Results.NotFound(new { error = "Clip not found" });

            var existing = await db.CollectionClips
                .FirstOrDefaultAsync(cc => cc.CollectionId == id && cc.ClipId == req.ClipId);

            if (existing != null)
            {
                return Results.Ok(new { message = "Clip already in collection" });
            }

            var maxOrder = await db.CollectionClips
                .Where(cc => cc.CollectionId == id)
                .Select(cc => (int?)cc.OrderIndex)
                .MaxAsync() ?? 0;

            db.CollectionClips.Add(new CollectionClip
            {
                CollectionId = id,
                ClipId = req.ClipId,
                OrderIndex = maxOrder + 1,
                AddedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Clip added to collection", collectionId = id, clipId = req.ClipId });
        })
        .RequireAuthorization();

        // Admin: Remove clip from collection
        group.MapDelete("/{id}/clips/{clipId}", async (string id, string clipId, DropDbContext db) =>
        {
            var entry = await db.CollectionClips
                .FirstOrDefaultAsync(cc => cc.CollectionId == id && cc.ClipId == clipId);

            if (entry == null) return Results.NotFound(new { error = "Clip not found in collection" });

            db.CollectionClips.Remove(entry);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Clip removed from collection", collectionId = id, clipId });
        })
        .RequireAuthorization();

        // Admin: Reorder clips in collection
        group.MapPut("/{id}/reorder", async (string id, ReorderClipsRequest req, DropDbContext db) =>
        {
            var entries = await db.CollectionClips
                .Where(cc => cc.CollectionId == id)
                .ToListAsync();

            for (var i = 0; i < req.OrderedClipIds.Count; i++)
            {
                var targetId = req.OrderedClipIds[i];
                var entry = entries.FirstOrDefault(e => e.ClipId == targetId);
                if (entry != null)
                {
                    entry.OrderIndex = i;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Collection reordered successfully" });
        })
        .RequireAuthorization();
    }
}
