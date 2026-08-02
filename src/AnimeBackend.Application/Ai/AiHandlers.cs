using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AnimeBackend.Application.Abstractions;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnimeBackend.Application.Ai;

// The /ai group. Prompts are Russian because the catalogue, notes and UI are;
// replies come back in the user's language either way. Temperature from the
// frontend is deliberately ignored — current Claude models reject sampling
// parameters — and `deep_think` maps to the effort level instead.
public sealed class AiHandlers(
    IAiChat ai,
    IAppDb db,
    IEnumerable<ISourceClient> sources,
    ILogger<AiHandlers> logger)
{
    private const string RefusedReply =
        "Запрос отклонён системой безопасности модели. Попробуйте переформулировать.";

    public static void EnsureConfigured(IAiChat ai)
    {
        if (!ai.IsConfigured)
        {
            throw new NotFoundException(
                "AI-ассистент не подключён: задайте ключ Anthropic API " +
                "(переменная окружения ANTHROPIC_API_KEY) и перезапустите бэкенд");
        }
    }

    public async Task<AiChatResponseDto> ChatAsync(AiChatApiRequest request, CancellationToken ct)
    {
        EnsureConfigured(ai);
        var completion = await ai.CompleteAsync(BuildChatRequest(request), ct);
        var reply = completion.Refused ? RefusedReply : completion.Text;
        return new AiChatResponseDto(reply, new AiUsageDto(completion.InputTokens, completion.OutputTokens));
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        AiChatApiRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureConfigured(ai);
        await foreach (var chunk in ai.StreamAsync(BuildChatRequest(request), ct))
            yield return chunk;
    }

    public async Task<AiParaphraseResponseDto> ParaphraseAsync(
        AiParaphraseApiRequest request, CancellationToken ct)
    {
        EnsureConfigured(ai);
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new DomainException("Нечего перефразировать: текст пуст");

        var styleInstruction = request.Style switch
        {
            "formal" => "Перепиши текст в официально-деловом стиле.",
            "casual" => "Перепиши текст в разговорном, дружеском стиле.",
            "compress" => "Сожми текст, сохранив смысл: примерно вдвое короче.",
            "academic" => "Перепиши текст в академическом стиле.",
            _ => "Приведи текст в порядок: исправь пунктуацию и убери шероховатости.",
        };

        var completion = await ai.CompleteAsync(new AiChatRequest
        {
            Model = AiModelCatalog.Resolve(request.Model),
            System = "Ты редактор заметок в трекере аниме. " + styleInstruction +
                     " Сохрани язык оригинала и Markdown-разметку, если она есть." +
                     " В ответе верни ТОЛЬКО итоговый текст, без пояснений и кавычек.",
            Messages = [new AiChatMessage("user", request.Text)],
            DeepThink = request.DeepThink ?? false,
            MaxTokens = 2048,
        }, ct);

        if (completion.Refused) throw new DomainException(RefusedReply);
        return new AiParaphraseResponseDto(
            completion.Text.Trim(),
            new AiUsageDto(completion.InputTokens, completion.OutputTokens));
    }

    public async Task<AiRecommendationsResponseDto> RecommendationsAsync(
        AiRecommendationsApiRequest request, CancellationToken ct)
    {
        EnsureConfigured(ai);

        var library = await LibrarySnapshotAsync(ct);
        var prompt = new StringBuilder();
        prompt.AppendLine("Подбери 5 аниме для пользователя.");
        prompt.AppendLine($"Настроение: {MoodLabel(request.Mood)}.");
        if (!string.IsNullOrWhiteSpace(request.Prompt))
            prompt.AppendLine($"Пожелание пользователя: {request.Prompt}");
        if (library.Count > 0)
        {
            prompt.AppendLine("Коллекция пользователя (не рекомендуй то, что уже есть):");
            foreach (var line in library) prompt.AppendLine("- " + line);
        }

        var completion = await ai.CompleteAsync(new AiChatRequest
        {
            Model = AiModelCatalog.Resolve(request.Model),
            System = "Ты рекомендательный движок аниме-трекера. Рекомендуй существующие аниме. " +
                     "В title пиши оригинальное ромадзи-название (как на MyAnimeList или Shikimori), " +
                     "в reason — короткое объяснение по-русски, почему это подходит, " +
                     "в score — уверенность от 0 до 1.",
            Messages = [new AiChatMessage("user", prompt.ToString())],
            DeepThink = request.DeepThink ?? false,
            MaxTokens = 3000,
            JsonSchema = RecommendationsSchema,
        }, ct);

        if (completion.Refused) throw new DomainException(RefusedReply);

        var usage = new AiUsageDto(completion.InputTokens, completion.OutputTokens);
        var items = await ResolveRecommendationsAsync(completion.Text, ct);
        return new AiRecommendationsResponseDto(items, usage);
    }

    public async Task<AiProcessVoiceResponseDto> ProcessVoiceAsync(
        AiProcessVoiceApiRequest request, CancellationToken ct)
    {
        EnsureConfigured(ai);
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new DomainException("Пустой текст распознавания");

        var completion = await ai.CompleteAsync(new AiChatRequest
        {
            Model = AiModelCatalog.DefaultModelId,
            System = "Тебе дают сырой текст голосового ввода из аниме-трекера. " +
                     "В processed_text верни его причёсанным: убери слова-паразиты и оговорки, " +
                     "поправь пунктуацию, сохрани язык и смысл. " +
                     "В suggestions предложи до 5 названий существующих аниме, " +
                     "которые пользователь мог иметь в виду или которые подходят по теме; " +
                     "если ничего не подходит — пустой список.",
            Messages = [new AiChatMessage("user", request.Text)],
            MaxTokens = 1000,
            JsonSchema = ProcessVoiceSchema,
        }, ct);

        if (completion.Refused) return new AiProcessVoiceResponseDto(request.Text.Trim(), []);

        try
        {
            using var parsed = JsonDocument.Parse(completion.Text);
            var processed = parsed.RootElement.GetProperty("processed_text").GetString() ?? "";
            var suggestions = parsed.RootElement.GetProperty("suggestions")
                .EnumerateArray()
                .Select(s => s.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .Take(5)
                .ToList();
            return new AiProcessVoiceResponseDto(processed, suggestions);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            logger.LogWarning(ex, "Некорректный JSON от модели в process-voice");
            return new AiProcessVoiceResponseDto(request.Text.Trim(), []);
        }
    }

    private AiChatRequest BuildChatRequest(AiChatApiRequest request)
    {
        if (request.Messages is not { Count: > 0 })
            throw new DomainException("Пустая история чата");

        var system = new StringBuilder(
            "Ты ассистент персонального трекера аниме и манги. Отвечай на языке пользователя, " +
            "кратко и по делу, в Markdown. Ты можешь рекомендовать аниме, объяснять сюжеты без " +
            "спойлеров (или со спойлерами, если явно просят) и помогать с заметками.");

        if (request.Context?.WatchedTitles is { Count: > 0 } watched)
            system.Append("\nПросмотрено пользователем: ").Append(string.Join(", ", watched.Take(50)));
        if (request.Context?.Tags is { Count: > 0 } tags)
            system.Append("\nТеги пользователя: ").Append(string.Join(", ", tags.Take(30)));

        var messages = request.Messages
            .Where(m => m.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new AiChatMessage(m.Role!, m.Content!))
            .ToList();
        if (messages.Count == 0)
            throw new DomainException("Пустая история чата");

        var deepThink = request.DeepThink ?? false;
        return new AiChatRequest
        {
            Model = AiModelCatalog.Resolve(request.Model),
            System = system.ToString(),
            Messages = messages,
            DeepThink = deepThink,
            // max_tokens caps thinking plus reply on current models, so the
            // deep-think mode gets extra headroom.
            MaxTokens = deepThink ? 16000 : 8192,
        };
    }

    // Compact library digest for the recommender: highest-rated first, capped
    // so the prompt stays small even for large collections.
    private async Task<List<string>> LibrarySnapshotAsync(CancellationToken ct)
    {
        var items = await db.MediaItems
            .Where(m => m.Tags.Count > 0)
            .OrderByDescending(m => m.UserScore ?? 0)
            .ThenByDescending(m => m.UpdatedAt)
            .Take(40)
            .Select(m => new { m.Title, m.Genres, m.UserScore })
            .ToListAsync(ct);

        return [.. items.Select(m =>
            m.Title
            + (m.Genres.Count > 0 ? $" ({string.Join(", ", m.Genres.Take(3))})" : "")
            + (m.UserScore is not null ? $" — оценка {m.UserScore:0.#}" : ""))];
    }

    // The model proposes titles; real catalogue ids come from source search so
    // every recommendation opens as a working card. Unresolvable titles are
    // dropped rather than shipped with a fake id.
    private async Task<List<AiRecommendationDto>> ResolveRecommendationsAsync(
        string json, CancellationToken ct)
    {
        List<(string Title, string Reason, double Score)> proposed = [];
        try
        {
            using var parsed = JsonDocument.Parse(json);
            foreach (var item in parsed.RootElement.GetProperty("items").EnumerateArray())
            {
                var title = item.GetProperty("title").GetString();
                if (string.IsNullOrWhiteSpace(title)) continue;
                proposed.Add((
                    title,
                    item.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : "",
                    item.TryGetProperty("score", out var score) && score.TryGetDouble(out var value)
                        ? Math.Clamp(value, 0, 1)
                        : 0.5));
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            logger.LogWarning(ex, "Некорректный JSON от модели в recommendations");
            return [];
        }

        var results = new List<AiRecommendationDto>();
        using var throttle = new SemaphoreSlim(2);
        var resolved = await Task.WhenAll(proposed.Select(async candidate =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                return (candidate, Id: await ResolveMediaIdAsync(candidate.Title, ct));
            }
            finally
            {
                throttle.Release();
            }
        }));

        foreach (var (candidate, id) in resolved)
        {
            if (id is null) continue;
            results.Add(new AiRecommendationDto(id, candidate.Title, candidate.Reason, candidate.Score));
        }
        return results;
    }

    private async Task<string?> ResolveMediaIdAsync(string title, CancellationToken ct)
    {
        foreach (var source in sources.OrderBy(s => s.Source))
        {
            try
            {
                var hits = await source.SearchAsync(title, MediaType.Anime, ct);
                if (hits.Count > 0) return hits[0].Id.ToString();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось найти «{Title}» в {Source}", title, source.Source);
            }
        }
        return null;
    }

    private static string MoodLabel(string? mood) => mood switch
    {
        "sad" => "грустное, хочется драмы",
        "happy" => "весёлое, хочется лёгкого и доброго",
        "tense" => "напряжённое, хочется триллера или экшена",
        "romantic" => "романтическое",
        _ => "любое",
    };

    private const string RecommendationsSchema = """
        {
          "type": "object",
          "properties": {
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "title": { "type": "string" },
                  "reason": { "type": "string" },
                  "score": { "type": "number" }
                },
                "required": ["title", "reason", "score"],
                "additionalProperties": false
              }
            }
          },
          "required": ["items"],
          "additionalProperties": false
        }
        """;

    private const string ProcessVoiceSchema = """
        {
          "type": "object",
          "properties": {
            "processed_text": { "type": "string" },
            "suggestions": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["processed_text", "suggestions"],
          "additionalProperties": false
        }
        """;
}
