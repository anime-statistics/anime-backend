using System.Text;
using AnimeBackend.Application.Ai;
using Microsoft.AspNetCore.Mvc;

namespace AnimeBackend.Api.Controllers;

// The /ai group, backed by the Anthropic API. Without a configured key the
// models list is honestly empty and every action answers 404 with a readable
// message (5xx would trigger the frontend's triple retry).
[ApiController]
[Route("api/v1/ai")]
public sealed class AiController(AiHandlers handlers, IAiChat ai) : ControllerBase
{
    [HttpGet("models")]
    public IActionResult Models()
        => Ok(new { Items = ai.IsConfigured ? AiModelCatalog.Models : [] });

    [HttpPost("chat")]
    public async Task<AiChatResponseDto> Chat([FromBody] AiChatApiRequest request, CancellationToken ct)
        => await handlers.ChatAsync(request, ct);

    // Plain text chunks, exactly what the frontend's fetch-reader expects —
    // not SSE. Model deltas are flushed as they arrive.
    [HttpPost("chat/stream")]
    public async Task ChatStream([FromBody] AiChatApiRequest request, CancellationToken ct)
    {
        Response.ContentType = "text/plain; charset=utf-8";
        await foreach (var chunk in handlers.ChatStreamAsync(request, ct))
        {
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(chunk), ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    [HttpPost("paraphrase")]
    public async Task<AiParaphraseResponseDto> Paraphrase(
        [FromBody] AiParaphraseApiRequest request, CancellationToken ct)
        => await handlers.ParaphraseAsync(request, ct);

    [HttpPost("recommendations")]
    public async Task<AiRecommendationsResponseDto> Recommendations(
        [FromBody] AiRecommendationsApiRequest request, CancellationToken ct)
        => await handlers.RecommendationsAsync(request, ct);

    [HttpPost("process-voice")]
    public async Task<AiProcessVoiceResponseDto> ProcessVoice(
        [FromBody] AiProcessVoiceApiRequest request, CancellationToken ct)
        => await handlers.ProcessVoiceAsync(request, ct);
}
