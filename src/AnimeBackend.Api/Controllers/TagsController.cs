using AnimeBackend.Application.Common;
using AnimeBackend.Application.Tags;
using Microsoft.AspNetCore.Mvc;

namespace AnimeBackend.Api.Controllers;

[ApiController]
[Route("api/v1/tags")]
public sealed class TagsController(TagsHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ItemsResponse<TagDto>> List(CancellationToken ct)
        => await handler.ListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<TagDto>> Create([FromBody] CreateTagRequest request, CancellationToken ct)
    {
        var tag = await handler.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, tag);
    }

    [HttpPatch("{id}")]
    public async Task<TagDto> Update(string id, [FromBody] UpdateTagRequest request, CancellationToken ct)
        => await handler.UpdateAsync(id, request, ct);

    // Deletes just the tag. Detaching it from works (or clearing collections)
    // is the frontend's job via bulk calls before this one.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await handler.DeleteAsync(id, ct);
        return NoContent();
    }
}
