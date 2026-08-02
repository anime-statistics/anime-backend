using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AnimeBackend.Domain;
using Microsoft.Extensions.Logging;

namespace AnimeBackend.Application.Ai;

// The /ai group. Prompts are Russian because the catalogue, notes and UI are;
// replies follow the user's language regardless. The assistant is given tools
// rather than a dump of the collection — see AiToolbox for why.
public sealed class AiHandlers(
    IAiChat ai,
    AiConversation conversation,
    AiModelCatalog catalog,
    ILogger<AiHandlers> logger)
{
    private const string ChatSystemPrompt =
        "Ты ассистент персонального трекера аниме и манги. Отвечай на языке пользователя, "
        + "кратко и по делу, в Markdown.\n"
        + "У тебя есть инструменты для работы с коллекцией пользователя — пользуйся ими, "
        + "а не догадками: library_stats для общей картины, search_library для поиска по "
        + "коллекции, get_media_details для подробностей о конкретной работе, list_tags "
        + "для тегов, search_catalog для того, чего у пользователя ещё нет.\n"
        + "Про коллекцию пользователя говори только то, что вернули инструменты. Не выдумывай "
        + "названия, оценки и прогресс: если в результатах инструментов чего-то нет — значит, "
        + "этого нет и у пользователя. Рекомендуя незнакомое, сначала проверь его через "
        + "search_catalog. Спойлеры — только если пользователь прямо о них просит.";

    public Task<IReadOnlyList<AiModelDto>> ModelsAsync(string? query, bool all, CancellationToken ct)
        => catalog.ListAsync(query, all, ct);

    public void EnsureConfigured()
    {
        if (!ai.IsConfigured)
        {
            throw new NotFoundException(
                "AI-ассистент не подключён: задайте ключ RouterAI "
                + "(переменная окружения ROUTERAI_API_KEY или секрет Ai:ApiKey) "
                + "и перезапустите бэкенд");
        }
    }

    public async Task<AiChatResponseDto> ChatAsync(AiChatApiRequest request, CancellationToken ct)
    {
        EnsureConfigured();
        var chatRequest = await BuildChatRequestAsync(request, ct);
        var result = await conversation.RunAsync(chatRequest, ct);
        return new AiChatResponseDto(result.Text, new AiUsageDto(result.InputTokens, result.OutputTokens));
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        AiChatApiRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureConfigured();
        var chatRequest = await BuildChatRequestAsync(request, ct);
        await foreach (var chunk in conversation.StreamAsync(chatRequest, ct))
            yield return chunk;
    }

    public async Task<AiParaphraseResponseDto> ParaphraseAsync(
        AiParaphraseApiRequest request, CancellationToken ct)
    {
        EnsureConfigured();
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

        var model = await catalog.ResolveAsync(request.Model, ct);
        var result = await conversation.RunAsync(new AiChatRequest
        {
            Model = model?.Id ?? AiModelCatalog.FallbackModelId,
            System = "Ты редактор заметок в трекере аниме. " + styleInstruction
                     + " Сохрани язык оригинала и Markdown-разметку, если она есть."
                     + " В ответе верни ТОЛЬКО итоговый текст, без пояснений и кавычек.",
            Messages = [AiChatMessage.User(request.Text)],
            Temperature = Temperature(model, request.Temperature),
            MaxTokens = 4000,
        }, ct);

        return new AiParaphraseResponseDto(
            result.Text.Trim(),
            new AiUsageDto(result.InputTokens, result.OutputTokens));
    }

    // One phase, not two. Each round is a full generation, and a reasoning
    // model spends 30-90 seconds on each — an explore pass followed by a
    // separate formatting pass pushed this past any tolerable wait. So the
    // model explores and emits the JSON in the same conversation, and the
    // parser tolerates prose or fences around it.
    public async Task<AiRecommendationsResponseDto> RecommendationsAsync(
        AiRecommendationsApiRequest request, CancellationToken ct)
    {
        EnsureConfigured();

        var model = await catalog.ResolveAsync(request.Model, ct);

        var brief = new StringBuilder();
        brief.AppendLine("Подбери для пользователя 5 аниме, которых ещё нет в его коллекции.");
        brief.AppendLine($"Настроение: {MoodLabel(request.Mood)}.");
        if (!string.IsNullOrWhiteSpace(request.Prompt))
            brief.AppendLine($"Пожелание: {request.Prompt}");
        brief.AppendLine(
            "Порядок работы: посмотри вкусы через library_stats или search_library; "
            + "сам выбери 5 конкретных известных тайтлов, подходящих под запрос; "
            + "проверь каждый через search_catalog по его точному названию и возьми оттуда "
            + "media_id. Не ищи по жанру или настроению — только по названиям кандидатов. "
            + "Последним сообщением верни только JSON.");

        var result = await conversation.RunAsync(new AiChatRequest
        {
            Model = model?.Id ?? AiModelCatalog.FallbackModelId,
            System = "Ты рекомендательный движок аниме-трекера. Опирайся на реальную коллекцию "
                     + "пользователя и на результаты поиска по каталогу, а не на догадки. "
                     + "Закончив, верни ТОЛЬКО JSON без пояснений вокруг, например: "
                     + "{\"items\":[{\"media_id\":\"shikimori_9253-steins-gate\","
                     + "\"title\":\"Steins;Gate\",\"reason\":\"Медленный старт и сильная "
                     + "вторая половина — как в отмеченных вами драмах\",\"score\":0.9}]}. "
                     + "В reason пиши живое объяснение по-русски, привязанное к вкусам "
                     + "пользователя. media_id бери строго из результатов search_catalog; "
                     + "тайтл без найденного media_id не включай.",
            Messages = [AiChatMessage.User(brief.ToString())],
            Tools = AiToolbox.Definitions,
            Temperature = Temperature(model, request.Temperature),
            MaxTokens = 8000,
            MaxToolRounds = 2,
        }, ct);

        return new AiRecommendationsResponseDto(
            ParseRecommendations(result.Text),
            new AiUsageDto(result.InputTokens, result.OutputTokens));
    }

    public async Task<AiProcessVoiceResponseDto> ProcessVoiceAsync(
        AiProcessVoiceApiRequest request, CancellationToken ct)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new DomainException("Пустой текст распознавания");

        var model = await catalog.ResolveAsync(null, ct);
        var result = await conversation.RunAsync(new AiChatRequest
        {
            Model = model?.Id ?? AiModelCatalog.FallbackModelId,
            System = "Тебе дают сырой текст голосового ввода из аниме-трекера. "
                     + "В processed_text верни его причёсанным: убери слова-паразиты и оговорки, "
                     + "поправь пунктуацию, сохрани язык и смысл. "
                     + "В suggestions предложи до 5 названий аниме, которые пользователь мог "
                     + "иметь в виду; если непонятно — пустой список.",
            Messages = [AiChatMessage.User(request.Text)],
            JsonSchema = ProcessVoiceSchema,
            MaxTokens = 4000,
        }, ct);

        try
        {
            using var parsed = JsonDocument.Parse(ExtractJson(result.Text));
            var processed = parsed.RootElement.TryGetProperty("processed_text", out var text)
                ? text.GetString() ?? request.Text
                : request.Text;
            var suggestions = parsed.RootElement.TryGetProperty("suggestions", out var list)
                    && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray()
                    .Select(s => s.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .Take(5)
                    .ToList()
                : [];
            return new AiProcessVoiceResponseDto(processed.Trim(), suggestions);
        }
        catch (JsonException ex)
        {
            // A model that ignored the schema shouldn't break dictation.
            logger.LogWarning(ex, "Некорректный JSON от модели в process-voice");
            return new AiProcessVoiceResponseDto(request.Text.Trim(), []);
        }
    }

    private async Task<AiChatRequest> BuildChatRequestAsync(
        AiChatApiRequest request, CancellationToken ct)
    {
        if (request.Messages is not { Count: > 0 })
            throw new DomainException("Пустая история чата");

        var messages = request.Messages
            .Where(m => m.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new AiChatMessage { Role = m.Role!, Content = m.Content })
            .ToList();
        if (messages.Count == 0)
            throw new DomainException("Пустая история чата");

        var model = await catalog.ResolveAsync(request.Model, ct);
        var deepThink = request.DeepThink ?? false;

        return new AiChatRequest
        {
            Model = model?.Id ?? AiModelCatalog.FallbackModelId,
            System = ChatSystemPrompt,
            Messages = messages,
            Tools = AiToolbox.Definitions,
            Temperature = Temperature(model, request.Temperature),
            // Reasoning models spend part of this budget thinking before a
            // single visible character appears, so it has to be generous.
            MaxTokens = deepThink ? 16000 : 8192,
        };
    }

    // Reasoning models reject sampling parameters; the catalogue tells us which
    // ones accept temperature, so the UI slider is honoured where it works and
    // silently dropped where it doesn't.
    private static double? Temperature(AiModelInfo? model, double? requested)
        => model?.SupportsTemperature == true && requested is >= 0 and <= 2 ? requested : null;

    private List<AiRecommendationDto> ParseRecommendations(string json)
    {
        var results = new List<AiRecommendationDto>();
        try
        {
            using var parsed = JsonDocument.Parse(ExtractJson(json));
            if (!parsed.RootElement.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var item in items.EnumerateArray())
            {
                var mediaId = item.TryGetProperty("media_id", out var id) ? id.GetString() : null;
                var title = item.TryGetProperty("title", out var name) ? name.GetString() : null;
                // No real catalogue id means the card wouldn't open — drop it
                // rather than ship an invented one.
                if (string.IsNullOrWhiteSpace(mediaId) || string.IsNullOrWhiteSpace(title)) continue;

                results.Add(new AiRecommendationDto(
                    mediaId,
                    title,
                    item.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : "",
                    item.TryGetProperty("score", out var score) && score.TryGetDouble(out var value)
                        ? Math.Clamp(value, 0, 1)
                        : 0.5));
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Некорректный JSON от модели в recommendations");
        }
        return results;
    }

    // Some models wrap JSON in prose or a fenced block even when asked not to.
    private static string ExtractJson(string text)
    {
        var trimmed = text.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : trimmed;
    }

    private static string MoodLabel(string? mood) => mood switch
    {
        "sad" => "грустное, хочется драмы",
        "happy" => "весёлое, хочется лёгкого и доброго",
        "tense" => "напряжённое, хочется триллера или экшена",
        "romantic" => "романтическое",
        _ => "любое",
    };

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
