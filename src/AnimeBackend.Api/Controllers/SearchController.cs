using AnimeBackend.Application.Search;
using Microsoft.AspNetCore.Mvc;

namespace AnimeBackend.Api.Controllers;

[ApiController]
[Route("api/v1/search")]
public sealed class SearchController(SearchHandler handler) : ControllerBase
{
    // One request from the client; fan-out, merge and tag overlay happen here.
    // Empty query returns the catalogue head. Nothing is persisted by search.
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] string? sources,
        [FromQuery] string? type,
        CancellationToken ct)
    {
        if (type == "manga")
            return Ok(await handler.SearchMangaAsync(query, sources, ct));
        return Ok(await handler.SearchAnimeAsync(query, sources, ct));
    }
}
