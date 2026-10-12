using System.Text.Json.Serialization;
using Drop.Api.Endpoints;
using Drop.Api.Services;

namespace Drop.Api;

public record SimpleMessageResponse(string Message, string? Id = null);
public record ErrorResponse(string Error);
public record LoginSuccessResponse(string Token, string Username, bool IsAuthenticated);
public record AiStatusResponse(bool IsConfigured, IReadOnlyList<string> AvailableModels);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true
)]
[JsonSerializable(typeof(AuthEndpoints.LoginRequest))]
[JsonSerializable(typeof(AuthEndpoints.AuthStatusResponse))]
[JsonSerializable(typeof(LoginSuccessResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(SimpleMessageResponse))]
[JsonSerializable(typeof(AiStatusResponse))]

[JsonSerializable(typeof(ClipEndpoints.ClipDto))]
[JsonSerializable(typeof(ClipEndpoints.ClipSummaryDto))]
[JsonSerializable(typeof(ClipEndpoints.ClipCollectionSummaryDto))]
[JsonSerializable(typeof(ClipEndpoints.ClipUpdateRequest))]
[JsonSerializable(typeof(ClipEndpoints.ClipTrimRequest))]
[JsonSerializable(typeof(ClipEndpoints.ClipAnalyzeAiRequest))]
[JsonSerializable(typeof(ClipEndpoints.ClipLikeRequest))]
[JsonSerializable(typeof(ClipEndpoints.ClipLikeResponse))]
[JsonSerializable(typeof(ClipEndpoints.ClipListResponse))]
[JsonSerializable(typeof(List<ClipEndpoints.ClipDto>))]
[JsonSerializable(typeof(List<string>))]

[JsonSerializable(typeof(CollectionEndpoints.CollectionDto))]
[JsonSerializable(typeof(CollectionEndpoints.CreateCollectionRequest))]
[JsonSerializable(typeof(CollectionEndpoints.UpdateCollectionRequest))]
[JsonSerializable(typeof(CollectionEndpoints.AddClipToCollectionRequest))]
[JsonSerializable(typeof(CollectionEndpoints.ReorderClipsRequest))]
[JsonSerializable(typeof(List<CollectionEndpoints.CollectionDto>))]

[JsonSerializable(typeof(AiClipAnalysisResult))]
[JsonSerializable(typeof(AiSubClipSuggestion))]
[JsonSerializable(typeof(List<AiSubClipSuggestion>))]

// Gemini REST models
[JsonSerializable(typeof(GeminiUploadFileResponse))]
[JsonSerializable(typeof(GeminiFileStatusResponse))]
[JsonSerializable(typeof(GeminiGenerateContentRequest))]
[JsonSerializable(typeof(GeminiGenerateContentResponse))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
