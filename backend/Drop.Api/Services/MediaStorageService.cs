using System.Diagnostics;
using System.Security.Cryptography;

namespace Drop.Api.Services;

public interface IMediaStorageService
{
    string IngestDirectory { get; }
    string ClipsDirectory { get; }
    string ThumbnailsDirectory { get; }

    string GenerateId(int length = 8);
    string GetClipFilePath(string storedFileName);
    string? GetThumbnailFilePath(string? thumbnailFileName);
    Task<(string storedFileName, string? thumbnailFileName, double? durationSeconds, long fileSize)> ProcessAndStoreVideoAsync(
        string sourceFilePath,
        string clipId,
        string originalFileName,
        bool isMove = true);
    Task<(string storedFileName, string? thumbnailFileName, double? durationSeconds, long fileSize)> SaveUploadedVideoAsync(
        IFormFile file,
        string clipId);
    void DeleteClipFiles(string storedFileName, string? thumbnailFileName);
    string GetContentType(string fileName);
}

public class MediaStorageService : IMediaStorageService
{
    private readonly ILogger<MediaStorageService> _logger;
    private readonly string _ingestDirectory;
    private readonly string _clipsDirectory;
    private readonly string _thumbnailsDirectory;

    public string IngestDirectory => _ingestDirectory;
    public string ClipsDirectory => _clipsDirectory;
    public string ThumbnailsDirectory => _thumbnailsDirectory;

    public MediaStorageService(IConfiguration configuration, ILogger<MediaStorageService> logger)
    {
        _logger = logger;

        var baseDataDir = configuration["DROP_DATA_DIR"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        baseDataDir = Path.GetFullPath(baseDataDir);
        _ingestDirectory = Path.GetFullPath(configuration["DROP_INGEST_PATH"] ?? Path.Combine(baseDataDir, "ingest"));
        _clipsDirectory = Path.GetFullPath(configuration["DROP_STORAGE_PATH"] ?? Path.Combine(baseDataDir, "clips"));
        _thumbnailsDirectory = Path.GetFullPath(Path.Combine(baseDataDir, "thumbnails"));

        Directory.CreateDirectory(_ingestDirectory);
        Directory.CreateDirectory(_clipsDirectory);
        Directory.CreateDirectory(_thumbnailsDirectory);

        _logger.LogInformation("Storage initialized. Ingest: {Ingest}, Clips: {Clips}, Thumbnails: {Thumbs}",
            _ingestDirectory, _clipsDirectory, _thumbnailsDirectory);
    }

    public string GenerateId(int length = 8)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return RandomNumberGenerator.GetString(chars, length);
    }

    public string GetClipFilePath(string storedFileName) => Path.Combine(_clipsDirectory, storedFileName);

    public string? GetThumbnailFilePath(string? thumbnailFileName)
    {
        if (string.IsNullOrWhiteSpace(thumbnailFileName)) return null;
        var path = Path.Combine(_thumbnailsDirectory, thumbnailFileName);
        return File.Exists(path) ? path : null;
    }

    public string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            _ => "application/octet-stream"
        };
    }

    public async Task<(string storedFileName, string? thumbnailFileName, double? durationSeconds, long fileSize)> ProcessAndStoreVideoAsync(
        string sourceFilePath,
        string clipId,
        string originalFileName,
        bool isMove = true)
    {
        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(ext)) ext = ".mp4";
        var storedFileName = $"{clipId}{ext}";
        var destinationPath = Path.Combine(_clipsDirectory, storedFileName);

        if (isMove)
        {
            File.Move(sourceFilePath, destinationPath, overwrite: true);
        }
        else
        {
            File.Copy(sourceFilePath, destinationPath, overwrite: true);
        }

        var fileInfo = new FileInfo(destinationPath);
        var fileSize = fileInfo.Length;

        var thumbnailFileName = await TryGenerateThumbnailAsync(destinationPath, clipId);
        var durationSeconds = await TryExtractDurationAsync(destinationPath);

        return (storedFileName, thumbnailFileName, durationSeconds, fileSize);
    }

    public async Task<(string storedFileName, string? thumbnailFileName, double? durationSeconds, long fileSize)> SaveUploadedVideoAsync(
        IFormFile file,
        string clipId)
    {
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".mp4";
        var storedFileName = $"{clipId}{ext}";
        var destinationPath = Path.Combine(_clipsDirectory, storedFileName);

        await using (var stream = new FileStream(destinationPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var fileInfo = new FileInfo(destinationPath);
        var fileSize = fileInfo.Length;

        var thumbnailFileName = await TryGenerateThumbnailAsync(destinationPath, clipId);
        var durationSeconds = await TryExtractDurationAsync(destinationPath);

        return (storedFileName, thumbnailFileName, durationSeconds, fileSize);
    }

    public void DeleteClipFiles(string storedFileName, string? thumbnailFileName)
    {
        try
        {
            var clipPath = Path.Combine(_clipsDirectory, storedFileName);
            if (File.Exists(clipPath)) File.Delete(clipPath);

            if (!string.IsNullOrWhiteSpace(thumbnailFileName))
            {
                var thumbPath = Path.Combine(_thumbnailsDirectory, thumbnailFileName);
                if (File.Exists(thumbPath)) File.Delete(thumbPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting clip files for {StoredFileName}", storedFileName);
        }
    }

    private async Task<string?> TryGenerateThumbnailAsync(string videoPath, string clipId)
    {
        var thumbFileName = $"{clipId}.jpg";
        var thumbPath = Path.Combine(_thumbnailsDirectory, thumbFileName);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -ss 00:00:01 -i \"{videoPath}\" -vframes 1 -q:v 2 \"{thumbPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var stderrTask = process.StandardError.ReadToEndAsync();
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                await Task.WhenAll(stderrTask, stdoutTask, process.WaitForExitAsync());
                if (process.ExitCode == 0 && File.Exists(thumbPath))
                {
                    return thumbFileName;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate thumbnail via ffmpeg for {VideoPath}", videoPath);
        }

        return null;
    }

    private async Task<double?> TryExtractDurationAsync(string videoPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync());
                var output = await stdoutTask;
                if (process.ExitCode == 0 && double.TryParse(output.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var duration))
                {
                    return duration;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract duration via ffprobe for {VideoPath}", videoPath);
        }

        return null;
    }
}
