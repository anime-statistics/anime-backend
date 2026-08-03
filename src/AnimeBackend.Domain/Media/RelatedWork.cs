namespace AnimeBackend.Domain.Media;

public sealed record RelatedWork(string Id, string Title, string Relation);

// Every source contributes two addresses: the page a person reads and the API
// twin the app queries. `ApiUrl` is null when it is not known — the frontend
// derives one of its own in that case (`core/utils/externalLinks.ts`).
public sealed record ExternalLink(string Source, string Url, string? ApiUrl = null)
{
    // User input on its way into the collection. Source clients skip this: a
    // malformed third-party address must not abort a whole import.
    public ExternalLink Normalized()
    {
        var source = Source?.Trim() ?? "";
        if (source.Length == 0)
            throw new DomainException("У ссылки не указан источник");

        var url = Absolute(Url) ?? throw new DomainException($"Некорректный адрес страницы: {Url}");
        var apiUrl = string.IsNullOrWhiteSpace(ApiUrl)
            ? DeriveApiUrl(url)
            : Absolute(ApiUrl) ?? throw new DomainException($"Некорректный адрес API: {ApiUrl}");

        return new ExternalLink(source, url, apiUrl);
    }

    // Both sources expose their data under /api on the same host, so the API
    // twin of a page URL is that URL with /api pushed in front of the path —
    // unless the address already points at the API. Mirrors `ensureApiUrl` in
    // the frontend, tests included. Null for anything that is not an absolute
    // http(s) address.
    public static string? DeriveApiUrl(string? url)
    {
        if (Absolute(url) is not { } normalized) return null;

        var parsed = new Uri(normalized);
        var path = parsed.AbsolutePath;
        if (path == "/api" || path.StartsWith("/api/", StringComparison.Ordinal))
            return normalized;

        var apiPath = path == "/" ? "/api" : $"/api{path}";
        return $"{parsed.Scheme}://{parsed.Authority}{apiPath}{parsed.Query}{parsed.Fragment}";
    }

    private static string? Absolute(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri
            : null;
}
