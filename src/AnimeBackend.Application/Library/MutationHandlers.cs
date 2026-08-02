using AnimeBackend.Application.Abstractions;
using AnimeBackend.Application.Common;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;
using AnimeBackend.Domain.Tags;
using Microsoft.EntityFrameworkCore;

namespace AnimeBackend.Application.Library;

public sealed record ProgressUpdateRequest(
    double? Score, int? WatchedEpisodes, int? VolumesRead, int? ChaptersRead);

public sealed class UpdateProgressHandler(IAppDb db, Materializer materializer)
{
    public async Task<AnimeDetailDto> AnimeAsync(string rawId, ProgressUpdateRequest request, CancellationToken ct)
    {
        var item = await ApplyAsync(rawId, MediaType.Anime, request, ct);
        return AnimeDetailDto.From(item);
    }

    public async Task<MangaDetailDto> MangaAsync(string rawId, ProgressUpdateRequest request, CancellationToken ct)
    {
        var item = await ApplyAsync(rawId, MediaType.Manga, request, ct);
        return MangaDetailDto.From(item);
    }

    private async Task<MediaItem> ApplyAsync(
        string rawId, MediaType type, ProgressUpdateRequest request, CancellationToken ct)
    {
        var item = await materializer.GetOrLoadAsync(rawId, type, ct);
        item.UpdateProgress(request.Score, request.WatchedEpisodes, request.VolumesRead, request.ChaptersRead);
        await db.SaveChangesAsync(ct);
        return item;
    }
}

public sealed class ReplaceTagsHandler(IAppDb db, Materializer materializer)
{
    public async Task<AnimeDetailDto> AnimeAsync(string rawId, IReadOnlyList<string> myTags, CancellationToken ct)
        => AnimeDetailDto.From(await ApplyAsync(rawId, MediaType.Anime, myTags, ct));

    public async Task<MangaDetailDto> MangaAsync(string rawId, IReadOnlyList<string> myTags, CancellationToken ct)
        => MangaDetailDto.From(await ApplyAsync(rawId, MediaType.Manga, myTags, ct));

    private async Task<MediaItem> ApplyAsync(
        string rawId, MediaType type, IReadOnlyList<string> myTags, CancellationToken ct)
    {
        var tags = await ResolveTagsAsync(db, myTags, ct);
        var item = await materializer.GetOrLoadAsync(rawId, type, ct);
        item.ReplaceTags(tags);
        await db.SaveChangesAsync(ct);
        return item;
    }

    internal static async Task<List<Tag>> ResolveTagsAsync(
        IAppDb db, IReadOnlyList<string> rawIds, CancellationToken ct)
    {
        var ids = new List<Guid>(rawIds.Count);
        foreach (var raw in rawIds)
        {
            if (!Guid.TryParse(raw, out var id))
                throw new DomainException($"Некорректный идентификатор тега: {raw}");
            ids.Add(id);
        }

        var tags = await db.Tags.Where(t => ids.Contains(t.Id)).ToListAsync(ct);
        var missing = ids.Except(tags.Select(t => t.Id)).ToList();
        if (missing.Count > 0)
            throw new DomainException($"Тег не найден: {missing[0]}");
        return tags;
    }
}

public sealed record BulkTagsRequest(
    IReadOnlyList<string> Ids,
    IReadOnlyList<string>? Add,
    IReadOnlyList<string>? Remove,
    bool? Clear);

// `clear: true` wipes every tag and overrides add/remove; otherwise removals
// run first, then additions without duplicates. Returns how many works
// actually changed.
public sealed class BulkTagsHandler(IAppDb db, Materializer materializer)
{
    public Task<BulkUpdateResponse> AnimeAsync(BulkTagsRequest request, CancellationToken ct)
        => ApplyAsync(MediaType.Anime, request, ct);

    public Task<BulkUpdateResponse> MangaAsync(BulkTagsRequest request, CancellationToken ct)
        => ApplyAsync(MediaType.Manga, request, ct);

    private async Task<BulkUpdateResponse> ApplyAsync(
        MediaType type, BulkTagsRequest request, CancellationToken ct)
    {
        if (request.Ids is not { Count: > 0 })
            throw new DomainException("Нужен хотя бы один идентификатор работы");

        var clear = request.Clear ?? false;
        var addTags = clear || request.Add is not { Count: > 0 }
            ? []
            : await ReplaceTagsHandler.ResolveTagsAsync(db, request.Add, ct);
        var removeIds = ParseGuids(request.Remove);

        var updated = 0;
        foreach (var rawId in request.Ids.Distinct())
        {
            var item = await materializer.GetOrLoadAsync(rawId, type, ct);
            if (item.ApplyBulk(addTags, removeIds, clear))
                updated++;
        }

        await db.SaveChangesAsync(ct);
        return new BulkUpdateResponse(updated);
    }

    private static HashSet<Guid> ParseGuids(IReadOnlyList<string>? rawIds)
    {
        var result = new HashSet<Guid>();
        foreach (var raw in rawIds ?? [])
        {
            if (!Guid.TryParse(raw, out var id))
                throw new DomainException($"Некорректный идентификатор тега: {raw}");
            result.Add(id);
        }
        return result;
    }
}
