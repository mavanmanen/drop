using Drop.Api.Data;
using Drop.Api.Models;

namespace Drop.Api.Services;

public class IngestWatcherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediaStorageService _mediaStorage;
    private readonly ILogger<IngestWatcherService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mkv", ".mov", ".avi"
    };

    private readonly Dictionary<string, (long Size, DateTime FirstSeen)> _candidateFiles = new();

    public IngestWatcherService(
        IServiceProvider serviceProvider,
        IMediaStorageService mediaStorage,
        ILogger<IngestWatcherService> logger)
    {
        _serviceProvider = serviceProvider;
        _mediaStorage = mediaStorage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ingest folder watcher started for: {IngestDir}", _mediaStorage.IngestDirectory);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessIngestFolderAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred during ingest directory scan.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Ingest folder watcher stopped.");
    }

    private async Task ProcessIngestFolderAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_mediaStorage.IngestDirectory))
        {
            Directory.CreateDirectory(_mediaStorage.IngestDirectory);
            return;
        }

        var files = Directory.GetFiles(_mediaStorage.IngestDirectory, "*.*", SearchOption.TopDirectoryOnly);

        foreach (var filePath in files)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var ext = Path.GetExtension(filePath);
            if (!AllowedExtensions.Contains(ext))
            {
                continue;
            }

            var fileName = Path.GetFileName(filePath);

            // Ignore hidden/temporary files
            if (fileName.StartsWith('.') || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    continue;
                }

                // Check file size stability to ensure copy/render is complete
                if (!_candidateFiles.TryGetValue(filePath, out var candidateState))
                {
                    _candidateFiles[filePath] = (fileInfo.Length, DateTime.UtcNow);
                    _logger.LogInformation("New clip candidate detected in ingest folder: {FileName}. Verifying write completion...", fileName);
                    continue;
                }

                if (candidateState.Size != fileInfo.Length)
                {
                    _candidateFiles[filePath] = (fileInfo.Length, DateTime.UtcNow);
                    _logger.LogDebug("Clip {FileName} is still being written ({Bytes} bytes). Waiting...", fileName, fileInfo.Length);
                    continue;
                }

                // Check if file is unlocked and can be moved
                if (!IsFileReady(filePath))
                {
                    _logger.LogDebug("Clip {FileName} is locked by another process. Waiting...", fileName);
                    continue;
                }

                // Ready to ingest!
                _candidateFiles.Remove(filePath);
                await IngestFileAsync(filePath, fileName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inspect ingest candidate: {FilePath}", filePath);
            }
        }

        // Clean up candidates that no longer exist
        var staleKeys = _candidateFiles.Keys.Where(k => !File.Exists(k)).ToList();
        foreach (var stale in staleKeys)
        {
            _candidateFiles.Remove(stale);
        }
    }

    private static bool IsFileReady(string filePath)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return fileStream.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task IngestFileAsync(string sourcePath, string originalFileName, CancellationToken cancellationToken)
    {
        var clipId = _mediaStorage.GenerateId(8);
        var baseTitle = Path.GetFileNameWithoutExtension(originalFileName).Replace('_', ' ').Trim();
        if (string.IsNullOrWhiteSpace(baseTitle))
        {
            baseTitle = $"Clip {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
        }

        _logger.LogInformation("Ingesting file '{OriginalFileName}' with Clip ID '{ClipId}'...", originalFileName, clipId);

        var (storedFileName, thumbnailFileName, durationSeconds, fileSize) =
            await _mediaStorage.ProcessAndStoreVideoAsync(sourcePath, clipId, originalFileName, isMove: true);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DropDbContext>();

        var clip = new Clip
        {
            Id = clipId,
            Title = baseTitle,
            Description = null,
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            FileSizeBytes = fileSize,
            ContentType = _mediaStorage.GetContentType(originalFileName),
            DurationSeconds = durationSeconds,
            ThumbnailFileName = thumbnailFileName,
            CreatedAtUtc = DateTime.UtcNow,
            ViewsCount = 0
        };

        db.Clips.Add(clip);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully ingested clip '{Title}' ({ClipId}, {FileSize} bytes, duration: {Duration}s)",
            clip.Title, clip.Id, clip.FileSizeBytes, clip.DurationSeconds ?? 0);
    }
}
