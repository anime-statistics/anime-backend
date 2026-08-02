using AnimeBackend.Application.Common;
using AnimeBackend.Application.Library;
using Microsoft.AspNetCore.Mvc;

namespace AnimeBackend.Api.Controllers;

public sealed record TagsUpdateRequest(IReadOnlyList<string>? MyTags);

[ApiController]
[Route("api/v1/anime")]
public sealed class AnimeController(
    GetLibraryHandler library,
    GetDetailHandler detail,
    UpdateProgressHandler progress,
    ReplaceTagsHandler replaceTags,
    BulkTagsHandler bulkTags) : ControllerBase
{
    // Library view: ONLY works with at least one tag. The catalogue lives in
    // /search.
    [HttpGet]
    public async Task<PagedResponse<AnimeListItemDto>> List(
        [FromQuery] string? query,
        [FromQuery] string? tag,
        [FromQuery] int page = 1,
        [FromQuery] int size = 200,
        CancellationToken ct = default)
        => await library.AnimeAsync(query, tag, page, size, ct);

    // Detail opens for works outside the collection too (straight from the
    // source, not saved).
    [HttpGet("{id}")]
    public async Task<AnimeDetailDto> Detail(string id, CancellationToken ct)
        => await detail.AnimeAsync(id, ct);

    [HttpPatch("{id}/progress")]
    public async Task<AnimeDetailDto> UpdateProgress(
        string id, [FromBody] ProgressUpdateRequest request, CancellationToken ct)
        => await progress.AnimeAsync(id, request, ct);

    // Full replacement; an empty list removes the work from the collection.
    // First tag on a search result materialises the work locally.
    [HttpPatch("{id}/tags")]
    public async Task<AnimeDetailDto> ReplaceTags(
        string id, [FromBody] TagsUpdateRequest request, CancellationToken ct)
        => await replaceTags.AnimeAsync(id, request.MyTags ?? [], ct);

    [HttpPost("tags/bulk")]
    public async Task<BulkUpdateResponse> BulkTags(
        [FromBody] BulkTagsRequest request, CancellationToken ct)
        => await bulkTags.AnimeAsync(request, ct);
}
