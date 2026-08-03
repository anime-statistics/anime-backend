using AnimeBackend.Application.Abstractions;
using AnimeBackend.Application.Common;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnimeBackend.Application.Search;

// One request from the client, one merged list back. The backend fans out to
// every requested source in parallel, collapses duplicates and overlays
// collection tags from the local database. Nothing is persisted here.
public sealed class SearchHandler(
    IAppDb db,
    IEnumerable<ISourceClient> clients,
    ILogger<SearchHandler> logger,
    TimeSpan? sourceBudget = null)
{
    // One source does not get to decide how long the whole search takes. The
    // HTTP clients carry their own ceiling; this one is here so the guarantee
    // survives a client that misbehaves in a way its transport cannot catch.
    private static readonly TimeSpan DefaultSourceBudget = TimeSpan.FromSeconds(12);

    private readonly TimeSpan _sourceBudget = sourceBudget ?? DefaultSourceBudget;

    public async Task<ItemsResponse<AnimeListItemDto>> SearchAnimeAsync(
        string? query, string? sourcesCsv, CancellationToken ct)
    {
        var (merged, tagsById) = await SearchAsync(query, sourcesCsv, MediaType.Anime, ct);
        var items = merged
            .Select(m => AnimeListItemDto.From(m.Snapshot, TagsFor(m, tagsById)))
            .ToList();
        return new ItemsResponse<AnimeListItemDto>(items, items.Count);
    }

    public async Task<ItemsResponse<MangaListItemDto>> SearchMangaAsync(
        string? query, string? sourcesCsv, CancellationToken ct)
    {
        var (merged, tagsById) = await SearchAsync(query, sourcesCsv, MediaType.Manga, ct);
        var items = merged
            .Select(m => MangaListItemDto.From(m.Snapshot, TagsFor(m, tagsById)))
            .ToList();
        return new ItemsResponse<MangaListItemDto>(items, items.Count);
    }

    private async Task<(List<SearchMerger.Merged> Merged, Dictionary<string, IReadOnlyList<string>> TagsById)>
        SearchAsync(string? query, string? sourcesCsv, MediaType type, CancellationToken ct)
    {
        var selected = SelectClients(sourcesCsv);
        var results = await Task.WhenAll(selected.Select(c => SearchOneAsync(c, query ?? "", type, ct)));

        if (selected.Count > 0 && results.All(r => r is null))
            throw new SourceUnavailableException("Источники каталогов недоступны, попробуйте позже");

        var all = results.Where(r => r is not null).SelectMany(r => r!).ToList();
        var merged = SearchMerger.Deduplicate(all);

        // Collection membership overlay: one query for every id that took part
        // in the merged list, tags ordered the way the tag list itself is.
        var ids = merged.SelectMany(m => m.ConstituentIds).ToList();
        // Guid→string happens client-side: SQLite stores guids upper-case and
        // the frontend compares tag ids as exact strings.
        var tagsById = await db.MediaItems
            .Where(m => ids.Contains(m.Id) && m.Tags.Count > 0)
            .Select(m => new
            {
                m.Id,
                TagIds = m.Tags.OrderBy(t => t.SortOrder).Select(t => t.Id).ToList(),
            })
            .ToDictionaryAsync(
                m => m.Id,
                m => (IReadOnlyList<string>)[.. m.TagIds.Select(id => id.ToString())],
                ct);

        return (merged, tagsById);
    }

    private static IReadOnlyList<string> TagsFor(
        SearchMerger.Merged merged, Dictionary<string, IReadOnlyList<string>> tagsById)
    {
        foreach (var id in merged.ConstituentIds)
        {
            if (tagsById.TryGetValue(id, out var tags) && tags.Count > 0)
                return tags;
        }
        return [];
    }

    private List<ISourceClient> SelectClients(string? sourcesCsv)
    {
        var all = clients.OrderBy(c => c.Source).ToList();
        if (string.IsNullOrWhiteSpace(sourcesCsv)) return all;

        var requested = sourcesCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(MediaSourceExtensions.FromWire)
            .Where(s => s is not null)
            .Select(s => s!.Value)
            .ToHashSet();

        return requested.Count == 0 ? all : [.. all.Where(c => requested.Contains(c.Source))];
    }

    // A source that is down — or merely stuck — contributes nothing instead of
    // failing the whole search; null marks the failure so "every source is
    // down" stays detectable. Stuck counts as down: answering late is the same
    // as not answering, and waiting for it would strand every other source.
    private async Task<IReadOnlyList<MediaSnapshot>?> SearchOneAsync(
        ISourceClient client, string query, MediaType type, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(_sourceBudget);

        try
        {
            return await client.SearchAsync(query, type, budget.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Источник {Source} не ответил на поиск", client.Source.ToWire());
            return null;
        }
    }
}
