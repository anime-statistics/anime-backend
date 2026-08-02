using System.Runtime.CompilerServices;
using AnimeBackend.Application.Ai;

namespace AnimeBackend.Api.Tests;

public sealed class FakeAiChat : IAiChat
{
    private readonly Queue<AiCompletion> _scripted = new();

    public bool IsConfigured { get; set; } = true;

    public string NextText { get; set; } = "Ответ ассистента";

    public List<AiChatRequest> Requests { get; } = [];

    public AiChatRequest? LastRequest => Requests.Count > 0 ? Requests[^1] : null;

    public IReadOnlyList<AiModelInfo> Models { get; set; } =
    [
        new()
        {
            Id = "deepseek/deepseek-r1",
            Name = "DeepSeek: R1",
            ProviderId = "deepseek",
            ContextLength = 163_840,
            InputPrice = 7.23e-5,
            OutputPrice = 2.58e-4,
            SupportsTools = true,
            SupportsTemperature = true,
        },
        new()
        {
            Id = "anthropic/claude-fable-5",
            Name = "Anthropic: Claude Fable 5",
            ProviderId = "anthropic",
            ContextLength = 1_000_000,
            InputPrice = 1.03e-3,
            OutputPrice = 5.17e-3,
            SupportsTools = true,
            SupportsTemperature = false,
        },
        new()
        {
            Id = "legacy/no-tools",
            Name = "Legacy: no tools",
            ProviderId = "legacy",
            ContextLength = 8_192,
            SupportsTools = false,
            SupportsTemperature = true,
        },
    ];

    // Queue turns to script a tool-calling loop; anything past the queue falls
    // back to plain text.
    public void Script(params AiCompletion[] turns)
    {
        foreach (var turn in turns) _scripted.Enqueue(turn);
    }

    public void Reset()
    {
        _scripted.Clear();
        Requests.Clear();
        NextText = "Ответ ассистента";
    }

    public Task<AiCompletion> CompleteAsync(AiChatRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(NextTurn());
    }

    public async IAsyncEnumerable<AiStreamEvent> StreamAsync(
        AiChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        Requests.Add(request);
        var completion = NextTurn();

        foreach (var chunk in completion.Text.Chunk(6))
        {
            await Task.Yield();
            yield return new AiTextDelta(new string(chunk));
        }

        yield return new AiTurnFinished(completion);
    }

    public Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken ct)
        => Task.FromResult(Models);

    private AiCompletion NextTurn()
        => _scripted.Count > 0
            ? _scripted.Dequeue()
            : new AiCompletion { Text = NextText, InputTokens = 10, OutputTokens = 20 };
}
