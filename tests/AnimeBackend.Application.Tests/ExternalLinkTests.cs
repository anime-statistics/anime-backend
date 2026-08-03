using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;

namespace AnimeBackend.Application.Tests;

public class ExternalLinkTests
{
    private static MediaSnapshot Snapshot(params ExternalLink[] links) => new()
    {
        Id = MediaId.Build(MediaSource.Shikimori, 52991, "Frieren"),
        Type = MediaType.Anime,
        Title = "Frieren",
        ExternalLinks = links.Length > 0 ? links : null,
    };

    // The table from the frontend's `ensureApiUrl`, which this must agree with
    // line for line — the client derives the same value when the field is absent.
    [Theory]
    [InlineData("https://shikimori.one/animes/5114", "https://shikimori.one/api/animes/5114")]
    [InlineData("https://shikimori.one/api/animes/5114", "https://shikimori.one/api/animes/5114")]
    [InlineData("https://aniliberty.top/apiary/anime", "https://aniliberty.top/api/apiary/anime")]
    [InlineData("https://aniliberty.top", "https://aniliberty.top/api")]
    [InlineData("https://aniliberty.top/anime/frieren?season=1", "https://aniliberty.top/api/anime/frieren?season=1")]
    public void ApiUrlIsThePageUrlUnderApi(string url, string expected)
        => Assert.Equal(expected, ExternalLink.DeriveApiUrl(url));

    [Theory]
    [InlineData("")]
    [InlineData("shikimori.one/animes/5114")]
    [InlineData("ftp://shikimori.one/animes/5114")]
    [InlineData(null)]
    public void ApiUrlOfSomethingThatIsNotAnHttpAddressIsUnknown(string? url)
        => Assert.Null(ExternalLink.DeriveApiUrl(url));

    [Fact]
    public void MissingApiUrlIsFilledInOnSave()
    {
        var item = MediaItem.FromSnapshot(Snapshot());

        item.ReplaceExternalLinks([new ExternalLink("shikimori", "https://shikimori.io/animes/52991")]);

        Assert.Equal("https://shikimori.io/api/animes/52991", item.ExternalLinks.Single().ApiUrl);
    }

    // The sources know their own API better than the rule does — AniLiberty
    // serves it from a different host entirely — so a supplied value stands.
    [Fact]
    public void SuppliedApiUrlIsKeptAsIs()
    {
        var item = MediaItem.FromSnapshot(Snapshot());

        item.ReplaceExternalLinks([new ExternalLink(
            "aniliberty",
            "https://anilibria.top/anime/releases/release/frieren/episodes",
            "https://api.anilibria.app/api/v1/anime/releases/9000")]);

        Assert.Equal("https://api.anilibria.app/api/v1/anime/releases/9000", item.ExternalLinks.Single().ApiUrl);
    }

    [Fact]
    public void LinksAreReplacedWholesaleAndTrimmed()
    {
        var item = MediaItem.FromSnapshot(Snapshot());
        item.ReplaceExternalLinks([
            new ExternalLink("shikimori", "https://shikimori.io/animes/52991"),
            new ExternalLink("myanimelist", "https://myanimelist.net/anime/52991"),
        ]);

        item.ReplaceExternalLinks([new ExternalLink("  aniliberty  ", "  https://anilibria.top/anime/frieren  ")]);

        var single = Assert.Single(item.ExternalLinks);
        Assert.Equal("aniliberty", single.Source);
        Assert.Equal("https://anilibria.top/anime/frieren", single.Url);
    }

    [Fact]
    public void EmptyListLeavesTheWorkWithNoLinks()
    {
        var item = MediaItem.FromSnapshot(Snapshot(new ExternalLink("shikimori", "https://shikimori.io/animes/52991")));

        item.ReplaceExternalLinks([]);

        Assert.Empty(item.ExternalLinks);
    }

    [Theory]
    [InlineData("", "https://shikimori.io/animes/52991", null)]
    [InlineData("   ", "https://shikimori.io/animes/52991", null)]
    [InlineData("shikimori", "не адрес", null)]
    [InlineData("shikimori", "javascript:alert(1)", null)]
    [InlineData("shikimori", "/animes/52991", null)]
    [InlineData("shikimori", "https://shikimori.io/animes/52991", "не адрес")]
    public void BadInputIsRejected(string source, string url, string? apiUrl)
    {
        var item = MediaItem.FromSnapshot(Snapshot());

        Assert.Throws<DomainException>(() =>
            item.ReplaceExternalLinks([new ExternalLink(source, url, apiUrl)]));
    }

    [Fact]
    public void RejectedInputLeavesTheStoredLinksAlone()
    {
        var item = MediaItem.FromSnapshot(Snapshot());
        item.ReplaceExternalLinks([new ExternalLink("shikimori", "https://shikimori.io/animes/52991")]);

        Assert.Throws<DomainException>(() => item.ReplaceExternalLinks([
            new ExternalLink("aniliberty", "https://anilibria.top/anime/frieren"),
            new ExternalLink("myanimelist", "не адрес"),
        ]));

        Assert.Equal("shikimori", item.ExternalLinks.Single().Source);
    }

    // The whole point of the flag: refreshing the catalogue snapshot must not
    // undo a correction the user made by hand.
    [Fact]
    public void SourceRefreshDoesNotClobberHandEditedLinks()
    {
        var item = MediaItem.FromSnapshot(Snapshot(new ExternalLink("myanimelist", "https://myanimelist.net/anime/52991")));
        item.ReplaceExternalLinks([new ExternalLink("shikimori", "https://shikimori.io/animes/52991")]);

        item.RefreshSnapshot(Snapshot(new ExternalLink("myanimelist", "https://myanimelist.net/anime/52991")));

        Assert.Equal("shikimori", item.ExternalLinks.Single().Source);
    }

    [Fact]
    public void SourceRefreshStillUpdatesUntouchedLinks()
    {
        var item = MediaItem.FromSnapshot(Snapshot(new ExternalLink("myanimelist", "https://myanimelist.net/anime/1")));

        item.RefreshSnapshot(Snapshot(new ExternalLink("myanimelist", "https://myanimelist.net/anime/52991")));

        Assert.Equal("https://myanimelist.net/anime/52991", item.ExternalLinks.Single().Url);
    }
}
