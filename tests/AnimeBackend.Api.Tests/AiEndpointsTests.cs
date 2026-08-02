using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace AnimeBackend.Api.Tests;

public class AiEndpointsTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;

    public AiEndpointsTests(TestAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Json(string raw) => new(raw, Encoding.UTF8, "application/json");

    private static async Task<JsonNode> ReadAsync(HttpResponseMessage response)
        => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    [Fact]
    public async Task ModelsListTheCatalogInSnakeCase()
    {
        var response = await _client.GetAsync("/api/v1/ai/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"supports_streaming\"", raw);
        Assert.Contains("\"input_price\"", raw);

        var body = JsonNode.Parse(raw)!;
        var ids = body["items"]!.AsArray().Select(m => (string)m!["id"]!).ToList();
        Assert.Contains("claude-opus-5", ids);
    }

    [Fact]
    public async Task ChatRepliesWithUsage()
    {
        _factory.Ai.Responder = null;
        _factory.Ai.NextText = "Привет! Чем помочь?";

        var response = await _client.PostAsync(
            "/api/v1/ai/chat",
            Json("""{"messages":[{"role":"user","content":"привет"}],"model":"claude-opus-5","temperature":0.7,"deep_think":false}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Привет! Чем помочь?", (string)body["reply"]!);
        Assert.Equal(10, (int)body["usage"]!["input_tokens"]!);
        Assert.Equal(20, (int)body["usage"]!["output_tokens"]!);
    }

    [Fact]
    public async Task ChatStreamSendsPlainTextChunks()
    {
        _factory.Ai.Responder = null;
        _factory.Ai.NextText = "Стриминговый ответ ассистента";

        var response = await _client.PostAsync(
            "/api/v1/ai/chat/stream",
            Json("""{"messages":[{"role":"user","content":"привет"}],"model":"claude-opus-5"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Стриминговый ответ ассистента", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ParaphrasePassesStyleAndReturnsResult()
    {
        _factory.Ai.Responder = request =>
            request.System!.Contains("официально-деловом") ? "Официальный текст." : "unexpected";

        var response = await _client.PostAsync(
            "/api/v1/ai/paraphrase",
            Json("""{"text":"ну короче классное аниме","style":"formal","model":"claude-opus-5"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Официальный текст.", (string)body["result"]!);
        _factory.Ai.Responder = null;
    }

    [Fact]
    public async Task ProcessVoiceReturnsCleanedTextAndSuggestions()
    {
        _factory.Ai.Responder = _ =>
            """{"processed_text":"Найди аниме про магию","suggestions":["Frieren","Mushoku Tensei"]}""";

        var response = await _client.PostAsync(
            "/api/v1/ai/process-voice",
            Json("""{"text":"эээ ну найди аниме про магию"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Найди аниме про магию", (string)body["processed_text"]!);
        Assert.Equal(2, body["suggestions"]!.AsArray().Count);
        _factory.Ai.Responder = null;
    }

    [Fact]
    public async Task RecommendationsResolveRealMediaIds()
    {
        _factory.Shikimori.AddAnime(801, "Vinland Saga", episodes: 24);
        _factory.Ai.Responder = request => request.JsonSchema is not null
            ? """{"items":[{"title":"Vinland Saga","reason":"Историческая драма","score":0.9},{"title":"Nonexistent Show XYZ","reason":"нет такого","score":0.5}]}"""
            : "unexpected";

        var response = await _client.PostAsync(
            "/api/v1/ai/recommendations",
            Json("""{"prompt":"что-то историческое","mood":"tense","model":"claude-opus-5"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        var items = body["items"]!.AsArray();

        // The resolvable title gets a real catalogue id; the invented one is dropped.
        Assert.Single(items);
        Assert.Equal("shikimori_801-vinland-saga", (string)items[0]!["media_id"]!);
        Assert.Equal("Историческая драма", (string)items[0]!["reason"]!);
        _factory.Ai.Responder = null;
    }
}

public class UnconfiguredAiTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public UnconfiguredAiTests(TestAppFactory factory)
    {
        factory.Ai.IsConfigured = false;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ModelsAreEmptyAndActionsExplainHowToConnect()
    {
        var models = await _client.GetAsync("/api/v1/ai/models");
        Assert.Equal(HttpStatusCode.OK, models.StatusCode);
        var body = JsonNode.Parse(await models.Content.ReadAsStringAsync())!;
        Assert.Empty(body["items"]!.AsArray());

        var chat = await _client.PostAsync(
            "/api/v1/ai/chat",
            new StringContent(
                """{"messages":[{"role":"user","content":"привет"}]}""",
                Encoding.UTF8,
                "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, chat.StatusCode);
        var error = JsonNode.Parse(await chat.Content.ReadAsStringAsync())!;
        Assert.Contains("ANTHROPIC_API_KEY", (string)error["message"]!);
    }
}
