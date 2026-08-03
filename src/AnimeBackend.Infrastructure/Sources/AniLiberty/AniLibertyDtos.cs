namespace AnimeBackend.Infrastructure.Sources.AniLiberty;

// Shapes of https://aniliberty.top/api/v1 responses; only the fields this
// app reads. Deserialised with a snake_case naming policy.

internal sealed record AlName(string? Main, string? English, string? Alternative);

internal sealed record AlPosterVariant(string? Src, string? Preview, string? Thumbnail);

internal sealed record AlPoster(string? Src, string? Preview, string? Thumbnail, AlPosterVariant? Optimized);

internal sealed record AlAgeRating(string? Value, string? Label);

internal sealed record AlGenre(long Id, string? Name);

internal sealed record AlRelease(
    long Id,
    AlName? Name,
    string? Alias,
    int? Year,
    AlPoster? Poster,
    string? Description,
    int? EpisodesTotal,
    int? AverageDurationOfEpisode,
    AlAgeRating? AgeRating,
    bool? IsOngoing,
    IReadOnlyList<AlGenre>? Genres);

internal sealed record AlPagination(int? Total, int? CurrentPage, int? TotalPages);

internal sealed record AlMeta(AlPagination? Pagination);

internal sealed record AlCatalogResponse(IReadOnlyList<AlRelease>? Data, AlMeta? Meta);
