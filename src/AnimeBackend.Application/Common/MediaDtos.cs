using AnimeBackend.Domain.Media;
using AnimeBackend.Domain.Tags;

namespace AnimeBackend.Application.Common;

// Wire DTOs mirroring the frontend zod schemas (`src/apis/dtos/*.ts`).
// Property names are PascalCase here; the host serialises them as snake_case.
// Nulls are dropped from the payload, matching zod's `.optional()` fields.

public sealed record RelatedWorkDto(string Id, string Title, string Relation)
{
    public static RelatedWorkDto From(RelatedWork related) => new(related.Id, related.Title, related.Relation);
}

// `api_url` is optional on the wire: absent means "not known", and the client
// derives one of its own.
public sealed record ExternalLinkDto(string Source, string Url, string? ApiUrl = null)
{
    public static ExternalLinkDto From(ExternalLink link) => new(link.Source, link.Url, link.ApiUrl);

    public ExternalLink ToDomain() => new(Source, Url, ApiUrl);
}

public sealed record AnimeListItemDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleRussian { get; init; }
    public string? TitleJapanese { get; init; }
    public string? TitleEnglish { get; init; }
    public required int EpisodesTotal { get; init; }
    public double? Score { get; init; }
    public string? ImageUrl { get; init; }
    public string? Synopsis { get; init; }
    public IReadOnlyList<string>? Genres { get; init; }
    public string? AiredFrom { get; init; }
    public string? AiredTo { get; init; }
    public required IReadOnlyList<string> MyTags { get; init; }
    public required string Source { get; init; }
    public string? SecondarySource { get; init; }

    public static AnimeListItemDto From(MediaItem item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        TitleRussian = item.TitleRussian,
        TitleJapanese = item.TitleJapanese,
        TitleEnglish = item.TitleEnglish,
        EpisodesTotal = item.EpisodesTotal,
        Score = item.EffectiveScore,
        ImageUrl = item.ImageUrl,
        Synopsis = item.Synopsis,
        Genres = item.Genres.Count > 0 ? item.Genres : null,
        AiredFrom = item.AiredFrom,
        AiredTo = item.AiredTo,
        MyTags = TagIds(item),
        Source = item.Source.ToWire(),
        SecondarySource = item.SecondarySource?.ToWire(),
    };

    public static AnimeListItemDto From(MediaSnapshot snapshot, IReadOnlyList<string> myTags) => new()
    {
        Id = snapshot.Id.ToString(),
        Title = snapshot.Title,
        TitleRussian = snapshot.TitleRussian,
        TitleJapanese = snapshot.TitleJapanese,
        TitleEnglish = snapshot.TitleEnglish,
        EpisodesTotal = snapshot.EpisodesTotal,
        Score = snapshot.SourceScore,
        ImageUrl = snapshot.ImageUrl,
        Synopsis = snapshot.Synopsis,
        Genres = snapshot.Genres is { Count: > 0 } genres ? genres : null,
        AiredFrom = snapshot.AiredFrom,
        AiredTo = snapshot.AiredTo,
        MyTags = myTags,
        Source = snapshot.Source.ToWire(),
        SecondarySource = snapshot.SecondarySource?.ToWire(),
    };

    internal static IReadOnlyList<string> TagIds(MediaItem item)
        => [.. item.Tags.OrderBy(t => t.SortOrder).Select(t => t.Id.ToString())];
}

public sealed record AnimeDetailDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleRussian { get; init; }
    public string? TitleJapanese { get; init; }
    public string? TitleEnglish { get; init; }
    public required int EpisodesTotal { get; init; }
    public double? Score { get; init; }
    public string? ImageUrl { get; init; }
    public string? Synopsis { get; init; }
    public IReadOnlyList<string>? Genres { get; init; }
    public string? AiredFrom { get; init; }
    public string? AiredTo { get; init; }
    public required IReadOnlyList<string> MyTags { get; init; }
    public required string Source { get; init; }
    public string? SecondarySource { get; init; }
    public string? Rating { get; init; }
    public int? Duration { get; init; }
    public int? WatchedEpisodes { get; init; }
    public IReadOnlyList<RelatedWorkDto>? RelatedAnime { get; init; }
    public IReadOnlyList<ExternalLinkDto>? ExternalLinks { get; init; }

    public static AnimeDetailDto From(MediaItem item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        TitleRussian = item.TitleRussian,
        TitleJapanese = item.TitleJapanese,
        TitleEnglish = item.TitleEnglish,
        EpisodesTotal = item.EpisodesTotal,
        Score = item.EffectiveScore,
        ImageUrl = item.ImageUrl,
        Synopsis = item.Synopsis,
        Genres = item.Genres.Count > 0 ? item.Genres : null,
        AiredFrom = item.AiredFrom,
        AiredTo = item.AiredTo,
        MyTags = AnimeListItemDto.TagIds(item),
        Source = item.Source.ToWire(),
        SecondarySource = item.SecondarySource?.ToWire(),
        Rating = item.Rating,
        Duration = item.DurationMinutes,
        WatchedEpisodes = item.WatchedEpisodes,
        RelatedAnime = item.Related.Count > 0 ? [.. item.Related.Select(RelatedWorkDto.From)] : null,
        ExternalLinks = item.ExternalLinks.Count > 0 ? [.. item.ExternalLinks.Select(ExternalLinkDto.From)] : null,
    };

    public static AnimeDetailDto From(MediaSnapshot snapshot) => new()
    {
        Id = snapshot.Id.ToString(),
        Title = snapshot.Title,
        TitleRussian = snapshot.TitleRussian,
        TitleJapanese = snapshot.TitleJapanese,
        TitleEnglish = snapshot.TitleEnglish,
        EpisodesTotal = snapshot.EpisodesTotal,
        Score = snapshot.SourceScore,
        ImageUrl = snapshot.ImageUrl,
        Synopsis = snapshot.Synopsis,
        Genres = snapshot.Genres is { Count: > 0 } genres ? genres : null,
        AiredFrom = snapshot.AiredFrom,
        AiredTo = snapshot.AiredTo,
        MyTags = [],
        Source = snapshot.Source.ToWire(),
        SecondarySource = snapshot.SecondarySource?.ToWire(),
        Rating = snapshot.Rating,
        Duration = snapshot.DurationMinutes,
        WatchedEpisodes = 0,
        RelatedAnime = snapshot.Related is { Count: > 0 } related
            ? [.. related.Select(RelatedWorkDto.From)]
            : null,
        ExternalLinks = snapshot.ExternalLinks is { Count: > 0 } links
            ? [.. links.Select(ExternalLinkDto.From)]
            : null,
    };
}

public sealed record MangaListItemDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleRussian { get; init; }
    public string? TitleJapanese { get; init; }
    public string? TitleEnglish { get; init; }
    public required int VolumesTotal { get; init; }
    public required int ChaptersTotal { get; init; }
    public double? Score { get; init; }
    public string? ImageUrl { get; init; }
    public string? Synopsis { get; init; }
    public IReadOnlyList<string>? Genres { get; init; }
    public string? PublishedFrom { get; init; }
    public string? PublishedTo { get; init; }
    public required IReadOnlyList<string> MyTags { get; init; }
    public required string Source { get; init; }
    public string? SecondarySource { get; init; }

    public static MangaListItemDto From(MediaItem item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        TitleRussian = item.TitleRussian,
        TitleJapanese = item.TitleJapanese,
        TitleEnglish = item.TitleEnglish,
        VolumesTotal = item.VolumesTotal,
        ChaptersTotal = item.ChaptersTotal,
        Score = item.EffectiveScore,
        ImageUrl = item.ImageUrl,
        Synopsis = item.Synopsis,
        Genres = item.Genres.Count > 0 ? item.Genres : null,
        PublishedFrom = item.PublishedFrom,
        PublishedTo = item.PublishedTo,
        MyTags = AnimeListItemDto.TagIds(item),
        Source = item.Source.ToWire(),
        SecondarySource = item.SecondarySource?.ToWire(),
    };

    public static MangaListItemDto From(MediaSnapshot snapshot, IReadOnlyList<string> myTags) => new()
    {
        Id = snapshot.Id.ToString(),
        Title = snapshot.Title,
        TitleRussian = snapshot.TitleRussian,
        TitleJapanese = snapshot.TitleJapanese,
        TitleEnglish = snapshot.TitleEnglish,
        VolumesTotal = snapshot.VolumesTotal,
        ChaptersTotal = snapshot.ChaptersTotal,
        Score = snapshot.SourceScore,
        ImageUrl = snapshot.ImageUrl,
        Synopsis = snapshot.Synopsis,
        Genres = snapshot.Genres is { Count: > 0 } genres ? genres : null,
        PublishedFrom = snapshot.PublishedFrom,
        PublishedTo = snapshot.PublishedTo,
        MyTags = myTags,
        Source = snapshot.Source.ToWire(),
        SecondarySource = snapshot.SecondarySource?.ToWire(),
    };
}

public sealed record MangaDetailDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleRussian { get; init; }
    public string? TitleJapanese { get; init; }
    public string? TitleEnglish { get; init; }
    public required int VolumesTotal { get; init; }
    public required int ChaptersTotal { get; init; }
    public double? Score { get; init; }
    public string? ImageUrl { get; init; }
    public string? Synopsis { get; init; }
    public IReadOnlyList<string>? Genres { get; init; }
    public string? PublishedFrom { get; init; }
    public string? PublishedTo { get; init; }
    public required IReadOnlyList<string> MyTags { get; init; }
    public required string Source { get; init; }
    public string? SecondarySource { get; init; }
    public IReadOnlyList<string>? Authors { get; init; }
    public int? VolumesRead { get; init; }
    public int? ChaptersRead { get; init; }
    public IReadOnlyList<RelatedWorkDto>? RelatedManga { get; init; }
    public IReadOnlyList<ExternalLinkDto>? ExternalLinks { get; init; }

    public static MangaDetailDto From(MediaItem item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        TitleRussian = item.TitleRussian,
        TitleJapanese = item.TitleJapanese,
        TitleEnglish = item.TitleEnglish,
        VolumesTotal = item.VolumesTotal,
        ChaptersTotal = item.ChaptersTotal,
        Score = item.EffectiveScore,
        ImageUrl = item.ImageUrl,
        Synopsis = item.Synopsis,
        Genres = item.Genres.Count > 0 ? item.Genres : null,
        PublishedFrom = item.PublishedFrom,
        PublishedTo = item.PublishedTo,
        MyTags = AnimeListItemDto.TagIds(item),
        Source = item.Source.ToWire(),
        SecondarySource = item.SecondarySource?.ToWire(),
        Authors = item.Authors.Count > 0 ? item.Authors : null,
        VolumesRead = item.VolumesRead,
        ChaptersRead = item.ChaptersRead,
        RelatedManga = item.Related.Count > 0 ? [.. item.Related.Select(RelatedWorkDto.From)] : null,
        ExternalLinks = item.ExternalLinks.Count > 0 ? [.. item.ExternalLinks.Select(ExternalLinkDto.From)] : null,
    };

    public static MangaDetailDto From(MediaSnapshot snapshot) => new()
    {
        Id = snapshot.Id.ToString(),
        Title = snapshot.Title,
        TitleRussian = snapshot.TitleRussian,
        TitleJapanese = snapshot.TitleJapanese,
        TitleEnglish = snapshot.TitleEnglish,
        VolumesTotal = snapshot.VolumesTotal,
        ChaptersTotal = snapshot.ChaptersTotal,
        Score = snapshot.SourceScore,
        ImageUrl = snapshot.ImageUrl,
        Synopsis = snapshot.Synopsis,
        Genres = snapshot.Genres is { Count: > 0 } genres ? genres : null,
        PublishedFrom = snapshot.PublishedFrom,
        PublishedTo = snapshot.PublishedTo,
        MyTags = [],
        Source = snapshot.Source.ToWire(),
        SecondarySource = snapshot.SecondarySource?.ToWire(),
        Authors = snapshot.Authors is { Count: > 0 } authors ? authors : null,
        VolumesRead = 0,
        ChaptersRead = 0,
        RelatedManga = snapshot.Related is { Count: > 0 } related
            ? [.. related.Select(RelatedWorkDto.From)]
            : null,
        ExternalLinks = snapshot.ExternalLinks is { Count: > 0 } links
            ? [.. links.Select(ExternalLinkDto.From)]
            : null,
    };
}

public sealed record TagDto(string Id, string Name, string Color, string? Icon, bool IsHidden, int SortOrder)
{
    public static TagDto From(Tag tag)
        => new(tag.Id.ToString(), tag.Name, tag.Color, tag.Icon, tag.IsHidden, tag.SortOrder);
}

public sealed record NoteDto(string Id, string MediaId, string Content, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static NoteDto From(Domain.Notes.Note note)
        => new(note.Id.ToString(), note.MediaId, note.Content, note.CreatedAt, note.UpdatedAt);
}
