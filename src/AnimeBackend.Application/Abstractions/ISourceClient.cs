using AnimeBackend.Domain.Media;

namespace AnimeBackend.Application.Abstractions;

// One external catalogue. Search returns list-shaped snapshots; GetDetail
// returns the full card (related works, links, duration) or null when the
// source genuinely does not know the id. Connectivity failures surface as
// SourceUnavailableException so callers can degrade per use case: search drops
// the source, materialisation refuses.
public interface ISourceClient
{
    MediaSource Source { get; }

    Task<IReadOnlyList<MediaSnapshot>> SearchAsync(string query, MediaType type, CancellationToken ct);

    Task<MediaSnapshot?> GetDetailAsync(MediaId id, MediaType type, CancellationToken ct);
}
