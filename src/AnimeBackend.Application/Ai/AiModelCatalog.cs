namespace AnimeBackend.Application.Ai;

public sealed record AiModelDto(
    string Id,
    string Name,
    string ProviderId,
    int ContextWindow,
    bool SupportsStreaming,
    double InputPrice,
    double OutputPrice);

// The models this backend serves. Prices are per token (the frontend
// multiplies by its own token estimates), matching the mock's convention.
public static class AiModelCatalog
{
    public const string DefaultModelId = "claude-opus-5";

    public static readonly IReadOnlyList<AiModelDto> Models =
    [
        new("claude-opus-5", "Claude Opus 5", "anthropic", 1_000_000, true, 5e-6, 25e-6),
        new("claude-sonnet-5", "Claude Sonnet 5", "anthropic", 1_000_000, true, 3e-6, 15e-6),
        new("claude-haiku-4-5", "Claude Haiku 4.5", "anthropic", 200_000, true, 1e-6, 5e-6),
    ];

    // Unknown ids (e.g. a model the mock advertised) quietly fall back to the
    // default instead of erroring — the picker is a preference, not a contract.
    public static string Resolve(string? requestedId)
        => Models.Any(m => m.Id == requestedId) ? requestedId! : DefaultModelId;
}
