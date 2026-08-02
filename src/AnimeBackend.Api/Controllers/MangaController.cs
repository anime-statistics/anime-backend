using AnimeBackend.Application.Common;
using AnimeBackend.Application.Library;
using Microsoft.AspNetCore.Mvc;

namespace AnimeBackend.Api.Controllers;

[ApiController]
[Route("api/v1/manga")]
public sealed class MangaController(
    GetLibraryHandler library,
    GetDetailHandler detail,
    UpdateProgressHandler progress,
    ReplaceTagsHandler replaceTags,
    BulkTagsHandler bulkTags) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResponse<MangaListItemDto>> List(
        [FromQuery] string? query,
        [FromQuery] string? tag,
        [FromQuery] int page = 1,
        [FromQuery] int size = 200,
        CancellationToken ct = default)
        => await library.MangaAsync(query, tag, page, size, ct);

    [HttpGet("{id}")]
    public async Task<MangaDetailDto> Detail(string id, CancellationToken ct)
        => await detail.MangaAsync(id, ct);

    [HttpPatch("{id}/progress")]
    public async Task<MangaDetailDto> UpdateProgress(
        string id, [FromBody] ProgressUpdateRequest request, CancellationToken ct)
        => await progress.MangaAsync(id, request, ct);

    [HttpPatch("{id}/tags")]
    public async Task<MangaDetailDto> ReplaceTags(
        string id, [FromBody] TagsUpdateRequest request, CancellationToken ct)
        => await replaceTags.MangaAsync(id, request.MyTags ?? [], ct);

    [HttpPost("tags/bulk")]
    public async Task<BulkUpdateResponse> BulkTags(
        [FromBody] BulkTagsRequest request, CancellationToken ct)
        => await bulkTags.MangaAsync(request, ct);
}
