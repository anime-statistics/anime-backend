using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AnimeBackend.Application.Abstractions;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AnimeBackend.Infrastructure.Sources.AniLiberty;

// Client for the AniLiberty (AniLibria) API v1. The catalogue is anime-only —
// manga queries return nothing. Detail requests must exclude the episodes
// payload: with it the endpoint streams for ~25 seconds, without it ~0.4s.
public sealed class AniLibertyClient(
    HttpClient http,
    HybridCache cache,
    IOptions<AniLibertyOptions> options) : ISourceClient
{
    private const string DetailExclude = "episodes,torrents,members,sponsor";

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

    public MediaSource Source => MediaSource.Aniliberty;

    public async Task<IReadOnlyList<MediaSnapshot>> SearchAsync(
        string query, MediaType type, CancellationToken ct)
    {
        if (type == MediaType.Manga) return [];

        var trimmed = query.Trim();
        var key = $"al:search:{trimmed.ToLowerInvariant()}";

        var result = await cache.GetOrCreateAsync<List<MediaSnapshot>>(
            key,
            async token =>
            {
                IReadOnlyList<AlRelease> releases;
                if (trimmed.Length > 0)
                {
                    releases = await GetAsync<List<AlRelease>>(
                        $"/api/v1/app/search/releases?query={Uri.EscapeDataString(trimmed)}", token) ?? [];
                }
                else
                {
                    var catalog = await GetAsync<AlCatalogResponse>(
                        $"/api/v1/anime/catalog/releases?page=1&limit={options.Value.CatalogPageSize}", token);
                    releases = catalog?.Data ?? [];
                }

                return [.. releases.Select(Map)];
            },
            SearchTtl,
            cancellationToken: ct);

        return result;
    }

    public async Task<MediaSnapshot?> GetDetailAsync(MediaId id, MediaType type, CancellationToken ct)
    {
        if (type == MediaType.Manga) return null;

        var key = $"al:detail:{id.NumericId}";

        return await cache.GetOrCreateAsync<MediaSnapshot?>(
            key,
            async token =>
            {
                var release = await GetAsync<AlRelease>(
                    $"/api/v1/anime/releases/{id.NumericId}?exclude={DetailExclude}", token);
                return release is null ? null : Map(release);
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
            throw new SourceUnavailableException(MediaSource.Aniliberty);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                throw new SourceUnavailableException(MediaSource.Aniliberty);
            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
    }

    private MediaSnapshot Map(AlRelease release)
    {
        // `name.english` is the romaji original ("Sousou no Frieren"),
        // `name.main` is the Russian localisation.
        var title = release.Name?.English ?? release.Name?.Main ?? $"release-{release.Id}";

        var links = new List<ExternalLink>();
        if (!string.IsNullOrEmpty(release.Alias))
        {
            links.Add(new ExternalLink(
                "aniliberty",
                $"{options.Value.AssetsBaseUrl.TrimEnd('/')}/anime/releases/release/{release.Alias}/episodes"));
        }

        return new MediaSnapshot
        {
            Id = MediaId.Build(MediaSource.Aniliberty, release.Id, title),
            Type = MediaType.Anime,
            Title = title,
            TitleRussian = release.Name?.Main,
            EpisodesTotal = release.EpisodesTotal ?? 0,
            ImageUrl = AbsolutePoster(release.Poster),
            Synopsis = string.IsNullOrWhiteSpace(release.Description) ? null : release.Description.Trim(),
            Genres = release.Genres is { Count: > 0 }
                ? [.. release.Genres.Select(g => g.Name ?? "").Where(g => g.Length > 0)]
                : null,
            Rating = release.AgeRating?.Label,
            DurationMinutes = release.AverageDurationOfEpisode is > 0 ? release.AverageDurationOfEpisode : null,
            ExternalLinks = links.Count > 0 ? links : null,
        };
    }

    private string? AbsolutePoster(AlPoster? poster)
    {
        var path = poster?.Optimized?.Src ?? poster?.Src ?? poster?.Preview;
        if (string.IsNullOrEmpty(path)) return null;
        return path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"{options.Value.AssetsBaseUrl.TrimEnd('/')}{path}";
    }
}
