using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace AnimeBackend.Api.Tests;

public class ApiTests : IClassFixture<TestAppFactory>
{
    private const string WatchingTag = "0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f701";
    private const string CompletedTag = "0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f703";

    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;

    public ApiTests(TestAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Json(string raw) => new(raw, Encoding.UTF8, "application/json");

    private async Task<JsonNode> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(text)!;
    }

    [Fact]
    public async Task TagsSeedComesBackInSnakeCase()
    {
        var response = await _client.GetAsync("/api/v1/tags");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"sort_order\"", raw);
        Assert.Contains("\"is_hidden\"", raw);

        var body = JsonNode.Parse(raw)!;
        Assert.Equal(6, (int)body["total"]!);
        Assert.Equal("Смотрю", (string)body["items"]![0]!["name"]!);
        Assert.Equal(WatchingTag, (string)body["items"]![0]!["id"]!);
    }

    [Fact]
    public async Task SearchMergesSourcesAndOverlaysTagsFromEitherConstituent()
    {
        _factory.Shikimori.AddAnime(101, "Vinland Saga", episodes: 24);
        _factory.Aniliberty.AddAnime(201, "Vinland Saga", episodes: 24, titleRussian: "Сага о Винланде");

        // Tag the AniLiberty record — the merged card is fronted by Shikimori,
        // yet the collection tags must still surface.
        var patch = await _client.PatchAsync(
            "/api/v1/anime/aniliberty_201-vinland-saga/tags",
            Json($"{{\"my_tags\":[\"{WatchingTag}\"]}}"));
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var search = await ReadAsync(await _client.GetAsync("/api/v1/search?query=vinland"));
        var items = search["items"]!.AsArray();

        Assert.Equal(1, (int)search["total"]!);
        var card = items[0]!;
        Assert.Equal("shikimori", (string)card["source"]!);
        Assert.Equal("aniliberty", (string)card["secondary_source"]!);
        Assert.Equal("Сага о Винланде", (string)card["title_russian"]!);
        Assert.Equal(WatchingTag, (string)card["my_tags"]![0]!);
    }

    [Fact]
    public async Task TaggingFromSearchMaterialisesAndUntaggingRemovesFromLibrary()
    {
        _factory.Shikimori.AddAnime(301, "Mushishi", episodes: 26);

        var tagged = await _client.PatchAsync(
            "/api/v1/anime/shikimori_301-mushishi/tags",
            Json($"{{\"my_tags\":[\"{WatchingTag}\",\"{CompletedTag}\"]}}"));
        Assert.Equal(HttpStatusCode.OK, tagged.StatusCode);

        var library = await ReadAsync(await _client.GetAsync("/api/v1/anime?query=mushishi"));
        Assert.Equal(1, (int)library["total"]!);
        Assert.Equal(2, library["items"]![0]!["my_tags"]!.AsArray().Count);

        // Dropping the last tag removes the work from the library...
        await _client.PatchAsync(
            "/api/v1/anime/shikimori_301-mushishi/tags", Json("{\"my_tags\":[]}"));
        var emptied = await ReadAsync(await _client.GetAsync("/api/v1/anime?query=mushishi"));
        Assert.Equal(0, (int)emptied["total"]!);

        // ...but the work stays findable in search, now with empty tags.
        var search = await ReadAsync(await _client.GetAsync("/api/v1/search?query=mushishi"));
        Assert.Equal(1, (int)search["total"]!);
        Assert.Empty(search["items"]![0]!["my_tags"]!.AsArray());
    }

    [Fact]
    public async Task BulkTagFiftyWorksInOneRequest()
    {
        var ids = new List<string>();
        for (var i = 1; i <= 50; i++)
        {
            _factory.Shikimori.AddAnime(1000 + i, $"Bulk Title {i}");
            ids.Add($"\"shikimori_{1000 + i}-bulk-title-{i}\"");
        }
        var idsJson = string.Join(',', ids);

        var added = await ReadAsync(await _client.PostAsync(
            "/api/v1/anime/tags/bulk",
            Json($"{{\"ids\":[{idsJson}],\"add\":[\"{CompletedTag}\"]}}")));
        Assert.Equal(50, (int)added["updated"]!);

        // Re-adding the same tag changes nothing.
        var repeat = await ReadAsync(await _client.PostAsync(
            "/api/v1/anime/tags/bulk",
            Json($"{{\"ids\":[{idsJson}],\"add\":[\"{CompletedTag}\"]}}")));
        Assert.Equal(0, (int)repeat["updated"]!);

        var removed = await ReadAsync(await _client.PostAsync(
            "/api/v1/anime/tags/bulk",
            Json($"{{\"ids\":[{idsJson}],\"remove\":[\"{CompletedTag}\"]}}")));
        Assert.Equal(50, (int)removed["updated"]!);
    }

    [Fact]
    public async Task NotesCrudWorksAndMaterialisesTheWork()
    {
        _factory.Shikimori.AddAnime(401, "Barakamon");
        const string mediaId = "shikimori_401-barakamon";

        var created = await _client.PostAsync(
            "/api/v1/notes",
            Json($"{{\"media_id\":\"{mediaId}\",\"content\":\"# Заметка\"}}"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var note = await ReadAsync(created);
        var noteId = (string)note["id"]!;
        Assert.Equal(mediaId, (string)note["media_id"]!);
        Assert.NotNull(note["created_at"]);

        var list = await ReadAsync(await _client.GetAsync($"/api/v1/notes?media_id={mediaId}"));
        Assert.Equal(1, (int)list["total"]!);

        var updated = await ReadAsync(await _client.PatchAsync(
            $"/api/v1/notes/{noteId}", Json("{\"content\":\"обновлено\"}")));
        Assert.Equal("обновлено", (string)updated["content"]!);

        var deleted = await _client.DeleteAsync($"/api/v1/notes/{noteId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var empty = await ReadAsync(await _client.GetAsync($"/api/v1/notes?media_id={mediaId}"));
        Assert.Equal(0, (int)empty["total"]!);
    }

    [Fact]
    public async Task ErrorsComeBackAsMessageJson()
    {
        // Malformed media id → 400.
        var badId = await _client.PatchAsync("/api/v1/anime/definitely-not-an-id/tags", Json("{\"my_tags\":[]}"));
        Assert.Equal(HttpStatusCode.BadRequest, badId.StatusCode);
        Assert.NotNull((await ReadAsync(badId))["message"]);

        // Unknown tag → 400 with a readable message.
        _factory.Shikimori.AddAnime(501, "Error Case");
        var missingTag = await _client.PatchAsync(
            "/api/v1/anime/shikimori_501-error-case/tags",
            Json($"{{\"my_tags\":[\"{Guid.NewGuid()}\"]}}"));
        Assert.Equal(HttpStatusCode.BadRequest, missingTag.StatusCode);

        // Work unknown to every source → 404.
        var unknown = await _client.GetAsync("/api/v1/anime/shikimori_999999-nope");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.NotNull((await ReadAsync(unknown))["message"]);
    }

    [Fact]
    public async Task ProgressValidationRejectsWrongShapes()
    {
        _factory.Shikimori.AddAnime(601, "Progress Case", episodes: 12);
        const string url = "/api/v1/anime/shikimori_601-progress-case/progress";

        var tooHigh = await _client.PatchAsync(url, Json("{\"score\":11}"));
        Assert.Equal(HttpStatusCode.BadRequest, tooHigh.StatusCode);

        var mangaFields = await _client.PatchAsync(url, Json("{\"volumes_read\":3}"));
        Assert.Equal(HttpStatusCode.BadRequest, mangaFields.StatusCode);

        var ok = await ReadAsync(await _client.PatchAsync(url, Json("{\"score\":8,\"watched_episodes\":5}")));
        Assert.Equal(8, (double)ok["score"]!);
        Assert.Equal(5, (int)ok["watched_episodes"]!);
    }

    [Fact]
    public async Task LibraryPaginationReportsTotalAcrossPages()
    {
        for (var i = 1; i <= 5; i++)
            _factory.Aniliberty.AddAnime(3000 + i, $"Paging Case {i}");

        var ids = string.Join(',', Enumerable.Range(1, 5)
            .Select(i => $"\"aniliberty_{3000 + i}-paging-case-{i}\""));
        await _client.PostAsync(
            "/api/v1/anime/tags/bulk",
            Json($"{{\"ids\":[{ids}],\"add\":[\"{WatchingTag}\"]}}"));

        var page2 = await ReadAsync(await _client.GetAsync("/api/v1/anime?query=paging&page=2&size=2"));
        Assert.Equal(5, (int)page2["total"]!);
        Assert.Equal(2, (int)page2["page"]!);
        Assert.Equal(2, (int)page2["size"]!);
        Assert.Equal(2, page2["items"]!.AsArray().Count);
    }
}

public class SearchDegradationTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;

    public SearchDegradationTests(TestAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OneDeadSourceDegradesGracefullyBothDeadIs424()
    {
        _factory.Aniliberty.AddAnime(701, "Survivor");
        _factory.Shikimori.IsDown = true;

        var degraded = await _client.GetAsync("/api/v1/search?query=survivor");
        Assert.Equal(HttpStatusCode.OK, degraded.StatusCode);
        var body = JsonNode.Parse(await degraded.Content.ReadAsStringAsync())!;
        Assert.Equal(1, (int)body["total"]!);

        _factory.Aniliberty.IsDown = true;
        var dead = await _client.GetAsync("/api/v1/search?query=survivor");
        Assert.Equal(HttpStatusCode.FailedDependency, dead.StatusCode);
        Assert.NotNull(JsonNode.Parse(await dead.Content.ReadAsStringAsync())!["message"]);
    }
}
