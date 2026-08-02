using AnimeBackend.Application.Abstractions;
using AnimeBackend.Application.Common;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace AnimeBackend.Application.Library;

// Library lists serve ONLY the collection: works with at least one tag. The
// frontend pages through with size=200 and needs an exact total to fetch the
// rest, so pagination is honoured server-side even though sorting stays on the
// client for now.
public sealed class GetLibraryHandler(IAppDb db)
{
    private const int MaxPageSize = 500;

    public async Task<PagedResponse<AnimeListItemDto>> AnimeAsync(
        string? query, string? tagId, int page, int size, CancellationToken ct)
    {
        var (items, total, p, s) = await QueryAsync(MediaType.Anime, query, tagId, page, size, ct);
        return new PagedResponse<AnimeListItemDto>([.. items.Select(AnimeListItemDto.From)], total, p, s);
    }

    public async Task<PagedResponse<MangaListItemDto>> MangaAsync(
        string? query, string? tagId, int page, int size, CancellationToken ct)
    {
        var (items, total, p, s) = await QueryAsync(MediaType.Manga, query, tagId, page, size, ct);
        return new PagedResponse<MangaListItemDto>([.. items.Select(MangaListItemDto.From)], total, p, s);
    }

    private async Task<(List<MediaItem> Items, int Total, int Page, int Size)> QueryAsync(
        MediaType type, string? query, string? tagId, int page, int size, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, MaxPageSize);

        var filtered = db.MediaItems.Where(m => m.Type == type && m.Tags.Count > 0);

        if (!string.IsNullOrWhiteSpace(tagId))
        {
            if (!Guid.TryParse(tagId, out var tagGuid))
                throw new DomainException("Некорректный идентификатор тега");
            filtered = filtered.Where(m => m.Tags.Any(t => t.Id == tagGuid));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var needle = query.Trim().ToLowerInvariant();
            filtered = filtered.Where(m => m.SearchText.Contains(needle));
        }

        var total = await filtered.CountAsync(ct);
        var items = await filtered
            .OrderBy(m => m.Title)
            .ThenBy(m => m.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Include(m => m.Tags)
            .ToListAsync(ct);

        return (items, total, page, size);
    }
}

// Detail cards must open for works outside the collection too — straight from
// the source, nothing saved.
public sealed class GetDetailHandler(IAppDb db, Materializer materializer)
{
    public async Task<AnimeDetailDto> AnimeAsync(string rawId, CancellationToken ct)
    {
        var (item, snapshot) = await FindAsync(rawId, MediaType.Anime, ct);
        return item is not null ? AnimeDetailDto.From(item) : AnimeDetailDto.From(snapshot!);
    }

    public async Task<MangaDetailDto> MangaAsync(string rawId, CancellationToken ct)
    {
        var (item, snapshot) = await FindAsync(rawId, MediaType.Manga, ct);
        return item is not null ? MangaDetailDto.From(item) : MangaDetailDto.From(snapshot!);
    }

    private async Task<(MediaItem? Item, MediaSnapshot? Snapshot)> FindAsync(
        string rawId, MediaType type, CancellationToken ct)
    {
        var id = MediaId.Parse(rawId);

        var item = await db.MediaItems
            .Include(m => m.Tags)
            .FirstOrDefaultAsync(m => m.Id == rawId && m.Type == type, ct);
        if (item is not null) return (item, null);

        var snapshot = await materializer.LoadSnapshotAsync(id, type, ct)
            ?? throw new NotFoundException("Работа не найдена");
        return (null, snapshot);
    }
}
