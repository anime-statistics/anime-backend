namespace AnimeBackend.Infrastructure;

public sealed class ShikimoriOptions
{
    public const string Section = "Sources:Shikimori";

    public string BaseUrl { get; set; } = "https://shikimori.io";

    // Shikimori rejects anonymous clients; the value identifies this app.
    public string UserAgent { get; set; } = "anime-statistics-backend";

    public int SearchLimit { get; set; } = 50;
}

// The project keeps moving domains: anilibria.tv is dead, and api.anilibria.app
// answers with headers and then stalls the body forever. aniliberty.top serves
// the same v1 API, the posters and the pages a person opens — and is the host
// the frontend already links to.
public sealed class AniLibertyOptions
{
    public const string Section = "Sources:AniLiberty";

    public string BaseUrl { get; set; } = "https://aniliberty.top";

    // Poster paths in API responses are relative to the site host. It happens to
    // be the API host too right now; the split stays because it has not always
    // been.
    public string AssetsBaseUrl { get; set; } = "https://aniliberty.top";

    public int CatalogPageSize { get; set; } = 50;
}

public sealed class StorageOptions
{
    public const string Section = "Storage";

    public string AttachmentsPath { get; set; } = "data/attachments";

    // URL prefix the attachments are served from by the API host.
    public string PublicPath { get; set; } = "/attachments";
}
