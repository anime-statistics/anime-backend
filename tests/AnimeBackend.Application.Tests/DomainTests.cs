using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;
using AnimeBackend.Domain.Tags;

namespace AnimeBackend.Application.Tests;

public class DomainTests
{
    private static MediaItem AnimeItem() => MediaItem.FromSnapshot(new MediaSnapshot
    {
        Id = MediaId.Build(MediaSource.Shikimori, 1, "Test Anime"),
        Type = MediaType.Anime,
        Title = "Test Anime",
        EpisodesTotal = 12,
    });

    private static MediaItem MangaItem() => MediaItem.FromSnapshot(new MediaSnapshot
    {
        Id = MediaId.Build(MediaSource.Shikimori, 2, "Test Manga"),
        Type = MediaType.Manga,
        Title = "Test Manga",
        ChaptersTotal = 100,
    });

    private static Tag MakeTag(string name = "Тег", int sortOrder = 0)
        => new(Guid.NewGuid(), name, "#aabbcc", null, false, sortOrder);

    [Theory]
    [InlineData(-1)]
    [InlineData(10.5)]
    public void ScoreOutsideRangeIsRejected(double score)
        => Assert.Throws<DomainException>(() => AnimeItem().UpdateProgress(score, null, null, null));

    [Fact]
    public void MangaRejectsWatchedEpisodes()
        => Assert.Throws<DomainException>(() => MangaItem().UpdateProgress(null, 5, null, null));

    [Fact]
    public void AnimeRejectsVolumesRead()
        => Assert.Throws<DomainException>(() => AnimeItem().UpdateProgress(null, null, 1, null));

    [Fact]
    public void ProgressUpdatesOnlyProvidedFields()
    {
        var item = AnimeItem();
        item.UpdateProgress(8, 5, null, null);
        item.UpdateProgress(null, 7, null, null);

        Assert.Equal(8, item.UserScore);
        Assert.Equal(7, item.WatchedEpisodes);
    }

    [Fact]
    public void UserScoreWinsOverSourceScore()
    {
        var item = MediaItem.FromSnapshot(new MediaSnapshot
        {
            Id = MediaId.Build(MediaSource.Shikimori, 3, "Scored"),
            Type = MediaType.Anime,
            Title = "Scored",
            SourceScore = 9.13,
        });

        Assert.Equal(9.13, item.EffectiveScore);
        item.UpdateProgress(6, null, null, null);
        Assert.Equal(6, item.EffectiveScore);
    }

    [Fact]
    public void BulkClearOverridesAddAndCountsOnlyChangedItems()
    {
        var tag = MakeTag();
        var tagged = AnimeItem();
        tagged.ReplaceTags([tag]);
        var untagged = AnimeItem();

        Assert.True(tagged.ApplyBulk([tag], new HashSet<Guid>(), clear: true));
        Assert.False(untagged.ApplyBulk([tag], new HashSet<Guid>(), clear: true));
        Assert.Empty(tagged.Tags);
    }

    [Fact]
    public void BulkRemovesThenAddsWithoutDuplicates()
    {
        var stay = MakeTag("Остаётся");
        var gone = MakeTag("Уходит", 1);
        var item = AnimeItem();
        item.ReplaceTags([stay, gone]);

        var changed = item.ApplyBulk([stay], new HashSet<Guid> { gone.Id }, clear: false);

        Assert.True(changed);
        Assert.Single(item.Tags);
        Assert.Equal(stay.Id, item.Tags.Single().Id);
    }

    [Fact]
    public void BulkIsNoOpWhenNothingMatches()
    {
        var tag = MakeTag();
        var item = AnimeItem();
        item.ReplaceTags([tag]);

        var changed = item.ApplyBulk([tag], new HashSet<Guid>(), clear: false);

        Assert.False(changed);
    }

    [Theory]
    [InlineData("", "#aabbcc")]
    [InlineData("Имя тега длиной больше пятидесяти символов для проверки границы", "#aabbcc")]
    [InlineData("Тег", "красный")]
    [InlineData("Тег", "#aabbc")]
    public void TagValidationRejectsBadInput(string name, string color)
        => Assert.Throws<DomainException>(() => new Tag(Guid.NewGuid(), name, color, null, false, 0));

    [Fact]
    public void RemovingLastTagKeepsUserData()
    {
        var item = AnimeItem();
        item.UpdateProgress(9, 12, null, null);
        item.ReplaceTags([MakeTag()]);
        item.ReplaceTags([]);

        Assert.Empty(item.Tags);
        Assert.Equal(9, item.UserScore);
        Assert.Equal(12, item.WatchedEpisodes);
    }
}
