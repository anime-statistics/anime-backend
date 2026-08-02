using AnimeBackend.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnimeBackend.Application.Ai;

public sealed record AiModelDto(
    string Id,
    string Name,
    string ProviderId,
    int ContextWindow,
    bool SupportsStreaming,
    double InputPrice,
    double OutputPrice,
    int UsageCount);

// The router serves hundreds of models; a raw dump is useless in a dropdown.
// The picker leads with what this user actually uses (most-used first), then a
// short curated set so the list is never empty on a fresh install.
public sealed class AiModelCatalog(IAiChat ai, IAppDb db, ILogger<AiModelCatalog> logger)
{
    public const string FallbackModelId = "deepseek/deepseek-r1";

    private const int PickerSize = 20;

    // Sensible starting points: a cheap tool-capable reasoner, a mid tier, and
    // the strongest option for when it matters.
    private static readonly string[] Recommended =
    [
        "deepseek/deepseek-r1",
        "anthropic/claude-sonnet-4.6",
        "anthropic/claude-opus-4.8",
        "anthropic/claude-fable-5",
        "openai/gpt-4o-mini",
    ];

    public async Task<IReadOnlyList<AiModelDto>> ListAsync(
        string? query, bool all, CancellationToken ct)
    {
        if (!ai.IsConfigured) return [];

        var models = await ai.ListModelsAsync(ct);
        // Only tool-capable models: the assistant's whole design is that it
        // queries the collection itself instead of being handed a dump.
        var usable = models.Where(m => m.SupportsTools).ToList();
        var usage = await db.AiModelUsages.ToDictionaryAsync(u => u.ModelId, u => u.CallCount, ct);

        if (!string.IsNullOrWhiteSpace(query))
        {
            usable = [.. usable.Where(m =>
                m.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains(query, StringComparison.OrdinalIgnoreCase))];
        }

        var ordered = usable
            .OrderByDescending(m => usage.GetValueOrDefault(m.Id))
            .ThenBy(m => RecommendedRank(m.Id))
            .ThenBy(m => m.Name)
            .Select(m => Map(m, usage.GetValueOrDefault(m.Id)))
            .ToList();

        if (all || !string.IsNullOrWhiteSpace(query)) return ordered;

        // Default view: everything the user has used, topped up with the
        // curated defaults to a workable list length.
        var used = ordered.Where(m => m.UsageCount > 0).ToList();
        var rest = ordered.Where(m => m.UsageCount == 0 && Recommended.Contains(m.Id));
        return [.. used.Concat(rest).Take(PickerSize)];
    }

    // The picker sends whatever it last stored; an id the router no longer
    // serves silently falls back instead of failing the request.
    public async Task<AiModelInfo?> ResolveAsync(string? requestedId, CancellationToken ct)
    {
        IReadOnlyList<AiModelInfo> models;
        try
        {
            models = await ai.ListModelsAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Каталог моделей недоступен, беру запрошенную модель как есть");
            return null;
        }

        if (requestedId is { Length: > 0 }
            && models.FirstOrDefault(m => m.Id == requestedId) is { } exact
            && exact.SupportsTools)
        {
            return exact;
        }

        foreach (var candidate in Recommended)
        {
            if (models.FirstOrDefault(m => m.Id == candidate && m.SupportsTools) is { } recommended)
                return recommended;
        }

        return models.FirstOrDefault(m => m.SupportsTools);
    }

    private static int RecommendedRank(string id)
    {
        var index = Array.IndexOf(Recommended, id);
        return index < 0 ? Recommended.Length : index;
    }

    private static AiModelDto Map(AiModelInfo model, int usageCount) => new(
        model.Id,
        model.Name,
        model.ProviderId,
        model.ContextLength,
        SupportsStreaming: true,
        model.InputPrice,
        model.OutputPrice,
        usageCount);
}
