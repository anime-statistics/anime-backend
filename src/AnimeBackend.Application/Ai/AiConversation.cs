using System.Runtime.CompilerServices;
using AnimeBackend.Application.Abstractions;
using AnimeBackend.Domain.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnimeBackend.Application.Ai;

// The agentic loop: ask, run whatever tools the model asked for, feed the
// results back, repeat until it answers. Provider-independent by design — the
// IAiChat implementation only knows single round trips.
public sealed class AiConversation(
    IAiChat ai,
    AiToolbox toolbox,
    IAppDb db,
    ILogger<AiConversation> logger)
{
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(30);

    public sealed record Result(string Text, long InputTokens, long OutputTokens, int ToolCallCount);

    public async Task<Result> RunAsync(AiChatRequest request, CancellationToken ct)
    {
        var messages = new List<AiChatMessage>(request.Messages);
        long inputTokens = 0, outputTokens = 0;
        var toolCallCount = 0;
        var toolsOffered = request.Tools is { Count: > 0 };
        var groundingRetried = false;

        for (var round = 0; ; round++)
        {
            var lastRound = round >= request.MaxToolRounds;
            if (lastRound) toolsOffered = false;

            // With tools withdrawn the model can no longer look anything up, so
            // it is told plainly to answer from what the tools already returned
            // rather than filling the gap from memory.
            var system = toolsOffered || request.Tools is not { Count: > 0 }
                ? request.System
                : request.System + "\n\nБольше инструментов нет. Отвечай строго по данным, "
                  + "которые уже вернули инструменты выше. Если данных не хватает, так и скажи — "
                  + "не придумывай названия, оценки и статистику.";

            // Tools stay declared even once they're closed off — the history
            // already contains tool_calls and tool replies, and dropping the
            // declarations under them leaves providers stalling for minutes.
            // "none" is how you say "answer now" without breaking the history.
            var hasTools = request.Tools is { Count: > 0 };
            var completion = await ai.CompleteAsync(
                request with
                {
                    Messages = messages,
                    Tools = request.Tools,
                    // Asking nicely isn't enough: models happily answer about
                    // the collection from memory. The opening round has to call
                    // a tool, so every answer starts from real data.
                    ToolChoice = !hasTools ? null
                        : !toolsOffered ? "none"
                        : round == 0 ? "required"
                        : null,
                    System = system,
                },
                ct);
            inputTokens += completion.InputTokens;
            outputTokens += completion.OutputTokens;

            if (completion.WantsTools && toolsOffered)
            {
                messages.Add(new AiChatMessage
                {
                    Role = "assistant",
                    Content = completion.Text,
                    ToolCalls = completion.ToolCalls,
                });
                toolCallCount += completion.ToolCalls.Count;
                messages.AddRange(await ExecuteAsync(completion.ToolCalls, ct));
                continue;
            }

            // A model that answers with neither text nor tools gets one more
            // chance without tools before we give up on the turn.
            if (completion.Text.Length == 0 && toolsOffered)
            {
                toolsOffered = false;
                continue;
            }

            // "required" is honoured almost always, but not quite: an answer
            // straight off the opening round was written from memory, not from
            // the collection. Ask once more before trusting it.
            if (round == 0 && toolsOffered && !groundingRetried)
            {
                groundingRetried = true;
                logger.LogInformation(
                    "Модель ответила без обращения к коллекции — повторяю запрос");
                round--;
                continue;
            }

            await RecordUsageAsync(request.Model, inputTokens, outputTokens, ct);
            return new Result(completion.Text, inputTokens, outputTokens, toolCallCount);
        }
    }

    // Streaming a tool-calling turn is not portable: some providers (RouterAI
    // with DeepSeek R1, for one) never emit tool_call deltas at all and send
    // only reasoning, which silently turns a grounded answer into an invented
    // one. So whenever tools are on the table the rounds run unstreamed, and
    // the finished answer is handed out in pieces. Toolless calls stream for
    // real.
    public async IAsyncEnumerable<string> StreamAsync(
        AiChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        if (request.Tools is { Count: > 0 })
        {
            var result = await RunAsync(request, ct);
            foreach (var chunk in Chunk(result.Text))
                yield return chunk;
            yield break;
        }

        AiCompletion? completion = null;
        await foreach (var streamEvent in ai.StreamAsync(request, ct))
        {
            switch (streamEvent)
            {
                case AiTextDelta delta when delta.Text.Length > 0:
                    yield return delta.Text;
                    break;
                case AiTurnFinished finished:
                    completion = finished.Completion;
                    break;
            }
        }

        await RecordUsageAsync(
            request.Model, completion?.InputTokens ?? 0, completion?.OutputTokens ?? 0, ct);
    }

    private static IEnumerable<string> Chunk(string text)
    {
        const int size = 24;
        for (var offset = 0; offset < text.Length; offset += size)
            yield return text.Substring(offset, Math.Min(size, text.Length - offset));
    }

    // A turn routinely fans out several calls — verifying five candidates
    // against the catalogue, say. Distinct calls run once and every call id
    // gets its answer, duplicates included. Each execution is bounded: a tool
    // that never returns must not wedge the whole request, and the model gets
    // told what happened instead of the user watching a spinner forever.
    private async Task<List<AiChatMessage>> ExecuteAsync(
        IReadOnlyList<AiToolCall> calls, CancellationToken ct)
    {
        var outputs = new Dictionary<(string Name, string Arguments), string>();

        foreach (var call in calls)
        {
            var key = (call.Name, call.ArgumentsJson);
            if (outputs.ContainsKey(key)) continue;

            logger.LogInformation(
                "AI вызывает инструмент {Tool} {Arguments}", call.Name, call.ArgumentsJson);

            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(ToolTimeout);
            try
            {
                outputs[key] = await toolbox.ExecuteAsync(call.Name, call.ArgumentsJson, budget.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Инструмент {Tool} не уложился в отведённое время", call.Name);
                outputs[key] = """{"error":"инструмент не ответил вовремя"}""";
            }
        }

        return
        [
            .. calls.Select(call => new AiChatMessage
            {
                Role = "tool",
                ToolCallId = call.Id,
                Content = outputs[(call.Name, call.ArgumentsJson)],
            }),
        ];
    }

    // Feeds the model picker: models the user actually reaches for come first.
    private async Task RecordUsageAsync(string modelId, long input, long output, CancellationToken ct)
    {
        try
        {
            var usage = await db.AiModelUsages.FirstOrDefaultAsync(u => u.ModelId == modelId, ct);
            if (usage is null)
            {
                usage = new AiModelUsage(modelId);
                db.AiModelUsages.Add(usage);
            }
            usage.Record(input, output);
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Bookkeeping must never fail the user's answer.
            logger.LogWarning(ex, "Не удалось записать статистику модели {Model}", modelId);
        }
    }
}
