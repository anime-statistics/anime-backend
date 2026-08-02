namespace AnimeBackend.Domain.Media;

// A point-in-time view of one work as an external catalogue reports it. This is
// what source clients return for search and detail requests; nothing here is
// user state. Optional fields are null when the source does not know them —
// merging keeps the primary record's value and fills the gaps from the
// secondary one, mirroring the frontend mock's `{...secondary, ...primary}`.
public sealed record MediaSnapshot
{
    public required MediaId Id { get; init; }
    public required MediaType Type { get; init; }
    public required string Title { get; init; }
    public string? TitleRussian { get; init; }
    public string? TitleJapanese { get; init; }
    public string? TitleEnglish { get; init; }

    // Zero is meaningful: an announced or ongoing title with no known count.
    public int EpisodesTotal { get; init; }
    public int VolumesTotal { get; init; }
    public int ChaptersTotal { get; init; }

    public double? SourceScore { get; init; }
    public string? ImageUrl { get; init; }
    public string? Synopsis { get; init; }
    public IReadOnlyList<string>? Genres { get; init; }
    public string? AiredFrom { get; init; }
    public string? AiredTo { get; init; }
    public string? PublishedFrom { get; init; }
    public string? PublishedTo { get; init; }
    public string? Rating { get; init; }
    public int? DurationMinutes { get; init; }
    public IReadOnlyList<string>? Authors { get; init; }
    public IReadOnlyList<RelatedWork>? Related { get; init; }
    public IReadOnlyList<ExternalLink>? ExternalLinks { get; init; }

    public MediaSource Source => Id.Source;

    // Set only by the merge step, never by a client.
    public MediaSource? SecondarySource { get; init; }
}

public enum MediaType
{
    Anime,
    Manga,
}
