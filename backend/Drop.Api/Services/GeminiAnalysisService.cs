using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Drop.Api.Services;

#region Gemini REST DTOs
public class GeminiFileRef
{
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public string? MimeType { get; set; }
    public string? SizeBytes { get; set; }
    public string? CreateTime { get; set; }
    public string? UpdateTime { get; set; }
    public string? ExpirationTime { get; set; }
    public string? Sha256Hash { get; set; }
    public string? Uri { get; set; }
    public string? State { get; set; } // "PROCESSING", "ACTIVE", "FAILED"
}

public class GeminiUploadFileResponse
{
    public GeminiFileRef? File { get; set; }
}

public class GeminiFileStatusResponse : GeminiFileRef
{
}

public class GeminiGenerateContentRequest
{
    public List<GeminiContent>? Contents { get; set; }
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

public class GeminiContent
{
    public string? Role { get; set; }
    public List<GeminiPart>? Parts { get; set; }
}

public class GeminiPart
{
    public string? Text { get; set; }
    public GeminiFileData? FileData { get; set; }
}

public class GeminiFileData
{
    public string? MimeType { get; set; }
    public string? FileUri { get; set; }
}

public class GeminiGenerationConfig
{
    public string? ResponseMimeType { get; set; }
    public float? Temperature { get; set; }
}

public class GeminiGenerateContentResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }
}

public class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
    public string? FinishReason { get; set; }
}
#endregion

public class GeminiAnalysisService : IGeminiAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiAnalysisService> _logger;
    private readonly string? _apiKey;
    private readonly string _defaultModel;

    public static readonly string[] SupportedModels =
    [
        "gemini-3.8-flash",
        "gemini-3.8-pro",
        "gemini-3.5-flash-lite",
        "gemini-3.1-flash-lite",
        "gemini-2.5-flash",
        "gemini-2.5-flash-lite",
        "gemini-2.5-pro"
    ];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);
    public IReadOnlyList<string> AvailableModels => SupportedModels;

    public GeminiAnalysisService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiAnalysisService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["GEMINI_API_KEY"]
                  ?? configuration["DROP_GEMINI_API_KEY"]
                  ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        _defaultModel = configuration["GEMINI_MODEL"] ?? "gemini-3.5-flash-lite";

        if (!IsConfigured)
        {
            _logger.LogInformation("GeminiAnalysisService: GEMINI_API_KEY is not set. AI analysis features will be disabled.");
        }
        else
        {
            _logger.LogInformation("GeminiAnalysisService: Initialized (default model: {Model}).", _defaultModel);
        }
    }

    public async Task<AiClipAnalysisResult> AnalyzeClipAsync(
        string videoFilePath,
        string mimeType,
        double? durationSeconds,
        string promptMode,
        string? originalFileName = null,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Gemini API key is not configured. Set the GEMINI_API_KEY environment variable to enable AI clip analysis.");
        }

        var targetModel = !string.IsNullOrWhiteSpace(model) ? model.Trim() : _defaultModel;

        if (!File.Exists(videoFilePath))
        {
            throw new FileNotFoundException($"Video file not found at '{videoFilePath}'.");
        }

        var mode = promptMode.Trim().ToLowerInvariant();
        if (mode is not "tags" and not "subclips" and not "both")
        {
            mode = "both";
        }

        GeminiFileRef? uploadedFile = null;

        try
        {
            var logFileName = !string.IsNullOrWhiteSpace(originalFileName) ? originalFileName : Path.GetFileName(videoFilePath);
            _logger.LogInformation("Uploading video '{File}' to Gemini Files API...", logFileName);
            uploadedFile = await UploadVideoFileAsync(videoFilePath, mimeType, originalFileName, cancellationToken);

            var fileName = uploadedFile.Name ?? throw new InvalidOperationException("Gemini upload failed to return a file name.");

            // Poll for processing completion
            var pollInterval = TimeSpan.FromSeconds(2);
            var maxAttempts = 30;
            var attempts = 0;

            while (attempts < maxAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var status = await GetFileStatusAsync(fileName, cancellationToken);
                if (string.Equals(status.State, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    uploadedFile = status;
                    break;
                }
                if (string.Equals(status.State, "FAILED", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Gemini file processing failed for '{fileName}'.");
                }

                _logger.LogInformation("Waiting for Gemini video processing (State: {State})...", status.State);
                await Task.Delay(pollInterval, cancellationToken);
                attempts++;
            }

            var promptText = BuildPrompt(mode, durationSeconds, originalFileName);
            _logger.LogInformation("Sending video analysis request to Gemini model {Model} (mode: {Mode}, filename context: {Filename})...",
                targetModel, mode, originalFileName ?? "(none)");

            var rawJson = await GenerateContentAsync(targetModel, uploadedFile.Uri!, uploadedFile.MimeType ?? mimeType, promptText, cancellationToken);

            return ParseResult(rawJson, durationSeconds, mode);
        }
        finally
        {
            if (uploadedFile != null && !string.IsNullOrWhiteSpace(uploadedFile.Name))
            {
                try
                {
                    _logger.LogInformation("Cleaning up uploaded file '{File}' from Gemini Files API...", uploadedFile.Name);
                    await DeleteFileAsync(uploadedFile.Name, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file '{File}' from Gemini Files API.", uploadedFile.Name);
                }
            }
        }
    }

    private async Task<GeminiFileRef> UploadVideoFileAsync(string filePath, string mimeType, string? displayName, CancellationToken ct)
    {
        // 1. Resumable / Initial upload request to https://generativelanguage.googleapis.com/upload/v1beta/files
        var uploadUri = $"https://generativelanguage.googleapis.com/upload/v1beta/files?key={_apiKey}";
        var fileInfo = new FileInfo(filePath);
        var uploadDisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : Path.GetFileName(filePath);

        using var initReq = new HttpRequestMessage(HttpMethod.Post, uploadUri);
        initReq.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        initReq.Headers.Add("X-Goog-Upload-Command", "start");
        initReq.Headers.Add("X-Goog-Upload-Header-Content-Length", fileInfo.Length.ToString());
        initReq.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);

        var metadataJson = $"{{\"file\": {{\"display_name\": {JsonSerializer.Serialize(uploadDisplayName)}}}}}";
        initReq.Content = new StringContent(metadataJson, Encoding.UTF8, "application/json");

        using var initResp = await _httpClient.SendAsync(initReq, ct);
        initResp.EnsureSuccessStatusCode();

        if (!initResp.Headers.TryGetValues("X-Goog-Upload-URL", out var uploadUrlValues))
        {
            throw new InvalidOperationException("Gemini Files API upload did not return an upload URL.");
        }

        var resumableUrl = uploadUrlValues.First();

        // 2. Upload file bytes
        await using var fileStream = File.OpenRead(filePath);
        using var chunkReq = new HttpRequestMessage(HttpMethod.Post, resumableUrl);
        chunkReq.Headers.Add("X-Goog-Upload-Offset", "0");
        chunkReq.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
        chunkReq.Content = new StreamContent(fileStream);
        chunkReq.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        chunkReq.Content.Headers.ContentLength = fileInfo.Length;

        using var chunkResp = await _httpClient.SendAsync(chunkReq, ct);
        chunkResp.EnsureSuccessStatusCode();

        var respStream = await chunkResp.Content.ReadAsStreamAsync(ct);
        var uploadResult = await JsonSerializer.DeserializeAsync(
            respStream,
            AppJsonSerializerContext.Default.GeminiUploadFileResponse,
            ct);

        return uploadResult?.File ?? throw new InvalidOperationException("Failed to parse Gemini upload response.");
    }

    private async Task<GeminiFileStatusResponse> GetFileStatusAsync(string fileName, CancellationToken ct)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/{fileName}?key={_apiKey}";
        using var resp = await _httpClient.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var status = await JsonSerializer.DeserializeAsync(
            stream,
            AppJsonSerializerContext.Default.GeminiFileStatusResponse,
            ct);

        return status ?? throw new InvalidOperationException($"Failed to deserialize status for file '{fileName}'.");
    }

    private async Task<string> GenerateContentAsync(
        string model,
        string fileUri,
        string mimeType,
        string promptText,
        CancellationToken ct)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_apiKey}";

        var requestBody = new GeminiGenerateContentRequest
        {
            Contents =
            [
                new GeminiContent
                {
                    Parts =
                    [
                        new GeminiPart
                        {
                            FileData = new GeminiFileData
                            {
                                FileUri = fileUri,
                                MimeType = mimeType
                            }
                        },
                        new GeminiPart { Text = promptText }
                    ]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseMimeType = "application/json",
                Temperature = 0.2f
            }
        };

        var jsonString = JsonSerializer.Serialize(requestBody, AppJsonSerializerContext.Default.GeminiGenerateContentRequest);
        using var postReq = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(postReq, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var genResult = await JsonSerializer.DeserializeAsync(
            stream,
            AppJsonSerializerContext.Default.GeminiGenerateContentResponse,
            ct);

        var rawJson = genResult?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new InvalidOperationException("Gemini returned an empty response.");
        }

        return rawJson;
    }

    private async Task DeleteFileAsync(string fileName, CancellationToken ct)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/{fileName}?key={_apiKey}";
        using var resp = await _httpClient.DeleteAsync(url, ct);
        // Best-effort cleanup
    }

    public static string BuildPrompt(string mode, double? durationSeconds, string? originalFileName = null)
    {
        var contextBuilder = new StringBuilder();
        if (durationSeconds.HasValue)
        {
            contextBuilder.Append($"The clip has a duration of approximately {durationSeconds.Value.ToString("F1", CultureInfo.InvariantCulture)} seconds. ");
        }

        if (!string.IsNullOrWhiteSpace(originalFileName))
        {
            contextBuilder.Append($"Original recording filename: \"{originalFileName.Trim()}\". Note: The recording filename often contains the game title, map name, or recording context; use this context to accurately recognize the game and generate related tags. ");
        }

        var contextInfo = contextBuilder.ToString().Trim();

        return mode switch
        {
            "tags" => $@"Analyze this video game recording. {contextInfo}
Task: Generate between 3 to 8 concise, relevant gaming tags that describe the game title, gameplay moments (e.g. clutch, ace, fail, snipe, win), hero/agent/weapon, or game mode. Use the filename context if available to accurately identify the specific game and its terminology.
Format your output as a valid JSON object matching:
{{
  ""tags"": [""tag1"", ""tag2""],
  ""subClips"": [],
  ""summary"": ""brief summary""
}}",

            "subclips" => $@"Analyze this video game recording. {contextInfo}
Task: Identify the most exciting, funny, or noteworthy highlight moments and suggest short sub-clips to cut from it.
Each suggested sub-clip must have an exciting title, brief description, startSeconds, and endSeconds strictly within the video duration boundaries.
Keep cut moments focused (typically between 5 and 30 seconds long).
Format your output as a valid JSON object matching:
{{
  ""tags"": [],
  ""subClips"": [
    {{ ""title"": ""Highlight"", ""description"": ""description"", ""startSeconds"": 1.0, ""endSeconds"": 15.0 }}
  ],
  ""summary"": ""brief summary""
}}",

            _ => $@"Analyze this video game recording. {contextInfo}
Task:
1. Generate between 3 to 8 concise, relevant gaming tags (game title, moment type like clutch/funny/ace, character/weapon). Use the filename context if available to accurately identify the specific game and its terminology.
2. Identify the most exciting or noteworthy highlight moments and suggest short sub-clips to cut from it with accurate startSeconds and endSeconds.
3. Provide a one-sentence summary of the clip.
Format your output as a valid JSON object matching:
{{
  ""tags"": [""tag1"", ""tag2""],
  ""subClips"": [
    {{ ""title"": ""Highlight"", ""description"": ""description"", ""startSeconds"": 1.0, ""endSeconds"": 15.0 }}
  ],
  ""summary"": ""brief summary""
}}"
        };
    }

    private static AiClipAnalysisResult ParseResult(string rawJson, double? durationSeconds, string mode)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var tags = new List<string>();
        if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tagsEl.EnumerateArray())
            {
                var s = t.GetString()?.Trim().TrimStart('#');
                if (!string.IsNullOrWhiteSpace(s) && !tags.Contains(s, StringComparer.OrdinalIgnoreCase))
                {
                    tags.Add(s);
                }
            }
        }

        var subClips = new List<AiSubClipSuggestion>();
        if (root.TryGetProperty("subClips", out var subClipsEl) && subClipsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var sc in subClipsEl.EnumerateArray())
            {
                var title = sc.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "Highlight" : "Highlight";
                var desc = sc.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "";

                double start = 0;
                if (sc.TryGetProperty("startSeconds", out var sEl) && sEl.TryGetDouble(out var sVal))
                {
                    start = Math.Max(0, sVal);
                }

                double end = start + 10;
                if (sc.TryGetProperty("endSeconds", out var eEl) && eEl.TryGetDouble(out var eVal))
                {
                    end = Math.Max(start + 0.5, eVal);
                }

                if (durationSeconds.HasValue)
                {
                    start = Math.Min(start, durationSeconds.Value);
                    end = Math.Min(end, durationSeconds.Value);
                    if (end <= start)
                    {
                        end = Math.Min(durationSeconds.Value, start + 5);
                    }
                }

                subClips.Add(new AiSubClipSuggestion(
                    Title: title,
                    Description: desc,
                    StartSeconds: Math.Round(start, 1),
                    EndSeconds: Math.Round(end, 1)
                ));
            }
        }

        string? summary = null;
        if (root.TryGetProperty("summary", out var sumEl) && sumEl.ValueKind == JsonValueKind.String)
        {
            summary = sumEl.GetString();
        }

        return new AiClipAnalysisResult(tags, subClips, summary);
    }
}
