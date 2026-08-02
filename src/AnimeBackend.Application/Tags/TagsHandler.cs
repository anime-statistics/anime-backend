using AnimeBackend.Application.Abstractions;
using AnimeBackend.Application.Common;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Tags;
using Microsoft.EntityFrameworkCore;

namespace AnimeBackend.Application.Tags;

public sealed record CreateTagRequest(string? Name, string? Color, string? Icon, bool? IsHidden, int? SortOrder);

public sealed record UpdateTagRequest(string? Name, string? Color, string? Icon, bool? IsHidden, int? SortOrder);

public sealed class TagsHandler(IAppDb db)
{
    public async Task<ItemsResponse<TagDto>> ListAsync(CancellationToken ct)
    {
        var tags = await db.Tags
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);
        return new ItemsResponse<TagDto>([.. tags.Select(TagDto.From)], tags.Count);
    }

    public async Task<TagDto> CreateAsync(CreateTagRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Название тега обязательно");
        if (string.IsNullOrWhiteSpace(request.Color))
            throw new DomainException("Цвет тега обязателен");

        var sortOrder = request.SortOrder
            ?? (await db.Tags.MaxAsync(t => (int?)t.SortOrder, ct) ?? -1) + 1;

        var tag = new Tag(Guid.NewGuid(), request.Name, request.Color, request.Icon,
            request.IsHidden ?? false, sortOrder);
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
        return TagDto.From(tag);
    }

    public async Task<TagDto> UpdateAsync(string rawId, UpdateTagRequest request, CancellationToken ct)
    {
        var tag = await FindAsync(rawId, ct);

        if (request.Name is not null) tag.Rename(request.Name);
        if (request.Color is not null) tag.Recolor(request.Color);
        if (request.Icon is not null) tag.SetIcon(request.Icon);
        if (request.IsHidden is not null) tag.SetHidden(request.IsHidden.Value);
        if (request.SortOrder is not null) tag.SetSortOrder(request.SortOrder.Value);

        await db.SaveChangesAsync(ct);
        return TagDto.From(tag);
    }

    // Deleting a tag deletes just the tag: the frontend detaches it from works
    // (or clears collections) with explicit bulk calls before this one.
    public async Task DeleteAsync(string rawId, CancellationToken ct)
    {
        var tag = await FindAsync(rawId, ct);
        db.Tags.Remove(tag);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Tag> FindAsync(string rawId, CancellationToken ct)
    {
        if (!Guid.TryParse(rawId, out var id))
            throw new DomainException("Некорректный идентификатор тега");
        return await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Тег не найден");
    }
}
