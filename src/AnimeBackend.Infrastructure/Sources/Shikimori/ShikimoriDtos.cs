namespace AnimeBackend.Infrastructure.Sources.Shikimori;

// Shapes of https://shikimori.one/api REST responses; only the fields this app
// reads. Deserialised with a snake_case naming policy.

internal sealed record ShikiImage(string? Original, string? Preview);

internal sealed record ShikiListItem(
    long Id,
    string Name,
    string? Russian,
    ShikiImage? Image,
    string? Score,
    int? Episodes,
    int? EpisodesAired,
    string? AiredOn,
    string? ReleasedOn,
    int? Volumes,
    int? Chapters,
    string? Kind,
    string? Status);

internal sealed record ShikiGenre(long Id, string? Name, string? Russian, string? Kind);

internal sealed record ShikiDetail(
    long Id,
    string Name,
    string? Russian,
    ShikiImage? Image,
    string? Score,
    int? Episodes,
    int? EpisodesAired,
    string? AiredOn,
    string? ReleasedOn,
    int? Volumes,
    int? Chapters,
    string? Kind,
    string? Status,
    IReadOnlyList<string?>? English,
    IReadOnlyList<string?>? Japanese,
    string? Description,
    int? Duration,
    string? Rating,
    IReadOnlyList<ShikiGenre>? Genres);

internal sealed record ShikiRelated(
    string? Relation,
    string? RelationRussian,
    ShikiListItem? Anime,
    ShikiListItem? Manga);

internal sealed record ShikiExternalLink(string? Kind, string? Url);
