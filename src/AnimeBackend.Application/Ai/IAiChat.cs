namespace AnimeBackend.Application.Ai;

// Port to the LLM router. Deliberately dumb: one round trip per call, no loop.
// The agentic loop (execute tools, feed results back, repeat) lives in the
// Application layer so it stays provider-independent and testable.
public interface IAiChat
{
    // False when no API key is configured — endpoints then answer with an
    // honest "not connected" instead of failing mid-request.
    bool IsConfigured { get; }

    Task<AiCompletion> CompleteAsync(AiChatRequest request, CancellationToken ct);

    IAsyncEnumerable<AiStreamEvent> StreamAsync(AiChatRequest request, CancellationToken ct);

    Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken ct);
}

public sealed record AiChatRequest
{
    public required string Model { get; init; }
    public string? System { get; init; }
    public required IReadOnlyList<AiChatMessage> Messages { get; init; }
    public int MaxTokens { get; init; } = 4096;

    // Dropped when the selected model doesn't list temperature as supported.
    public double? Temperature { get; init; }

    public IReadOnlyList<AiToolDefinition>? Tools { get; init; }

    // "required" makes the model call a tool instead of answering from memory;
    // null leaves the choice to it.
    public string? ToolChoice { get; init; }

    // Each round is a full generation, and reasoning models take tens of
    // seconds per round — this is the latency budget, not just a safety net.
    public int MaxToolRounds { get; init; } = 3;

    // Constrains the reply to this JSON schema. Not combined with tools —
    // gathering and formatting run as separate phases.
    public string? JsonSchema { get; init; }
}

// `role` follows the OpenAI convention: system | user | assistant | tool.
public sealed record AiChatMessage
{
    public required string Role { get; init; }
    public string? Content { get; init; }

    // Set on an assistant turn that asked for tools.
    public IReadOnlyList<AiToolCall>? ToolCalls { get; init; }

    // Set on a tool turn, matching the call it answers.
    public string? ToolCallId { get; init; }

    public static AiChatMessage User(string content) => new() { Role = "user", Content = content };

    public static AiChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };
}

public sealed record AiToolCall(string Id, string Name, string ArgumentsJson);

public sealed record AiToolDefinition(string Name, string Description, string ParametersJson);

public sealed record AiCompletion
{
    public string Text { get; init; } = "";
    public IReadOnlyList<AiToolCall> ToolCalls { get; init; } = [];
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public string? FinishReason { get; init; }

    public bool WantsTools => ToolCalls.Count > 0;
}

// Streaming yields text as it arrives, then exactly one AiTurnFinished
// carrying the tool calls and token usage for the turn.
public abstract record AiStreamEvent;

public sealed record AiTextDelta(string Text) : AiStreamEvent;

public sealed record AiTurnFinished(AiCompletion Completion) : AiStreamEvent;

public sealed record AiModelInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ProviderId { get; init; }
    public int ContextLength { get; init; }

    // Currency is the provider's — RouterAI bills in roubles per token.
    public double InputPrice { get; init; }
    public double OutputPrice { get; init; }

    public bool SupportsTools { get; init; }
    public bool SupportsTemperature { get; init; }
}
