using AnimeBackend.Domain;
using Microsoft.AspNetCore.Mvc;

namespace AnimeBackend.Api.Controllers;

// The AI endpoints exist in the frontend as sketches; this backend does not
// implement them yet. The models list is honestly empty (the settings page
// polls it unconditionally), everything else answers 404 with a readable
// message — 5xx would trigger the frontend's triple retry.
[ApiController]
[Route("api/v1/ai")]
public sealed class AiController : ControllerBase
{
    [HttpGet("models")]
    public IActionResult Models()
        => Ok(new { Items = Array.Empty<object>() });

    [HttpPost("chat")]
    [HttpPost("chat/stream")]
    [HttpPost("paraphrase")]
    [HttpPost("recommendations")]
    [HttpPost("process-voice")]
    public IActionResult NotImplemented()
        => throw new NotFoundException("AI-ассистент не подключён в этой версии бэкенда");
}
