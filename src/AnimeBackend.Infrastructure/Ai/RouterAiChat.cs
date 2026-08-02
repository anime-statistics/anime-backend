using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AnimeBackend.Application.Ai;
using AnimeBackend.Domain;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnimeBackend.Infrastructure.Ai;

public sealed class RouterAiOptions
{
    public const string Section = "Ai";

    public string BaseUrl { get; set; } = "https://routerai.ru/api/v1";

    // Falls back to the ROUTERAI_API_KEY environment variable when unset.
    public string? ApiKey { get; set; }
}

// IAiChat over RouterAI — an OpenAI-compatible router that bills in roubles
// and fronts several hundred models. Only the wire format lives here; the
// agentic loop is in the Application layer.
public sealed class RouterAiChat : IAiChat
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly HybridCacheEntryOptions ModelsTtl = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30),
    };

    private readonly HttpClient _http;
    private readonly HybridCache _cache;
    private readonly ILogger<RouterAiChat> _logger;
    private readonly string? _apiKey;

    public RouterAiChat(
        HttpClient http,
        HybridCache cache,
        IOptions<RouterAiOptions> options,
        ILogger<RouterAiChat> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _apiKey = string.IsNullOrWhiteSpace(options.Value.ApiKey)
            ? Environment.GetEnvironmentVariable("ROUTERAI_API_KEY")
            : options.Value.ApiKey;

        if (_apiKey is { Length: > 0 })
            _http.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<AiCompletion> CompleteAsync(AiChatRequest request, CancellationToken ct)
    {
        var payload = BuildPayload(request, stream: false);
        var started = System.Diagnostics.Stopwatch.StartNew();
        using var response = await SendAsync(payload, ct);
        _logger.LogInformation(
            "RouterAI {Model} ответил за {Elapsed} мс (tool_choice={ToolChoice})",
            request.Model, started.ElapsedMilliseconds, request.ToolChoice ?? "auto");
        var body = await response.Content.ReadFromJsonAsync<JsonNode>(ct)
                   ?? throw new SourceUnavailableException("Пустой ответ от RouterAI");

        var choice = body["choices"]?[0];
        var message = choice?["message"];
        var usage = body["usage"];

        return new AiCompletion
        {
            Text = message?["content"]?.GetValue<string>() ?? "",
            ToolCalls = ReadToolCalls(message?["tool_calls"]),
            InputTokens = usage?["prompt_tokens"]?.GetValue<long>() ?? 0,
            OutputTokens = usage?["completion_tokens"]?.GetValue<long>() ?? 0,
            FinishReason = choice?["finish_reason"]?.GetValue<string>(),
        };
    }

    public async IAsyncEnumerable<AiStreamEvent> StreamAsync(
        AiChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var payload = BuildPayload(request, stream: true);
        payload["stream_options"] = new JsonObject { ["include_usage"] = true };

        using var response = await SendAsync(payload, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var text = new StringBuilder();
        var toolCalls = new SortedDictionary<int, ToolCallBuilder>();
        long inputTokens = 0, outputTokens = 0;
        string? finishReason = null;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].Trim();
            if (data.Length == 0 || data == "[DONE]") continue;

            JsonNode? chunk;
            try
            {
                chunk = JsonNode.Parse(data);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Не разобрал чанк стрима RouterAI");
                continue;
            }

            if (chunk?["usage"] is { } usage)
            {
                inputTokens = usage["prompt_tokens"]?.GetValue<long>() ?? inputTokens;
                outputTokens = usage["completion_tokens"]?.GetValue<long>() ?? outputTokens;
            }

            var choice = chunk?["choices"]?[0];
            if (choice is null) continue;
            finishReason = choice["finish_reason"]?.GetValue<string>() ?? finishReason;

            var delta = choice["delta"];
            if (delta?["content"]?.GetValue<string>() is { Length: > 0 } content)
            {
                text.Append(content);
                yield return new AiTextDelta(content);
            }

            // Tool calls arrive fragmented: an index keys the call, the name
            // lands once and the arguments stream in pieces.
            if (delta?["tool_calls"] is JsonArray deltaCalls)
            {
                foreach (var call in deltaCalls.OfType<JsonNode>())
                {
                    var index = call["index"]?.GetValue<int>() ?? 0;
                    if (!toolCalls.TryGetValue(index, out var builder))
                        toolCalls[index] = builder = new ToolCallBuilder();

                    if (call["id"]?.GetValue<string>() is { Length: > 0 } id) builder.Id = id;
                    if (call["function"]?["name"]?.GetValue<string>() is { Length: > 0 } name)
                        builder.Name = name;
                    if (call["function"]?["arguments"]?.GetValue<string>() is { Length: > 0 } arguments)
                        builder.Arguments.Append(arguments);
                }
            }
        }

        yield return new AiTurnFinished(new AiCompletion
        {
            Text = text.ToString(),
            ToolCalls = [.. toolCalls.Values
                .Where(b => b.Name is { Length: > 0 })
                .Select(b => new AiToolCall(b.Id, b.Name!, b.Arguments.ToString()))],
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            FinishReason = finishReason,
        });
    }

    public async Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken ct)
    {
        var models = await _cache.GetOrCreateAsync<List<AiModelInfo>>(
            "routerai:models",
            async token =>
            {
                using var response = await _http.GetAsync("models", token);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<JsonNode>(token);
                var data = body?["data"] as JsonArray ?? [];
                return [.. data.OfType<JsonNode>().Select(MapModel)];
            },
            ModelsTtl,
            cancellationToken: ct);

        return models;
    }

    private static IReadOnlyList<AiToolCall> ReadToolCalls(JsonNode? node)
    {
        if (node is not JsonArray calls) return [];
        return
        [
            .. calls.OfType<JsonNode>()
                .Select(call => new AiToolCall(
                    call["id"]?.GetValue<string>() ?? "",
                    call["function"]?["name"]?.GetValue<string>() ?? "",
                    call["function"]?["arguments"]?.GetValue<string>() ?? "{}"))
                .Where(call => call.Name.Length > 0),
        ];
    }

    private static AiModelInfo MapModel(JsonNode node)
    {
        var id = node["id"]?.GetValue<string>() ?? "";
        var parameters = (node["supported_parameters"] as JsonArray ?? [])
            .OfType<JsonNode>()
            .Select(p => p.GetValue<string>())
            .ToHashSet();

        return new AiModelInfo
        {
            Id = id,
            Name = node["name"]?.GetValue<string>() ?? id,
            // Ids are `provider/model`; the prefix is the provider.
            ProviderId = id.Contains('/') ? id[..id.IndexOf('/')] : "routerai",
            ContextLength = node["context_length"]?.GetValue<int>() ?? 0,
            InputPrice = ReadPrice(node["pricing"]?["prompt"]),
            OutputPrice = ReadPrice(node["pricing"]?["completion"]),
            SupportsTools = parameters.Contains("tools"),
            SupportsTemperature = parameters.Contains("temperature"),
        };
    }

    private static double ReadPrice(JsonNode? node)
    {
        if (node is null) return 0;
        try
        {
            return node.GetValue<double>();
        }
        catch (FormatException)
        {
            return double.TryParse(node.ToString(), out var parsed) ? parsed : 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private JsonObject BuildPayload(AiChatRequest request, bool stream)
    {
        // Schema enforcement is not portable behind a router: the upstream
        // provider for a given model may reject `json_schema` outright (Novita
        // does, for DeepSeek R1). So the request asks for `json_object`, which
        // every provider accepts, and spells the schema out in the prompt.
        var system = request.JsonSchema is { Length: > 0 } schema
            ? (request.System is { Length: > 0 } prefix ? prefix + "\n\n" : "")
              + "Ответ — строго один JSON-объект по схеме, без пояснений и Markdown:\n" + schema
            : request.System;

        var messages = new JsonArray();
        if (system is { Length: > 0 })
        {
            messages.Add(new JsonObject
            {
                ["role"] = "system",
                ["content"] = system,
            });
        }

        foreach (var message in request.Messages)
        {
            var node = new JsonObject { ["role"] = message.Role };

            // The API wants content present even on a tool-calling turn.
            node["content"] = message.Content ?? "";

            if (message.ToolCallId is { Length: > 0 })
                node["tool_call_id"] = message.ToolCallId;

            if (message.ToolCalls is { Count: > 0 })
            {
                var calls = new JsonArray();
                foreach (var call in message.ToolCalls)
                {
                    calls.Add(new JsonObject
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["arguments"] = call.ArgumentsJson,
                        },
                    });
                }
                node["tool_calls"] = calls;
            }

            messages.Add(node);
        }

        var payload = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["max_tokens"] = request.MaxTokens,
        };

        if (stream) payload["stream"] = true;
        if (request.Temperature is { } temperature) payload["temperature"] = temperature;

        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = JsonNode.Parse(tool.ParametersJson),
                    },
                });
            }
            payload["tools"] = tools;
            if (request.ToolChoice is { Length: > 0 } choice) payload["tool_choice"] = choice;
        }

        if (request.JsonSchema is { Length: > 0 })
            payload["response_format"] = new JsonObject { ["type"] = "json_object" };

        return payload;
    }

    private async Task<HttpResponseMessage> SendAsync(JsonObject payload, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            using var content = new StringContent(
                payload.ToJsonString(Json), Encoding.UTF8, "application/json");
            var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content,
            };
            response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            throw new SourceUnavailableException("RouterAI недоступен, попробуйте позже");
        }

        if (response.IsSuccessStatusCode) return response;

        // Router errors carry a readable message; surface it instead of a 500
        // so the frontend shows something actionable and doesn't retry.
        var body = await response.Content.ReadAsStringAsync(ct);
        response.Dispose();
        _logger.LogWarning("RouterAI ответил {Status}: {Body}", (int)response.StatusCode, body);
        throw new DomainException($"RouterAI: {ExtractError(body, response.StatusCode)}");
    }

    private static string ExtractError(string body, System.Net.HttpStatusCode status)
    {
        try
        {
            var node = JsonNode.Parse(body);
            if (node?["error"]?["message"]?.GetValue<string>() is { Length: > 0 } message)
                return message;
        }
        catch (JsonException)
        {
            // Fall through to the status code.
        }
        return $"ошибка {(int)status}";
    }

    private sealed class ToolCallBuilder
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public StringBuilder Arguments { get; } = new();
    }
}
