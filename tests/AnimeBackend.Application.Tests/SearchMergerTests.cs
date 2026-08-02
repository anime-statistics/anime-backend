using AnimeBackend.Application.Search;
using AnimeBackend.Domain.Media;

namespace AnimeBackend.Application.Tests;

public class SearchMergerTests
{
    private static MediaSnapshot Anime(
        MediaSource source,
        long id,
        string title,
        string? titleEnglish = null,
        int episodes = 12,
        string? airedFrom = null,
        IReadOnlyList<string>? genres = null,
        string? synopsis = null,
        string? imageUrl = null)
        => new()
        {
            Id = MediaId.Build(source, id, title),
            Type = MediaType.Anime,
            Title = title,
            TitleEnglish = titleEnglish,
            EpisodesTotal = episodes,
            AiredFrom = airedFrom,
            Genres = genres,
            Synopsis = synopsis,
            ImageUrl = imageUrl,
        };

    [Fact]
    public void MergesSameTitleFromDifferentSources()
    {
        var shiki = Anime(MediaSource.Shikimori, 1, "Sousou no Frieren", episodes: 28);
        var liberty = Anime(MediaSource.Aniliberty, 2, "Sousou no Frieren", episodes: 28);

        var merged = SearchMerger.Deduplicate([liberty, shiki]);

        Assert.Single(merged);
        Assert.Equal(MediaSource.Shikimori, merged[0].Snapshot.Source);
        Assert.Equal(MediaSource.Aniliberty, merged[0].Snapshot.SecondarySource);
    }

    [Fact]
    public void NeverMergesRecordsOfTheSameSource()
    {
        var first = Anime(MediaSource.Shikimori, 1, "Steins Gate", episodes: 24);
        var second = Anime(MediaSource.Shikimori, 2, "Steins Gate", episodes: 24);

        var merged = SearchMerger.Deduplicate([first, second]);

        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void PunctuationDifferencesStillMerge()
    {
        // "Steins;Gate" vs "Steins Gate": normalisation strips punctuation.
        var shiki = Anime(MediaSource.Shikimori, 1, "Steins;Gate", episodes: 24);
        var liberty = Anime(MediaSource.Aniliberty, 2, "Steins Gate", episodes: 24);

        var merged = SearchMerger.Deduplicate([shiki, liberty]);

        Assert.Single(merged);
        Assert.Equal("shikimori_1-steins-gate", merged[0].Snapshot.Id.ToString());
    }

    [Fact]
    public void DifferentTitlesStayApart()
    {
        var shiki = Anime(MediaSource.Shikimori, 1, "Naruto", episodes: 220);
        var liberty = Anime(MediaSource.Aniliberty, 2, "Bleach", episodes: 366);

        var merged = SearchMerger.Deduplicate([shiki, liberty]);

        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void MissingComponentsAreSkippedNotPenalised()
    {
        // Only title + episodes known on one side: 0.5*1 + 0.15*1 over weight
        // 0.65 = 1.0, well above the 0.7 threshold.
        var shiki = Anime(MediaSource.Shikimori, 1, "Frieren", episodes: 28,
            airedFrom: "2023-09-29", genres: ["Приключения", "Драма"]);
        var liberty = Anime(MediaSource.Aniliberty, 2, "Frieren", episodes: 28);

        var merged = SearchMerger.Deduplicate([shiki, liberty]);

        Assert.Single(merged);
    }

    [Fact]
    public void EpisodeMismatchAlonePushesBelowThresholdWhenYearDisagrees()
    {
        // Same one-word title but different episode count and year:
        // (0.5*1 + 0.15*0 + 0.15*0) / 0.8 = 0.625 < 0.7 — stays apart.
        var shiki = Anime(MediaSource.Shikimori, 1, "Monogatari", episodes: 12, airedFrom: "2009-07-03");
        var liberty = Anime(MediaSource.Aniliberty, 2, "Monogatari", episodes: 26, airedFrom: "2013-07-06");

        var merged = SearchMerger.Deduplicate([shiki, liberty]);

        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void PrimaryFieldsWinGapsAreFilledFromSecondary()
    {
        var shiki = Anime(MediaSource.Shikimori, 1, "Frieren", episodes: 28,
            synopsis: null, imageUrl: null);
        var liberty = Anime(MediaSource.Aniliberty, 2, "Frieren", episodes: 28,
            synopsis: "Описание от АниЛиберти", imageUrl: "https://anilibria.top/poster.webp");

        var merged = SearchMerger.Deduplicate([liberty, shiki]);

        Assert.Single(merged);
        var snapshot = merged[0].Snapshot;
        Assert.Equal(MediaSource.Shikimori, snapshot.Source);
        Assert.Equal("Описание от АниЛиберти", snapshot.Synopsis);
        Assert.Equal("https://anilibria.top/poster.webp", snapshot.ImageUrl);
    }

    [Fact]
    public void ConstituentIdsListPrimaryFirst()
    {
        var liberty = Anime(MediaSource.Aniliberty, 2, "Frieren", episodes: 28);
        var shiki = Anime(MediaSource.Shikimori, 1, "Frieren", episodes: 28);

        // AniLiberty row comes first in the raw list, yet the primary id must
        // lead so tag lookup prefers the Shikimori record.
        var merged = SearchMerger.Deduplicate([liberty, shiki]);

        Assert.Single(merged);
        Assert.Equal(
            ["shikimori_1-frieren", "aniliberty_2-frieren"],
            merged[0].ConstituentIds);
    }

    [Fact]
    public void GenresCompareCaseInsensitively()
    {
        var left = Anime(MediaSource.Shikimori, 1, "Test Title", episodes: 10,
            genres: ["Драма", "Фэнтези"]);
        var right = Anime(MediaSource.Aniliberty, 2, "Test Title", episodes: 10,
            genres: ["драма", "фэнтези"]);

        Assert.Equal(1.0, SearchMerger.ComputeSimilarity(left, right), 3);
    }
}
