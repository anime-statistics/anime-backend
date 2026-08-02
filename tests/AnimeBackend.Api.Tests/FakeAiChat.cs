using System.Runtime.CompilerServices;
using AnimeBackend.Application.Ai;

namespace AnimeBackend.Api.Tests;

public sealed class FakeAiChat : IAiChat
{
    public bool IsConfigured { get; set; } = true;

    public string NextText { get; set; } = "Ответ ассистента";

    // Lets a test shape the reply per request (e.g. structured JSON).
    public Func<AiChatRequest, string>? Responder { get; set; }

    public AiChatRequest? LastRequest { get; private set; }

    public Task<AiCompletion> CompleteAsync(AiChatRequest request, CancellationToken ct)
    {
        LastRequest = request;
        var text = Responder?.Invoke(request) ?? NextText;
        return Task.FromResult(new AiCompletion(text, 10, 20, Refused: false));
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AiChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        LastRequest = request;
        var text = Responder?.Invoke(request) ?? NextText;
        foreach (var chunk in text.Chunk(6))
        {
            await Task.Yield();
            yield return new string(chunk);
        }
    }
}
