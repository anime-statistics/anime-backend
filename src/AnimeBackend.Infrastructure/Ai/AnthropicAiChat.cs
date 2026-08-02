using System.Runtime.CompilerServices;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using AnimeBackend.Application.Ai;
using Microsoft.Extensions.Options;

namespace AnimeBackend.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string Section = "Ai";

    // Falls back to the ANTHROPIC_API_KEY environment variable when unset.
    public string? ApiKey { get; set; }
}

// IAiChat over the official Anthropic SDK. Model specifics live here: current
// Claude models reject sampling parameters (the frontend's temperature is
// dropped upstream), thinking is adaptive by default, and `deep_think` maps to
// the effort level.
public sealed class AnthropicAiChat : IAiChat
{
    private readonly Lazy<AnthropicClient> _client;
    private readonly string? _apiKey;

    public AnthropicAiChat(IOptions<AiOptions> options)
    {
        _apiKey = string.IsNullOrWhiteSpace(options.Value.ApiKey)
            ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            : options.Value.ApiKey;
        _client = new Lazy<AnthropicClient>(() => new AnthropicClient { ApiKey = _apiKey });
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<AiCompletion> CompleteAsync(AiChatRequest request, CancellationToken ct)
    {
        var response = await _client.Value.Messages.Create(BuildParams(request));

        if (response.StopReason == "refusal")
            return new AiCompletion("", Tokens(response.Usage.InputTokens), Tokens(response.Usage.OutputTokens), true);

        var text = string.Concat(response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(block => block.Text));

        return new AiCompletion(
            text,
            Tokens(response.Usage.InputTokens),
            Tokens(response.Usage.OutputTokens),
            Refused: false);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AiChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var streamEvent in _client.Value.Messages.CreateStreaming(BuildParams(request)))
        {
            ct.ThrowIfCancellationRequested();
            if (streamEvent.TryPickContentBlockDelta(out var delta)
                && delta.Delta.TryPickText(out var text)
                && text.Text.Length > 0)
            {
                yield return text.Text;
            }
        }
    }

    private static MessageCreateParams BuildParams(AiChatRequest request) => new()
    {
        Model = request.Model,
        MaxTokens = request.MaxTokens,
        System = request.System is { Length: > 0 } system ? system : (MessageCreateParamsSystem?)null,
        Messages = [.. request.Messages.Select(message => new MessageParam
        {
            Role = message.Role == "assistant" ? Role.Assistant : Role.User,
            Content = message.Content,
        })],
        OutputConfig = new OutputConfig
        {
            // Adaptive thinking stays on (the model default); effort is the
            // latency/quality lever the deep-think toggle actually controls.
            Effort = request.DeepThink ? Effort.High : Effort.Low,
            Format = request.JsonSchema is null
                ? null
                : new JsonOutputFormat
                {
                    Schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.JsonSchema)!,
                },
        },
    };

    private static long Tokens(long? value) => value ?? 0;
}
