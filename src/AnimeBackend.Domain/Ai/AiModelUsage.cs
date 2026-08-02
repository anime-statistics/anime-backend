namespace AnimeBackend.Domain.Ai;

// How often each model has actually been used, so the picker can lead with the
// ones this user reaches for instead of the provider's full catalogue.
public sealed class AiModelUsage
{
    public string ModelId { get; private set; } = null!;
    public int CallCount { get; private set; }
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public DateTimeOffset LastUsedAt { get; private set; }

    private AiModelUsage() { }

    public AiModelUsage(string modelId)
    {
        ModelId = modelId;
        LastUsedAt = DateTimeOffset.UtcNow;
    }

    public void Record(long inputTokens, long outputTokens)
    {
        CallCount++;
        InputTokens += Math.Max(inputTokens, 0);
        OutputTokens += Math.Max(outputTokens, 0);
        LastUsedAt = DateTimeOffset.UtcNow;
    }
}
