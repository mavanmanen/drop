using Drop.Api.Data;
using Drop.Api.Models;
using Drop.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Drop.Api.Endpoints;

public static class ClipEndpoints
{
    public record ClipDto(
        string Id,
        string Title,
        string? Description,
        string OriginalFileName,
        long FileSizeBytes,
        string ContentType,
        double? DurationSeconds,
        bool HasThumbnail,
        string VideoUrl,
        string? ThumbnailUrl,
        DateTime CreatedAtUtc,
        long ViewsCount,
        string Visibility,
        List<string> Tags,
        AiClipAnalysisResult? LatestAiAnalysis,
        List<ClipCollectionSummaryDto> Collections,
        ClipSummaryDto? ParentClip,
        List<ClipSummaryDto> ChildClips
    );

    public record ClipSummaryDto(
        string Id,
        string Title,
        string? ThumbnailUrl,
        double? DurationSeconds,
        string Visibility,
        DateTime CreatedAtUtc
    );

    public record ClipCollectionSummaryDto(string Id, string Name);
    public record ClipUpdateRequest(string Title, string? Description, string? Visibility, List<string>? Tags);
    public record ClipTrimRequest(
        double StartSeconds,
        double EndSeconds,
        string? Title,
        string? Description,
        string? Visibility,
        string? CollectionId,
        List<string>? Tags
    );
    public record ClipAnalyzeAiRequest(string Mode, string? Model = null); // "tags", "subclips", "both"
    public record ClipListResponse(List<ClipDto> Items, int TotalCount, int Page, int PageSize);

    public static string NormalizeVisibility(string? v)
    {
        var val = v?.Trim().ToLowerInvariant();
        return val switch
        {
            "unlisted" => "unlisted",
            "private" => "private",
            _ => "public"
        };
    }

    public static List<string> ParseTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags)) return [];
        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string FormatTags(IEnumerable<string>? tags)
    {
        if (tags == null) return string.Empty;
        var clean = tags.Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().TrimStart('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(",", clean);
    }

    public static AiClipAnalysisResult? ParseAiAnalysis(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return System.Text.Json.JsonSerializer.Deserialize<AiClipAnalysisResult>(json, options);
        }
        catch
        {
            return null;
        }
    }

    public static void MapClipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clips").WithTags("Clips");

        // List clips: Public users see only "public"; Admin sees public, unlisted, and private
        group.MapGet("/", async (
            DropDbContext db,
            IAuthService authService,
            HttpContext httpContext,
            [FromQuery] string? search,
            [FromQuery] string? collectionId,
            [FromQuery] string? visibility,
            [FromQuery] string? tag,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 24) =>
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 24;

            var isAdmin = authService.IsAdmin(httpContext);

            IQueryable<Clip> query = db.Clips
                .Include(c => c.CollectionClips)
                .ThenInclude(cc => cc.Collection);

            // Visibility filtering
            if (!isAdmin)
            {
                query = query.Where(c => c.Visibility == "public");
            }
            else if (!string.IsNullOrWhiteSpace(visibility))
            {
                var norm = NormalizeVisibility(visibility);
                query = query.Where(c => c.Visibility == norm);
            }

            if (!string.IsNullOrWhiteSpace(collectionId))
            {
                query = query.Where(c => c.CollectionClips.Any(cc => cc.CollectionId == collectionId));
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                var t = tag.Trim().ToLower();
                query = query.Where(c => c.Tags != null && c.Tags.ToLower().Contains(t));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(c =>
                    c.Title.ToLower().Contains(term) ||
                    (c.Description != null && c.Description.ToLower().Contains(term)) ||
                    (c.Tags != null && c.Tags.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(c => c.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => ToDto(c))
                .ToListAsync();

            return Results.Ok(new ClipListResponse(items, totalCount, page, pageSize));
        });

        // Get single clip: Public and unlisted can be viewed by anyone with direct link; Private requires admin
        group.MapGet("/{id}", async (string id, DropDbContext db, IAuthService authService, HttpContext httpContext) =>
        {
            var isAdmin = authService.IsAdmin(httpContext);

            var clip = await db.Clips
                .Include(c => c.CollectionClips)
                .ThenInclude(cc => cc.Collection)
                .Include(c => c.ParentClip)
                .Include(c => c.ChildClips)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (clip == null) return Results.NotFound(new { error = "Clip not found" });

            if (clip.Visibility == "private" && !isAdmin)
            {
                return Results.NotFound(new { error = "Clip not found" });
            }

            // Increment views
            clip.ViewsCount++;
            await db.SaveChangesAsync();

            return Results.Ok(ToDto(clip, isAdmin));
        });

        // Stream video: Private clips require admin
        group.MapMethods("/{id}/stream", ["GET", "HEAD"], (
            string id,
            DropDbContext db,
            IMediaStorageService mediaStorage,
            IAuthService authService,
            HttpContext httpContext) =>
        {
            var clip = db.Clips.AsNoTracking().FirstOrDefault(c => c.Id == id);
            if (clip == null) return Results.NotFound(new { error = "Clip not found" });

            if (clip.Visibility == "private" && !authService.IsAdmin(httpContext))
            {
                return Results.NotFound(new { error = "Clip not found" });
            }

            var filePath = Path.GetFullPath(mediaStorage.GetClipFilePath(clip.StoredFileName));
            if (!File.Exists(filePath))
            {
                return Results.NotFound(new { error = "Video file missing on disk" });
            }

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Results.Stream(
                stream,
                contentType: clip.ContentType,
                fileDownloadName: clip.OriginalFileName,
                enableRangeProcessing: true);
        });

        // Thumbnail: Private clips require admin
        group.MapMethods("/{id}/thumbnail", ["GET", "HEAD"], (
            string id,
            DropDbContext db,
            IMediaStorageService mediaStorage,
            IAuthService authService,
            HttpContext httpContext) =>
        {
            var clip = db.Clips.AsNoTracking().FirstOrDefault(c => c.Id == id);
            if (clip == null) return Results.NotFound();

            if (clip.Visibility == "private" && !authService.IsAdmin(httpContext))
            {
                return Results.NotFound();
            }

            var thumbPath = mediaStorage.GetThumbnailFilePath(clip.ThumbnailFileName);
            if (thumbPath != null && File.Exists(thumbPath))
            {
                var stream = new FileStream(Path.GetFullPath(thumbPath), FileMode.Open, FileAccess.Read, FileShare.Read);
                return Results.Stream(stream, "image/jpeg");
            }

            // Return SVG placeholder
            const string svg = """
                <svg xmlns="http://www.w3.org/2000/svg" width="640" height="360" viewBox="0 0 640 360" fill="none">
                    <rect width="640" height="360" fill="#12141a"/>
                    <circle cx="320" cy="180" r="48" fill="#1f2430" stroke="#00e5ff" stroke-width="3"/>
                    <polygon points="312,160 338,180 312,200" fill="#00e5ff"/>
                    <text x="320" y="255" text-anchor="middle" fill="#64748b" font-family="system-ui, sans-serif" font-size="14" font-weight="600">DROP CLIP</text>
                </svg>
                """;
            return Results.Content(svg, "image/svg+xml");
        });

        // Admin: Direct Upload through web interface
        group.MapPost("/upload", async (
            IFormFile file,
            [FromForm] string? title,
            [FromForm] string? description,
            [FromForm] string? collectionId,
            [FromForm] string? visibility,
            DropDbContext db,
            IMediaStorageService mediaStorage) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "No video file was uploaded" });
            }

            var clipId = mediaStorage.GenerateId(8);
            var effectiveTitle = !string.IsNullOrWhiteSpace(title)
                ? title.Trim()
                : Path.GetFileNameWithoutExtension(file.FileName).Replace('_', ' ').Trim();

            var (storedFileName, thumbnailFileName, durationSeconds, fileSize) =
                await mediaStorage.SaveUploadedVideoAsync(file, clipId);

            var clip = new Clip
            {
                Id = clipId,
                Title = effectiveTitle,
                Description = description?.Trim(),
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                FileSizeBytes = fileSize,
                ContentType = mediaStorage.GetContentType(file.FileName),
                DurationSeconds = durationSeconds,
                ThumbnailFileName = thumbnailFileName,
                CreatedAtUtc = DateTime.UtcNow,
                ViewsCount = 0,
                Visibility = NormalizeVisibility(visibility)
            };

            db.Clips.Add(clip);

            if (!string.IsNullOrWhiteSpace(collectionId))
            {
                var collection = await db.Collections.FindAsync(collectionId);
                if (collection != null)
                {
                    var maxOrder = await db.CollectionClips
                        .Where(cc => cc.CollectionId == collectionId)
                        .Select(cc => (int?)cc.OrderIndex)
                        .MaxAsync() ?? 0;

                    db.CollectionClips.Add(new CollectionClip
                    {
                        CollectionId = collectionId,
                        ClipId = clipId,
                        OrderIndex = maxOrder + 1,
                        AddedAtUtc = DateTime.UtcNow
                    });
                }
            }

            await db.SaveChangesAsync();

            // Re-fetch with relations for DTO
            var created = await db.Clips
                .Include(c => c.CollectionClips)
                .ThenInclude(cc => cc.Collection)
                .FirstAsync(c => c.Id == clipId);

            return Results.Created($"/api/clips/{clipId}", ToDto(created));
        })
        .DisableAntiforgery()
        .RequireAuthorization();

        // Admin: Update clip title, description, visibility, tags
        group.MapPut("/{id}", async (string id, ClipUpdateRequest req, DropDbContext db) =>
        {
            var clip = await db.Clips.FindAsync(id);
            if (clip == null) return Results.NotFound(new { error = "Clip not found" });

            if (!string.IsNullOrWhiteSpace(req.Title))
            {
                clip.Title = req.Title.Trim();
            }
            clip.Description = req.Description?.Trim();
            if (!string.IsNullOrWhiteSpace(req.Visibility))
            {
                clip.Visibility = NormalizeVisibility(req.Visibility);
            }
            if (req.Tags != null)
            {
                clip.Tags = FormatTags(req.Tags);
            }

            await db.SaveChangesAsync();

            var updated = await db.Clips
                .Include(c => c.CollectionClips)
                .ThenInclude(cc => cc.Collection)
                .Include(c => c.ParentClip)
                .Include(c => c.ChildClips)
                .FirstAsync(c => c.Id == id);

            return Results.Ok(ToDto(updated));
        })
        .RequireAuthorization();

        // Admin: Cut a shorter clip from an existing clip
        group.MapPost("/{id}/trim", async (
            string id,
            ClipTrimRequest req,
            DropDbContext db,
            IMediaStorageService mediaStorage) =>
        {
            var sourceClip = await db.Clips.FindAsync(id);
            if (sourceClip == null)
            {
                return Results.NotFound(new { error = "Source clip not found" });
            }

            if (req.StartSeconds < 0)
            {
                return Results.BadRequest(new { error = "Start time cannot be negative" });
            }

            if (req.EndSeconds <= req.StartSeconds)
            {
                return Results.BadRequest(new { error = "End time must be greater than start time" });
            }

            if (sourceClip.DurationSeconds.HasValue && req.StartSeconds >= sourceClip.DurationSeconds.Value)
            {
                return Results.BadRequest(new { error = "Start time cannot exceed source clip duration" });
            }

            var newClipId = mediaStorage.GenerateId(8);
            var title = !string.IsNullOrWhiteSpace(req.Title)
                ? req.Title.Trim()
                : $"{sourceClip.Title} (Cut {Math.Round(req.StartSeconds)}s-{Math.Round(req.EndSeconds)}s)";

            var visibility = !string.IsNullOrWhiteSpace(req.Visibility)
                ? NormalizeVisibility(req.Visibility)
                : sourceClip.Visibility;

            var initialTags = req.Tags != null ? FormatTags(req.Tags) : sourceClip.Tags;

            try
            {
                var (storedFileName, thumbnailFileName, durationSeconds, fileSize) =
                    await mediaStorage.TrimVideoAsync(sourceClip.StoredFileName, newClipId, req.StartSeconds, req.EndSeconds);

                var newClip = new Clip
                {
                    Id = newClipId,
                    Title = title,
                    Description = req.Description?.Trim() ?? sourceClip.Description,
                    OriginalFileName = $"cut_{sourceClip.OriginalFileName}",
                    StoredFileName = storedFileName,
                    FileSizeBytes = fileSize,
                    ContentType = sourceClip.ContentType,
                    DurationSeconds = durationSeconds,
                    ThumbnailFileName = thumbnailFileName,
                    CreatedAtUtc = DateTime.UtcNow,
                    ViewsCount = 0,
                    Visibility = visibility,
                    Tags = initialTags,
                    ParentClipId = sourceClip.Id
                };

                db.Clips.Add(newClip);

                if (!string.IsNullOrWhiteSpace(req.CollectionId))
                {
                    var collection = await db.Collections.FindAsync(req.CollectionId);
                    if (collection != null)
                    {
                        var maxOrder = await db.CollectionClips
                            .Where(cc => cc.CollectionId == req.CollectionId)
                            .Select(cc => (int?)cc.OrderIndex)
                            .MaxAsync() ?? 0;

                        db.CollectionClips.Add(new CollectionClip
                        {
                            CollectionId = req.CollectionId,
                            ClipId = newClipId,
                            OrderIndex = maxOrder + 1,
                            AddedAtUtc = DateTime.UtcNow
                        });
                    }
                }

                await db.SaveChangesAsync();

                var created = await db.Clips
                    .Include(c => c.CollectionClips)
                    .ThenInclude(cc => cc.Collection)
                    .Include(c => c.ParentClip)
                    .Include(c => c.ChildClips)
                    .FirstAsync(c => c.Id == newClipId);

                return Results.Created($"/api/clips/{newClipId}", ToDto(created, isAdmin: true));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Trimming failed: {ex.Message}" });
            }
        })
        .RequireAuthorization();

        // Admin: AI Video Analysis via Gemini
        group.MapPost("/{id}/ai-analyze", async (
            string id,
            ClipAnalyzeAiRequest req,
            DropDbContext db,
            IMediaStorageService mediaStorage,
            IGeminiAnalysisService geminiService,
            CancellationToken cancellationToken) =>
        {
            if (!geminiService.IsConfigured)
            {
                return Results.BadRequest(new
                {
                    error = "Gemini AI is not configured. Please set GEMINI_API_KEY in the environment or .env file to enable AI analysis."
                });
            }

            var clip = await db.Clips.FindAsync([id], cancellationToken: cancellationToken);
            if (clip == null) return Results.NotFound(new { error = "Clip not found" });

            var filePath = Path.GetFullPath(mediaStorage.GetClipFilePath(clip.StoredFileName));
            if (!File.Exists(filePath))
            {
                return Results.NotFound(new { error = "Video file missing on disk" });
            }

            try
            {
                var result = await geminiService.AnalyzeClipAsync(
                    videoFilePath: filePath,
                    mimeType: clip.ContentType,
                    durationSeconds: clip.DurationSeconds,
                    promptMode: req.Mode,
                    model: req.Model,
                    cancellationToken: cancellationToken);

                // Persist the latest AI analysis result to the clip entity
                clip.AiAnalysisJson = System.Text.Json.JsonSerializer.Serialize(result);
                await db.SaveChangesAsync(cancellationToken);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"AI analysis failed: {ex.Message}" });
            }
        })
        .RequireAuthorization();

        // Admin: Check Gemini AI configuration status and available models
        group.MapGet("/ai-status", (IGeminiAnalysisService geminiService) =>
        {
            return Results.Ok(new
            {
                isConfigured = geminiService.IsConfigured,
                availableModels = geminiService.AvailableModels
            });
        })
        .RequireAuthorization();

        // Admin: Delete clip
        group.MapDelete("/{id}", async (string id, DropDbContext db, IMediaStorageService mediaStorage) =>
        {
            var clip = await db.Clips.FindAsync(id);
            if (clip == null) return Results.NotFound(new { error = "Clip not found" });

            mediaStorage.DeleteClipFiles(clip.StoredFileName, clip.ThumbnailFileName);

            db.Clips.Remove(clip);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Clip deleted successfully", id });
        })
        .RequireAuthorization();
    }

    public static ClipDto ToDto(Clip clip, bool isAdmin = true) => new(
        Id: clip.Id,
        Title: clip.Title,
        Description: clip.Description,
        OriginalFileName: clip.OriginalFileName,
        FileSizeBytes: clip.FileSizeBytes,
        ContentType: clip.ContentType,
        DurationSeconds: clip.DurationSeconds,
        HasThumbnail: !string.IsNullOrWhiteSpace(clip.ThumbnailFileName),
        VideoUrl: $"/api/clips/{clip.Id}/stream",
        ThumbnailUrl: $"/api/clips/{clip.Id}/thumbnail",
        CreatedAtUtc: clip.CreatedAtUtc,
        ViewsCount: clip.ViewsCount,
        Visibility: clip.Visibility ?? "public",
        Tags: ParseTags(clip.Tags),
        LatestAiAnalysis: ParseAiAnalysis(clip.AiAnalysisJson),
        Collections: clip.CollectionClips
            .Where(cc => cc.Collection != null)
            .OrderBy(cc => cc.OrderIndex)
            .Select(cc => new ClipCollectionSummaryDto(cc.CollectionId, cc.Collection.Name))
            .ToList(),
        ParentClip: clip.ParentClip != null && (isAdmin || clip.ParentClip.Visibility != "private")
            ? ToSummaryDto(clip.ParentClip)
            : null,
        ChildClips: (clip.ChildClips ?? [])
            .Where(c => isAdmin || c.Visibility != "private")
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => ToSummaryDto(c))
            .ToList()
    );

    public static ClipSummaryDto ToSummaryDto(Clip clip) => new(
        Id: clip.Id,
        Title: clip.Title,
        ThumbnailUrl: $"/api/clips/{clip.Id}/thumbnail",
        DurationSeconds: clip.DurationSeconds,
        Visibility: clip.Visibility ?? "public",
        CreatedAtUtc: clip.CreatedAtUtc
    );
}
