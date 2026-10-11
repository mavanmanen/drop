namespace Drop.Api.Services;

public record AiSubClipSuggestion(
    string Title,
    string Description,
    double StartSeconds,
    double EndSeconds
);

public record AiClipAnalysisResult(
    List<string> Tags,
    List<AiSubClipSuggestion> SubClips,
    string? Summary
);

public interface IGeminiAnalysisService
{
    bool IsConfigured { get; }
    Task<AiClipAnalysisResult> AnalyzeClipAsync(
        string videoFilePath,
        string mimeType,
        double? durationSeconds,
        string promptMode, // "tags", "subclips", or "both"
        CancellationToken cancellationToken = default);
}
