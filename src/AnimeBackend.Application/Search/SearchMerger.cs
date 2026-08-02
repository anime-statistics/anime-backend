using AnimeBackend.Domain.Media;

namespace AnimeBackend.Application.Search;

// Port of the frontend mock's `src/mocks/search/mergeResults.ts` — the
// behavioural contract for collapsing the same title arriving from two
// sources. Weights, threshold and the "missing components are skipped, not
// penalised" rule are identical; the only deliberate difference is candidate
// selection: the mock pre-filters with fuse.js purely as an optimisation,
// while here result sets are small enough to score every cross-source pair.
public static class SearchMerger
{
    private const double SimilarityThreshold = 0.7;

    private const double TitleWeight = 0.5;
    private const double TitleEnglishWeight = 0.2;
    private const double YearWeight = 0.15;
    private const double EpisodesWeight = 0.15;
    private const double GenresWeight = 0.15;

    // A merged card plus every source id that went into it, so the caller can
    // look up collection tags for both constituents.
    public sealed record Merged(MediaSnapshot Snapshot, IReadOnlyList<string> ConstituentIds);

    public static List<Merged> Deduplicate(IReadOnlyList<MediaSnapshot> items)
    {
        var merged = new List<Merged>(items.Count);
        var used = new bool[items.Count];

        for (var i = 0; i < items.Count; i++)
        {
            if (used[i]) continue;
            used[i] = true;

            var current = items[i];
            var duplicateIndex = -1;
            var bestScore = SimilarityThreshold;

            for (var j = 0; j < items.Count; j++)
            {
                if (used[j] || items[j].Source == current.Source) continue;
                var score = ComputeSimilarity(current, items[j]);
                if (score > bestScore)
                {
                    bestScore = score;
                    duplicateIndex = j;
                }
            }

            if (duplicateIndex < 0)
            {
                merged.Add(new Merged(current, [current.Id.ToString()]));
                continue;
            }

            used[duplicateIndex] = true;
            var duplicate = items[duplicateIndex];
            var combined = MergeDuplicates(current, duplicate);
            // Primary id first: collection tags are looked up in that order.
            var primaryId = combined.Id.ToString();
            var secondaryId = current.Id.ToString() == primaryId
                ? duplicate.Id.ToString()
                : current.Id.ToString();
            merged.Add(new Merged(combined, [primaryId, secondaryId]));
        }

        return merged;
    }

    public static double ComputeSimilarity(MediaSnapshot left, MediaSnapshot right)
    {
        var components = new (double Weight, double? Score)[]
        {
            (TitleWeight, TitleSimilarity(left.Title, right.Title)),
            (TitleEnglishWeight, TitleSimilarity(left.TitleEnglish, right.TitleEnglish)),
            (YearWeight, YearSimilarity(StartDate(left), StartDate(right))),
            (EpisodesWeight, CountSimilarity(left, right)),
            (GenresWeight, GenresSimilarity(left.Genres, right.Genres)),
        };

        double weightedScore = 0, usedWeight = 0;
        foreach (var (weight, score) in components)
        {
            if (score is null) continue;
            weightedScore += weight * score.Value;
            usedWeight += weight;
        }

        return usedWeight == 0 ? 0 : weightedScore / usedWeight;
    }

    // The primary record is Shikimori's; its present fields win and gaps are
    // filled from the secondary — the mock's `{...secondary, ...primary}`.
    private static MediaSnapshot MergeDuplicates(MediaSnapshot left, MediaSnapshot right)
    {
        var primary = right.Source == MediaSource.Shikimori && left.Source != MediaSource.Shikimori
            ? right
            : left;
        var secondary = ReferenceEquals(primary, left) ? right : left;

        return primary with
        {
            TitleRussian = primary.TitleRussian ?? secondary.TitleRussian,
            TitleJapanese = primary.TitleJapanese ?? secondary.TitleJapanese,
            TitleEnglish = primary.TitleEnglish ?? secondary.TitleEnglish,
            SourceScore = primary.SourceScore ?? secondary.SourceScore,
            ImageUrl = primary.ImageUrl ?? secondary.ImageUrl,
            Synopsis = primary.Synopsis ?? secondary.Synopsis,
            Genres = primary.Genres ?? secondary.Genres,
            AiredFrom = primary.AiredFrom ?? secondary.AiredFrom,
            AiredTo = primary.AiredTo ?? secondary.AiredTo,
            PublishedFrom = primary.PublishedFrom ?? secondary.PublishedFrom,
            PublishedTo = primary.PublishedTo ?? secondary.PublishedTo,
            Rating = primary.Rating ?? secondary.Rating,
            DurationMinutes = primary.DurationMinutes ?? secondary.DurationMinutes,
            Authors = primary.Authors ?? secondary.Authors,
            Related = primary.Related ?? secondary.Related,
            ExternalLinks = primary.ExternalLinks ?? secondary.ExternalLinks,
            SecondarySource = secondary.Source == primary.Source ? null : secondary.Source,
        };
    }

    private static double? TitleSimilarity(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return null;
        return DiceCoefficient(NormaliseTitle(left), NormaliseTitle(right));
    }

    private static double? YearSimilarity(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return null;
        return Slice4(left) == Slice4(right) ? 1 : 0;

        static string Slice4(string value) => value.Length <= 4 ? value : value[..4];
    }

    // Episode (or, for manga, chapter) counts compare by exact equality and are
    // always present — zero matches zero, mirroring the mock.
    private static double CountSimilarity(MediaSnapshot left, MediaSnapshot right)
        => left.Type == MediaType.Manga
            ? left.ChaptersTotal == right.ChaptersTotal ? 1 : 0
            : left.EpisodesTotal == right.EpisodesTotal ? 1 : 0;

    private static double? GenresSimilarity(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        if (left is not { Count: > 0 } || right is not { Count: > 0 }) return null;
        return DiceCoefficient(
            left.Select(g => g.ToLowerInvariant()),
            right.Select(g => g.ToLowerInvariant()));
    }

    private static string StartDate(MediaSnapshot snapshot)
        => (snapshot.Type == MediaType.Manga ? snapshot.PublishedFrom : snapshot.AiredFrom) ?? "";

    private static IEnumerable<string> NormaliseTitle(string title)
        => title.ToLowerInvariant()
            .Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : ' ')
            .Aggregate(new System.Text.StringBuilder(), (sb, c) => sb.Append(c))
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static double? DiceCoefficient(IEnumerable<string> left, IEnumerable<string> right)
    {
        var leftSet = left.ToHashSet();
        var rightSet = right.ToHashSet();
        if (leftSet.Count == 0 || rightSet.Count == 0) return null;

        var shared = leftSet.Count(rightSet.Contains);
        return 2.0 * shared / (leftSet.Count + rightSet.Count);
    }
}
