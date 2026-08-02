using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;

namespace AnimeBackend.Application.Tests;

public class MediaIdTests
{
    [Theory]
    [InlineData("Sousou no Frieren", "sousou-no-frieren")]
    [InlineData("Steins;Gate", "steins-gate")]
    [InlineData("Re:Zero kara Hajimeru Isekai Seikatsu", "re-zero-kara-hajimeru-isekai-seikatsu")]
    [InlineData("86", "86")]
    [InlineData("---", "media")]
    [InlineData("Провожающая в последний путь", "media")]
    public void SlugMatchesFrontendGenerator(string title, string expected)
        => Assert.Equal(expected, MediaId.GenerateSlug(title));

    [Fact]
    public void BuildProducesWireCompatibleId()
    {
        var id = MediaId.Build(MediaSource.Shikimori, 52991, "Sousou no Frieren");
        Assert.Equal("shikimori_52991-sousou-no-frieren", id.ToString());
    }

    [Theory]
    [InlineData("shikimori_52991-sousou-no-frieren", "shikimori", 52991)]
    [InlineData("aniliberty_9542-sousou-no-frieren", "aniliberty", 9542)]
    public void ParseRoundTrips(string raw, string source, long numericId)
    {
        var id = MediaId.Parse(raw);
        Assert.Equal(source, id.Source.ToWire());
        Assert.Equal(numericId, id.NumericId);
        Assert.Equal(raw, id.ToString());
    }

    [Theory]
    [InlineData("unknown_1-slug")]
    [InlineData("shikimori-1-slug")]
    [InlineData("shikimori_x-slug")]
    [InlineData("shikimori_1-")]
    [InlineData("")]
    public void InvalidIdsAreRejected(string raw)
    {
        Assert.Null(MediaId.TryParse(raw));
        Assert.Throws<DomainException>(() => MediaId.Parse(raw));
    }
}
