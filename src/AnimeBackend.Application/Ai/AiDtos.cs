namespace AnimeBackend.Application.Ai;

// Wire shapes for the /ai group, mirroring the frontend's aiApi.ts.

public sealed record AiUsageDto(long InputTokens, long OutputTokens);

public sealed record AiChatApiMessage(string? Role, string? Content);

public sealed record AiContextDto(IReadOnlyList<string>? WatchedTitles, IReadOnlyList<string>? Tags);

public sealed record AiChatApiRequest(
    IReadOnlyList<AiChatApiMessage>? Messages,
    string? Model,
    double? Temperature,
    bool? DeepThink,
    AiContextDto? Context);

public sealed record AiChatResponseDto(string Reply, AiUsageDto Usage);

public sealed record AiParaphraseApiRequest(
    string? Text,
    string? Style,
    string? Model,
    double? Temperature,
    bool? DeepThink);

public sealed record AiParaphraseResponseDto(string Result, AiUsageDto Usage);

public sealed record AiRecommendationsApiRequest(
    string? Prompt,
    string? Mood,
    string? Model,
    double? Temperature,
    bool? DeepThink,
    AiContextDto? Context);

public sealed record AiRecommendationDto(string MediaId, string Title, string Reason, double Score);

public sealed record AiRecommendationsResponseDto(
    IReadOnlyList<AiRecommendationDto> Items, AiUsageDto Usage);

public sealed record AiProcessVoiceApiRequest(string? Text);

public sealed record AiProcessVoiceResponseDto(string ProcessedText, IReadOnlyList<string> Suggestions);
