using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AnimeBackend.Application.Ai;

namespace AnimeBackend.Api.Tests;

public class AiEndpointsTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;

    public AiEndpointsTests(TestAppFactory factory)
    {
        _factory = factory;
        _factory.Ai.Reset();
        _factory.Ai.IsConfigured = true;
        _client = factory.CreateClient();
    }

    private static StringContent Json(string raw) => new(raw, Encoding.UTF8, "application/json");

    private static async Task<JsonNode> ReadAsync(HttpResponseMessage response)
        => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    [Fact]
    public async Task ModelsExposeOnlyToolCapableOnesInSnakeCase()
    {
        var response = await _client.GetAsync("/api/v1/ai/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"context_window\"", raw);
        Assert.Contains("\"usage_count\"", raw);

        var ids = JsonNode.Parse(raw)!["items"]!.AsArray()
            .Select(m => (string)m!["id"]!)
            .ToList();
        Assert.Contains("deepseek/deepseek-r1", ids);
        // A model without tool support can't drive the assistant's design.
        Assert.DoesNotContain("legacy/no-tools", ids);
    }

    [Fact]
    public async Task ChatRunsTheToolLoopAndReturnsCombinedUsage()
    {
        _factory.Shikimori.AddAnime(901, "Monster", episodes: 74);
        _factory.Ai.Script(
            new AiCompletion
            {
                ToolCalls = [new AiToolCall("call_1", "library_stats", "{}")],
                InputTokens = 100,
                OutputTokens = 15,
            },
            new AiCompletion
            {
                Text = "В коллекции пока пусто.",
                InputTokens = 140,
                OutputTokens = 25,
            });

        var response = await _client.PostAsync(
            "/api/v1/ai/chat",
            Json("""{"messages":[{"role":"user","content":"что у меня в коллекции?"}],"model":"deepseek/deepseek-r1"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("В коллекции пока пусто.", (string)body["reply"]!);
        // Usage sums every round trip the loop made, not just the last one.
        Assert.Equal(240, (int)body["usage"]!["input_tokens"]!);
        Assert.Equal(40, (int)body["usage"]!["output_tokens"]!);

        // The tool result was fed back as a `tool` message.
        var secondTurn = _factory.Ai.Requests[1];
        Assert.Contains(secondTurn.Messages, m => m.Role == "tool" && m.ToolCallId == "call_1");
    }

    [Fact]
    public async Task ChatOffersTheLibraryToolsToTheModel()
    {
        await _client.PostAsync(
            "/api/v1/ai/chat",
            Json("""{"messages":[{"role":"user","content":"привет"}]}"""));

        var tools = _factory.Ai.LastRequest!.Tools!.Select(t => t.Name).ToList();
        Assert.Contains("search_library", tools);
        Assert.Contains("get_media_details", tools);
        Assert.Contains("search_catalog", tools);
    }

    [Fact]
    public async Task TemperatureIsDroppedForModelsThatRejectIt()
    {
        await _client.PostAsync(
            "/api/v1/ai/chat",
            Json("""{"messages":[{"role":"user","content":"привет"}],"model":"deepseek/deepseek-r1","temperature":0.4}"""));
        Assert.Equal(0.4, _factory.Ai.LastRequest!.Temperature);

        _factory.Ai.Reset();
        await _client.PostAsync(
            "/api/v1/ai/chat",
            Json("""{"messages":[{"role":"user","content":"привет"}],"model":"anthropic/claude-fable-5","temperature":0.4}"""));
        Assert.Null(_factory.Ai.LastRequest!.Temperature);
    }

    [Fact]
    public async Task UnknownModelFallsBackInsteadOfFailing()
    {
        await _client.PostAsync(
            "/api/v1/ai/chat",
            Json("""{"messages":[{"role":"user","content":"привет"}],"model":"claude-sonnet-4-5"}"""));

        Assert.Equal("deepseek/deepseek-r1", _factory.Ai.LastRequest!.Model);
    }

    [Fact]
    public async Task ChatStreamSendsPlainTextChunks()
    {
        _factory.Ai.NextText = "Стриминговый ответ ассистента";

        var response = await _client.PostAsync(
            "/api/v1/ai/chat/stream",
            Json("""{"messages":[{"role":"user","content":"привет"}]}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Стриминговый ответ ассистента", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UsedModelsLeadThePicker()
    {
        _factory.Ai.NextText = "ok";
        await _client.PostAsync(
            "/api/v1/ai/chat",
            Json("""{"messages":[{"role":"user","content":"привет"}],"model":"anthropic/claude-fable-5"}"""));

        var items = (await ReadAsync(await _client.GetAsync("/api/v1/ai/models")))["items"]!.AsArray();
        var counts = items.Select(m => (int)m!["usage_count"]!).ToList();
        var fable = items.Single(m => (string)m!["id"]! == "anthropic/claude-fable-5")!;

        Assert.True((int)fable["usage_count"]! >= 1);
        // Every used model sorts ahead of every unused one. Asserting the exact
        // first place would depend on what other tests in this class ran first.
        var lastUsed = counts.FindLastIndex(count => count > 0);
        var firstUnused = counts.FindIndex(count => count == 0);
        if (lastUsed >= 0 && firstUnused >= 0) Assert.True(lastUsed < firstUnused);
    }

    [Fact]
    public async Task ParaphrasePassesStyleAndReturnsResult()
    {
        _factory.Ai.NextText = "Официальный текст.";

        var response = await _client.PostAsync(
            "/api/v1/ai/paraphrase",
            Json("""{"text":"ну короче классное аниме","style":"formal"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Официальный текст.", (string)(await ReadAsync(response))["result"]!);
        Assert.Contains("официально-деловом", _factory.Ai.LastRequest!.System!);
    }

    [Fact]
    public async Task ProcessVoiceReturnsCleanedTextAndSuggestions()
    {
        _factory.Ai.NextText =
            """{"processed_text":"Найди аниме про магию","suggestions":["Frieren","Mushoku Tensei"]}""";

        var response = await _client.PostAsync(
            "/api/v1/ai/process-voice",
            Json("""{"text":"эээ ну найди аниме про магию"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Найди аниме про магию", (string)body["processed_text"]!);
        Assert.Equal(2, body["suggestions"]!.AsArray().Count);
    }

    [Fact]
    public async Task ProcessVoiceSurvivesAModelThatIgnoresTheSchema()
    {
        _factory.Ai.NextText = "я не умею в json";

        var body = await ReadAsync(await _client.PostAsync(
            "/api/v1/ai/process-voice",
            Json("""{"text":"найди аниме про магию"}""")));

        // Falls back to the raw dictation rather than failing the request.
        Assert.Equal("найди аниме про магию", (string)body["processed_text"]!);
        Assert.Empty(body["suggestions"]!.AsArray());
    }

    [Fact]
    public async Task RecommendationsKeepOnlyItemsWithRealMediaIds()
    {
        _factory.Ai.Script(
            // The model looks the candidate up...
            new AiCompletion
            {
                ToolCalls = [new AiToolCall("call_1", "search_catalog", """{"query":"Monster"}""")],
                InputTokens = 200,
                OutputTokens = 40,
            },
            // ...then answers with the JSON in the same conversation, wrapped
            // in a fenced block the way models habitually do.
            new AiCompletion
            {
                Text = """
                    Вот подборка:
                    ```json
                    {"items":[
                      {"media_id":"shikimori_901-monster","title":"Monster","reason":"Психологический триллер","score":0.92},
                      {"media_id":"","title":"Выдуманное аниме","reason":"нет id","score":0.4}
                    ]}
                    ```
                    """,
                InputTokens = 60,
                OutputTokens = 30,
            });

        var body = await ReadAsync(await _client.PostAsync(
            "/api/v1/ai/recommendations",
            Json("""{"prompt":"что-то напряжённое","mood":"tense"}""")));

        var items = body["items"]!.AsArray();
        // The invented entry has no catalogue id, so it never reaches the UI.
        Assert.Single(items);
        Assert.Equal("shikimori_901-monster", (string)items[0]!["media_id"]!);
        Assert.Equal(260, (int)body["usage"]!["input_tokens"]!);
        Assert.NotNull(_factory.Ai.Requests[0].Tools);
    }
}

public class UnconfiguredAiTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public UnconfiguredAiTests(TestAppFactory factory)
    {
        factory.Ai.Reset();
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
        Assert.Contains("ROUTERAI_API_KEY", (string)error["message"]!);
    }
}
