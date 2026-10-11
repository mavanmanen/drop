using System.Text.Json;
using System.Text.Json.Nodes;
using Google.GenAI;
using Google.GenAI.Types;

namespace Drop.Api.Services;

public class GeminiAnalysisService : IGeminiAnalysisService
{
    private readonly ILogger<GeminiAnalysisService> _logger;
    private readonly string? _apiKey;
    private readonly string _model;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public GeminiAnalysisService(IConfiguration configuration, ILogger<GeminiAnalysisService> logger)
    {
        _logger = logger;
        _apiKey = configuration["GEMINI_API_KEY"]
                  ?? configuration["DROP_GEMINI_API_KEY"]
                  ?? System.Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        _model = configuration["GEMINI_MODEL"] ?? "gemini-3.8-flash";

        if (!IsConfigured)
        {
            _logger.LogInformation("GeminiAnalysisService: GEMINI_API_KEY is not set. AI analysis features will be disabled.");
        }
        else
        {
            _logger.LogInformation("GeminiAnalysisService: Initialized with model {Model}.", _model);
        }
    }

    public async Task<AiClipAnalysisResult> AnalyzeClipAsync(
        string videoFilePath,
        string mimeType,
        double? durationSeconds,
        string promptMode,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Gemini API key is not configured. Set the GEMINI_API_KEY environment variable to enable AI clip analysis.");
        }

        if (!System.IO.File.Exists(videoFilePath))
        {
            throw new FileNotFoundException($"Video file not found at '{videoFilePath}'.");
        }

        var client = new Client(apiKey: _apiKey);

        var mode = promptMode.Trim().ToLowerInvariant();
        if (mode is not "tags" and not "subclips" and not "both")
        {
            mode = "both";
        }

        Google.GenAI.Types.File? uploadedFile = null;

        try
        {
            _logger.LogInformation("Uploading video '{File}' to Gemini Files API...", Path.GetFileName(videoFilePath));
            uploadedFile = await client.Files.UploadAsync(
                filePath: videoFilePath,
                config: new UploadFileConfig
                {
                    MimeType = mimeType,
                    DisplayName = Path.GetFileName(videoFilePath)
                }
            );

            // Wait for file processing if necessary
            var fileName = uploadedFile.Name ?? throw new InvalidOperationException("Gemini upload failed to return a file name.");
            var pollInterval = TimeSpan.FromSeconds(2);
            var maxAttempts = 30;
            var attempts = 0;

            while (attempts < maxAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileState = await client.Files.GetAsync(name: fileName);
                if (fileState.State == FileState.Active)
                {
                    uploadedFile = fileState;
                    break;
                }
                if (fileState.State == FileState.Failed)
                {
                    throw new InvalidOperationException($"Gemini file processing failed for '{fileName}'.");
                }

                _logger.LogInformation("Waiting for Gemini video processing (State: {State})...", fileState.State);
                await Task.Delay(pollInterval, cancellationToken);
                attempts++;
            }

            var promptText = BuildPrompt(mode, durationSeconds);
            _logger.LogInformation("Sending video analysis request to Gemini model {Model} (mode: {Mode})...", _model, mode);

            var schemaString = """
            {
              "type": "object",
              "properties": {
                "tags": {
                  "type": "array",
                  "items": { "type": "string" },
                  "description": "Short relevant gaming tags (game title, clutch, funny, ace, weapon, hero, map, etc.)"
                },
                "subClips": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "title": { "type": "string", "description": "Concise exciting title for the highlight moment" },
                      "description": { "type": "string", "description": "Brief description of the action" },
                      "startSeconds": { "type": "number", "description": "Start timestamp in seconds" },
                      "endSeconds": { "type": "number", "description": "End timestamp in seconds" }
                    },
                    "required": ["title", "startSeconds", "endSeconds"]
                  },
                  "description": "Suggested sub-clip highlights cut from this video"
                },
                "summary": {
                  "type": "string",
                  "description": "Brief summary of what happened in the clip"
                }
              },
              "required": ["tags", "subClips"]
            }
            """;

            var config = new GenerateContentConfig
            {
                ResponseMimeType = "application/json",
                ResponseJsonSchema = JsonNode.Parse(schemaString),
                Temperature = 0.2f
            };

            var contentParts = new List<Part>
            {
                new Part
                {
                    FileData = new FileData
                    {
                        FileUri = uploadedFile.Uri,
                        MimeType = uploadedFile.MimeType
                    }
                },
                new Part { Text = promptText }
            };

            var response = await client.Models.GenerateContentAsync(
                model: _model,
                contents: new Content { Parts = contentParts },
                config: config
            );

            var rawJson = response.Candidates?[0]?.Content?.Parts?[0]?.Text;
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                throw new InvalidOperationException("Gemini returned an empty response.");
            }

            return ParseResult(rawJson, durationSeconds, mode);
        }
        finally
        {
            if (uploadedFile != null && !string.IsNullOrWhiteSpace(uploadedFile.Name))
            {
                try
                {
                    _logger.LogInformation("Cleaning up uploaded file '{File}' from Gemini Files API...", uploadedFile.Name);
                    await client.Files.DeleteAsync(name: uploadedFile.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file '{File}' from Gemini Files API.", uploadedFile.Name);
                }
            }
        }
    }

    private static string BuildPrompt(string mode, double? durationSeconds)
    {
        var durationInfo = durationSeconds.HasValue
            ? $"The clip has a duration of approximately {durationSeconds.Value:F1} seconds."
            : "";

        return mode switch
        {
            "tags" => $@"Analyze this video game recording. {durationInfo}
Task: Generate between 3 to 8 concise, relevant gaming tags that describe the game title, gameplay moments (e.g. clutch, ace, fail, snipe, win), hero/agent/weapon, or game mode.
Leave subClips as an empty array.",

            "subclips" => $@"Analyze this video game recording. {durationInfo}
Task: Identify the most exciting, funny, or noteworthy highlight moments and suggest short sub-clips to cut from it.
Each suggested sub-clip must have an exciting title, brief description, startSeconds, and endSeconds strictly within the video duration boundaries.
Keep cut moments focused (typically between 5 and 30 seconds long).
Leave tags as an empty array.",

            _ => $@"Analyze this video game recording. {durationInfo}
Task:
1. Generate between 3 to 8 concise, relevant gaming tags (game title, moment type like clutch/funny/ace, character/weapon).
2. Identify the most exciting or noteworthy highlight moments and suggest short sub-clips to cut from it with accurate startSeconds and endSeconds.
3. Provide a one-sentence summary of the clip."
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
