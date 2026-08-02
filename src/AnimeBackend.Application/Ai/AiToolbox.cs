using System.Text.Json;
using AnimeBackend.Application.Abstractions;
using AnimeBackend.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnimeBackend.Application.Ai;

// What the assistant can look up on its own. Dumping the whole collection into
// the prompt is both expensive and vague — with tools the model asks for what
// it actually needs, so a 500-title library costs the same as a 5-title one.
public sealed class AiToolbox(
    IAppDb db,
    IEnumerable<ISourceClient> sources,
    ILogger<AiToolbox> logger)
{
    private const int MaxRows = 30;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static readonly IReadOnlyList<AiToolDefinition> Definitions =
    [
        new("library_stats",
            "Сводка по коллекции пользователя: сколько аниме и манги, сколько просмотрено, "
            + "любимые жанры, средняя оценка. Вызывай первым, если нужен общий контекст.",
            """{"type":"object","properties":{},"required":[]}"""),

        new("search_library",
            "Поиск по коллекции пользователя (только то, что он себе добавил). "
            + "Возвращает названия, жанры, оценку, прогресс и теги.",
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string", "description": "Часть названия на любом языке; пусто — вернуть всё подряд" },
                "type": { "type": "string", "enum": ["anime", "manga"], "description": "По умолчанию аниме" },
                "tag": { "type": "string", "description": "Название тега, например «Просмотрено»" },
                "genre": { "type": "string", "description": "Жанр по-русски, например «Драма»" },
                "limit": { "type": "integer", "description": "Сколько вернуть, максимум 30" }
              },
              "required": []
            }
            """),

        new("get_media_details",
            "Полная карточка одной работы из коллекции: описание, прогресс, оценка, теги, заметки.",
            """
            {
              "type": "object",
              "properties": {
                "media_id": { "type": "string", "description": "Идентификатор вида shikimori_52991-sousou-no-frieren" }
              },
              "required": ["media_id"]
            }
            """),

        new("list_tags",
            "Теги пользователя и сколько работ в каждом.",
            """{"type":"object","properties":{},"required":[]}"""),

        new("search_catalog",
            "Поиск по внешним каталогам (Shikimori, AniLiberty) — то, чего у пользователя ещё нет. "
            + "Используй, чтобы проверить, существует ли тайтл, и получить его идентификатор.",
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string", "description": "Название на любом языке" },
                "type": { "type": "string", "enum": ["anime", "manga"] }
              },
              "required": ["query"]
            }
            """),
    ];

    public async Task<string> ExecuteAsync(string name, string argumentsJson, CancellationToken ct)
    {
        try
        {
            using var args = ParseArguments(argumentsJson);
            var root = args.RootElement;
            return name switch
            {
                "library_stats" => await LibraryStatsAsync(ct),
                "search_library" => await SearchLibraryAsync(root, ct),
                "get_media_details" => await GetMediaDetailsAsync(root, ct),
                "list_tags" => await ListTagsAsync(ct),
                "search_catalog" => await SearchCatalogAsync(root, ct),
                _ => Error($"Неизвестный инструмент: {name}"),
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Tool failures go back to the model as data, not as exceptions —
            // it can then explain or try a different approach.
            logger.LogWarning(ex, "Инструмент {Tool} упал", name);
            return Error(ex.Message);
        }
    }

    private static JsonDocument ParseArguments(string argumentsJson)
        => JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);

    private async Task<string> LibraryStatsAsync(CancellationToken ct)
    {
        var items = await db.MediaItems
            .Where(m => m.Tags.Count > 0)
            .Select(m => new { m.Type, m.Genres, m.UserScore, m.WatchedEpisodes, m.DurationMinutes, m.EpisodesTotal })
            .ToListAsync(ct);

        var anime = items.Where(i => i.Type == MediaType.Anime).ToList();
        var scored = items.Where(i => i.UserScore is > 0).Select(i => i.UserScore!.Value).ToList();

        var genres = items
            .SelectMany(i => i.Genres)
            .GroupBy(g => g)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g => new { genre = g.Key, count = g.Count() });

        return Serialize(new
        {
            anime_count = anime.Count,
            manga_count = items.Count - anime.Count,
            watched_episodes = anime.Sum(i => i.WatchedEpisodes),
            watched_minutes = anime.Sum(i => (long)i.WatchedEpisodes * (i.DurationMinutes ?? 0)),
            average_score = scored.Count > 0 ? Math.Round(scored.Average(), 2) : (double?)null,
            top_genres = genres,
        });
    }

    private async Task<string> SearchLibraryAsync(JsonElement args, CancellationToken ct)
    {
        var type = ReadString(args, "type") == "manga" ? MediaType.Manga : MediaType.Anime;
        var limit = Math.Clamp(ReadInt(args, "limit") ?? 10, 1, MaxRows);

        var query = db.MediaItems.Where(m => m.Type == type && m.Tags.Count > 0);

        if (ReadString(args, "query") is { Length: > 0 } needle)
        {
            var lowered = needle.ToLowerInvariant();
            query = query.Where(m => m.SearchText.Contains(lowered));
        }

        if (ReadString(args, "tag") is { Length: > 0 } tagName)
            query = query.Where(m => m.Tags.Any(t => t.Name == tagName));

        var rows = await query
            .OrderByDescending(m => m.UserScore ?? 0)
            .ThenBy(m => m.Title)
            .Take(MaxRows)
            .Select(m => new
            {
                media_id = m.Id,
                title = m.Title,
                title_russian = m.TitleRussian,
                genres = m.Genres,
                score = m.UserScore ?? m.SourceScore,
                episodes_total = m.EpisodesTotal,
                watched_episodes = m.WatchedEpisodes,
                chapters_total = m.ChaptersTotal,
                chapters_read = m.ChaptersRead,
                tags = m.Tags.Select(t => t.Name).ToList(),
            })
            .ToListAsync(ct);

        // Genre filtering happens client-side: genres live in a JSON column.
        if (ReadString(args, "genre") is { Length: > 0 } genre)
        {
            rows = [.. rows.Where(r => r.genres.Any(g =>
                g.Contains(genre, StringComparison.OrdinalIgnoreCase)))];
        }

        return Serialize(new { items = rows.Take(limit), total_found = rows.Count });
    }

    private async Task<string> GetMediaDetailsAsync(JsonElement args, CancellationToken ct)
    {
        var mediaId = ReadString(args, "media_id");
        if (string.IsNullOrWhiteSpace(mediaId)) return Error("Не указан media_id");

        var item = await db.MediaItems
            .Include(m => m.Tags)
            .FirstOrDefaultAsync(m => m.Id == mediaId, ct);
        if (item is null) return Error($"Работа {mediaId} не найдена в коллекции");

        var notes = await db.Notes
            .Where(n => n.MediaId == mediaId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .Select(n => n.Content)
            .ToListAsync(ct);

        return Serialize(new
        {
            media_id = item.Id,
            title = item.Title,
            title_russian = item.TitleRussian,
            type = item.Type.ToString().ToLowerInvariant(),
            synopsis = Truncate(item.Synopsis, 1200),
            genres = item.Genres,
            score = item.UserScore ?? item.SourceScore,
            user_score = item.UserScore,
            episodes_total = item.EpisodesTotal,
            watched_episodes = item.WatchedEpisodes,
            chapters_total = item.ChaptersTotal,
            chapters_read = item.ChaptersRead,
            aired_from = item.AiredFrom,
            tags = item.Tags.Select(t => t.Name),
            notes = notes.Select(n => Truncate(n, 500)),
        });
    }

    private async Task<string> ListTagsAsync(CancellationToken ct)
    {
        var tags = await db.Tags
            .OrderBy(t => t.SortOrder)
            .Select(t => new { name = t.Name, count = t.MediaItems.Count })
            .ToListAsync(ct);
        return Serialize(new { items = tags });
    }

    private async Task<string> SearchCatalogAsync(JsonElement args, CancellationToken ct)
    {
        var query = ReadString(args, "query");
        if (string.IsNullOrWhiteSpace(query)) return Error("Не указан query");
        var type = ReadString(args, "type") == "manga" ? MediaType.Manga : MediaType.Anime;

        // Sources are queried one after another on purpose. Fanning these out
        // across tasks deadlocked the request — the tool work finished but the
        // await never returned — so the modest latency is the price of a call
        // that always comes back.
        var found = new List<object>();
        foreach (var source in sources.OrderBy(s => s.Source))
        {
            try
            {
                var hits = await source.SearchAsync(query, type, ct);
                found.AddRange(hits.Take(5).Select(h => new
                {
                    media_id = h.Id.ToString(),
                    title = h.Title,
                    title_russian = h.TitleRussian,
                    genres = h.Genres,
                    score = h.SourceScore,
                    episodes_total = h.EpisodesTotal,
                    aired_from = h.AiredFrom,
                    source = h.Source.ToWire(),
                }));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Источник {Source} не ответил в search_catalog", source.Source);
            }
        }

        return Serialize(new { items = found });
    }

    private static string? ReadString(JsonElement args, string name)
        => args.ValueKind == JsonValueKind.Object
           && args.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static string? Truncate(string? text, int max)
        => text is null || text.Length <= max ? text : text[..max] + "…";

    private static string Serialize(object value) => JsonSerializer.Serialize(value, Json);

    private static string Error(string message) => Serialize(new { error = message });
}
