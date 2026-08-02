namespace AnimeBackend.Application.Ai;

// Port to the LLM provider. Kept minimal on purpose: plain text in, text (or
// JSON constrained by a schema) out, plus a streaming variant for the chat
// panel. The implementation decides model specifics; callers only choose an
// effort level and, optionally, a JSON schema for structured output.
public interface IAiChat
{
    // False when no API key is configured — endpoints degrade to an honest
    // "assistant not connected" answer instead of failing mid-request.
    bool IsConfigured { get; }

    Task<AiCompletion> CompleteAsync(AiChatRequest request, CancellationToken ct);

    IAsyncEnumerable<string> StreamAsync(AiChatRequest request, CancellationToken ct);
}

public sealed record AiChatRequest
{
    public required string Model { get; init; }
    public string? System { get; init; }
    public required IReadOnlyList<AiChatMessage> Messages { get; init; }
    public bool DeepThink { get; init; }
    public int MaxTokens { get; init; } = 4096;

    // When set, the completion is constrained to this JSON schema and the
    // returned text is guaranteed-parseable JSON.
    public string? JsonSchema { get; init; }
}

public sealed record AiChatMessage(string Role, string Content);

public sealed record AiCompletion(string Text, long InputTokens, long OutputTokens, bool Refused);
