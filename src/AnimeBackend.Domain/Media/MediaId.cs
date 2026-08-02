using System.Text.RegularExpressions;

namespace AnimeBackend.Domain.Media;

// Wire format shared with the frontend: `<source>_<numericId>-<slug>`, e.g.
// `shikimori_52991-sousou-no-frieren`. The slug is ASCII-only because the
// frontend regex is `^(shikimori|aniliberty)_\d+-[\w-]+$`.
public readonly partial record struct MediaId(MediaSource Source, long NumericId, string Slug)
{
    public static MediaId Build(MediaSource source, long numericId, string title)
        => new(source, numericId, GenerateSlug(title));

    public static MediaId? TryParse(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var match = IdPattern().Match(value);
        if (!match.Success) return null;
        var source = MediaSourceExtensions.FromWire(match.Groups[1].Value)!.Value;
        return new MediaId(source, long.Parse(match.Groups[2].Value), match.Groups[3].Value);
    }

    public static MediaId Parse(string value)
        => TryParse(value) ?? throw new DomainException($"Некорректный идентификатор работы: {value}");

    // Port of the frontend slug generator: lowercase, non-[a-z0-9] runs become a
    // single dash. Fully non-Latin titles would collapse to an empty slug that
    // fails the shared regex, hence the fallback.
    public static string GenerateSlug(string title)
    {
        var slug = NonAlphanumeric().Replace(title.ToLowerInvariant(), "-").Trim('-');
        return slug.Length > 0 ? slug : "media";
    }

    public override string ToString() => $"{Source.ToWire()}_{NumericId}-{Slug}";

    [GeneratedRegex(@"^(shikimori|aniliberty)_(\d+)-([\w-]+)$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
