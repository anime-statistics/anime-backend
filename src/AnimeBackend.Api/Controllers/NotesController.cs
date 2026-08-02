using AnimeBackend.Application.Common;
using AnimeBackend.Application.Notes;
using Microsoft.AspNetCore.Mvc;

namespace AnimeBackend.Api.Controllers;

public sealed record CreateNoteRequest(string? MediaId, string? Content);

public sealed record UpdateNoteRequest(string? Content);

[ApiController]
[Route("api/v1/notes")]
public sealed class NotesController(NotesHandler notes, AttachmentsHandler attachments) : ControllerBase
{
    [HttpGet]
    public async Task<ItemsResponse<NoteDto>> List(
        [FromQuery(Name = "media_id")] string? mediaId, CancellationToken ct)
        => await notes.ListAsync(mediaId, ct);

    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(
        [FromBody] CreateNoteRequest request, CancellationToken ct)
    {
        var note = await notes.CreateAsync(request.MediaId, request.Content, ct);
        return StatusCode(StatusCodes.Status201Created, note);
    }

    [HttpPatch("{id}")]
    public async Task<NoteDto> Update(
        string id, [FromBody] UpdateNoteRequest request, CancellationToken ct)
        => await notes.UpdateAsync(id, request.Content, ct);

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await notes.DeleteAsync(id, ct);
        return NoContent();
    }

    // Multipart image upload used by the markdown editor; the note referencing
    // the file may not exist yet.
    [HttpPost("attachments")]
    public async Task<AttachmentResponse> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            throw new AnimeBackend.Domain.DomainException("Файл не передан");

        await using var stream = file.OpenReadStream();
        return await attachments.SaveAsync(stream, file.Length, file.FileName, ct);
    }
}
