namespace AnimeBackend.Domain.Media;

public enum MediaSource
{
    Shikimori,
    Aniliberty,
}

public static class MediaSourceExtensions
{
    public static string ToWire(this MediaSource source) => source switch
    {
        MediaSource.Shikimori => "shikimori",
        MediaSource.Aniliberty => "aniliberty",
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };

    public static MediaSource? FromWire(string? value) => value switch
    {
        "shikimori" => MediaSource.Shikimori,
        "aniliberty" => MediaSource.Aniliberty,
        _ => null,
    };
}
