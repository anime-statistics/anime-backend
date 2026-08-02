namespace AnimeBackend.Infrastructure;

public sealed class ShikimoriOptions
{
    public const string Section = "Sources:Shikimori";

    public string BaseUrl { get; set; } = "https://shikimori.io";

    // Shikimori rejects anonymous clients; the value identifies this app.
    public string UserAgent { get; set; } = "anime-statistics-backend";

    public int SearchLimit { get; set; } = 50;
}

public sealed class AniLibertyOptions
{
    public const string Section = "Sources:AniLiberty";

    public string BaseUrl { get; set; } = "https://api.anilibria.app";

    // Poster paths in API responses are relative to the site host, not the API
    // host.
    public string AssetsBaseUrl { get; set; } = "https://anilibria.top";

    public int CatalogPageSize { get; set; } = 50;
}

public sealed class StorageOptions
{
    public const string Section = "Storage";

    public string AttachmentsPath { get; set; } = "data/attachments";

    // URL prefix the attachments are served from by the API host.
    public string PublicPath { get; set; } = "/attachments";
}
