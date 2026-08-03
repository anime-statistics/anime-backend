using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnimeBackend.Application.Abstractions;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AnimeBackend.Infrastructure.Sources.Shikimori;

// REST client for shikimori.io. The site enforces a User-Agent and rate
// limits (5 rps / 90 rpm); responses are cached so repeated searches do not
// burn the budget. Search understands Russian queries natively.
public sealed partial class ShikimoriClient(
    HttpClient http,
    HybridCache cache,
    IOptions<ShikimoriOptions> options) : ISourceClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly HybridCacheEntryOptions SearchTtl = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5),
    };

    private static readonly HybridCacheEntryOptions DetailTtl = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30),
    };

    public MediaSource Source => MediaSource.Shikimori;

    public async Task<IReadOnlyList<MediaSnapshot>> SearchAsync(
        string query, MediaType type, CancellationToken ct)
    {
        var trimmed = query.Trim();
        var key = $"shiki:search:{type}:{trimmed.ToLowerInvariant()}";

        var result = await cache.GetOrCreateAsync<List<MediaSnapshot>>(
            key,
            async token =>
            {
                var resource = type == MediaType.Manga ? "mangas" : "animes";
                var url = $"/api/{resource}?limit={options.Value.SearchLimit}"
                    + (trimmed.Length > 0
                        ? $"&search={Uri.EscapeDataString(trimmed)}"
                        : "&order=popularity");

                var items = await GetAsync<List<ShikiListItem>>(url, token) ?? [];
                return [.. items.Select(item => MapListItem(item, type))];
            },
            SearchTtl,
            cancellationToken: ct);

        return result;
    }

    public async Task<MediaSnapshot?> GetDetailAsync(MediaId id, MediaType type, CancellationToken ct)
    {
        var key = $"shiki:detail:{type}:{id.NumericId}";

        return await cache.GetOrCreateAsync<MediaSnapshot?>(
            key,
            async token =>
            {
                var resource = type == MediaType.Manga ? "mangas" : "animes";
                var detailTask = GetAsync<ShikiDetail>($"/api/{resource}/{id.NumericId}", token);
                var relatedTask = GetAsync<List<ShikiRelated>>($"/api/{resource}/{id.NumericId}/related", token);
                var linksTask = GetAsync<List<ShikiExternalLink>>($"/api/{resource}/{id.NumericId}/external_links", token);
                await Task.WhenAll(detailTask, relatedTask, linksTask);

                var detail = detailTask.Result;
                if (detail is null) return null;
                return MapDetail(detail, type, relatedTask.Result, linksTask.Result);
            },
            DetailTtl,
            cancellationToken: ct);
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct) where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            throw new SourceUnavailableException(MediaSource.Shikimori);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                throw new SourceUnavailableException(MediaSource.Shikimori);
            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
    }

    private MediaSnapshot MapListItem(ShikiListItem item, MediaType type) => new()
    {
        Id = MediaId.Build(MediaSource.Shikimori, item.Id, item.Name),
        Type = type,
        Title = item.Name,
        TitleRussian = Clean(item.Russian),
        EpisodesTotal = PickEpisodes(item.Episodes, item.EpisodesAired),
        VolumesTotal = item.Volumes ?? 0,
        ChaptersTotal = item.Chapters ?? 0,
        SourceScore = ParseScore(item.Score),
        ImageUrl = AbsoluteImage(item.Image),
        AiredFrom = type == MediaType.Anime ? item.AiredOn : null,
        AiredTo = type == MediaType.Anime ? item.ReleasedOn : null,
        PublishedFrom = type == MediaType.Manga ? item.AiredOn : null,
        PublishedTo = type == MediaType.Manga ? item.ReleasedOn : null,
    };

    private MediaSnapshot MapDetail(
        ShikiDetail detail,
        MediaType type,
        List<ShikiRelated>? related,
        List<ShikiExternalLink>? links)
    {
        var relatedWorks = (related ?? [])
            .Select(r =>
            {
                var work = r.Anime ?? r.Manga;
                if (work is null) return null;
                var workType = r.Anime is not null ? MediaType.Anime : MediaType.Manga;
                return new RelatedWork(
                    MediaId.Build(MediaSource.Shikimori, work.Id, work.Name).ToString(),
                    Clean(work.Russian) ?? work.Name,
                    r.RelationRussian ?? r.Relation ?? "Связано");
            })
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

        // `/external_links` lists everyone BUT Shikimori — MAL, AniDB, the
        // official site. Shikimori's own address is the one the user reaches
        // for most, so it leads the list; the rest keep their `kind` as source.
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        var resource = type == MediaType.Manga ? "mangas" : "animes";
        var externalLinks = new List<ExternalLink>
        {
            new("shikimori",
                $"{baseUrl}/{resource}/{detail.Id}",
                $"{baseUrl}/api/{resource}/{detail.Id}"),
        };

        externalLinks.AddRange((links ?? [])
            .Where(l => !string.IsNullOrEmpty(l.Url) && l.Kind != "shikimori")
            // The /api twin rule holds for Shikimori's own host only; inventing
            // one for myanimelist.net would just be a broken address.
            .Select(l => new ExternalLink(
                l.Kind ?? "shikimori",
                l.Url!,
                SameHost(l.Url!, baseUrl) ? ExternalLink.DeriveApiUrl(l.Url) : null)));

        return MapListItem(
            new ShikiListItem(detail.Id, detail.Name, detail.Russian, detail.Image, detail.Score,
                detail.Episodes, detail.EpisodesAired, detail.AiredOn, detail.ReleasedOn,
                detail.Volumes, detail.Chapters, detail.Kind, detail.Status),
            type) with
        {
            TitleEnglish = detail.English?.FirstOrDefault(t => !string.IsNullOrEmpty(t)),
            TitleJapanese = detail.Japanese?.FirstOrDefault(t => !string.IsNullOrEmpty(t)),
            Synopsis = CleanDescription(detail.Description),
            DurationMinutes = detail.Duration is > 0 ? detail.Duration : null,
            Rating = FormatRating(detail.Rating),
            Genres = detail.Genres is { Count: > 0 }
                ? [.. detail.Genres.Select(g => g.Russian ?? g.Name ?? "").Where(g => g.Length > 0)]
                : null,
            Related = relatedWorks.Count > 0 ? relatedWorks : null,
            ExternalLinks = externalLinks,
        };
    }

    private static bool SameHost(string url, string baseUrl)
        => Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin)
            && string.Equals(parsed.Host, origin.Host, StringComparison.OrdinalIgnoreCase);

    // Shikimori sends 0 episodes for ongoing titles in some kinds; falling back
    // to the aired count keeps progress bars meaningful, zero stays "announced".
    private static int PickEpisodes(int? episodes, int? aired)
        => episodes is > 0 ? episodes.Value : aired ?? 0;

    private static double? ParseScore(string? score)
        => double.TryParse(score, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;

    private string? AbsoluteImage(ShikiImage? image)
    {
        var path = image?.Original ?? image?.Preview;
        if (string.IsNullOrEmpty(path)) return null;
        return path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"{options.Value.BaseUrl.TrimEnd('/')}{path}";
    }

    private static string? FormatRating(string? rating)
        => string.IsNullOrEmpty(rating) || rating == "none"
            ? null
            : rating.Replace('_', '-').ToUpperInvariant();

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    // Descriptions carry Shikimori wiki markup: [character=123]Name[/character],
    // [[Term]], [spoiler]...[/spoiler]. Strip the brackets, keep the words.
    private static string? CleanDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var text = WikiLink().Replace(description, "$1");
        text = BbTag().Replace(text, "");
        text = text.Replace("[[", "").Replace("]]", "");
        return text.Trim();
    }

    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|[^\]]*)?\]\]")]
    private static partial Regex WikiLink();

    [GeneratedRegex(@"\[/?[a-zA-Z_]+(?:=[^\]]*)?\]")]
    private static partial Regex BbTag();
}
