using AnimeBackend.Application.Abstractions;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace AnimeBackend.Application.Library;

// Local rows come into being the moment the user first touches a work: the
// snapshot is pulled from its source and stored. Callers own SaveChanges.
public sealed class Materializer(IAppDb db, IEnumerable<ISourceClient> clients)
{
    public async Task<MediaItem> GetOrLoadAsync(string rawId, MediaType type, CancellationToken ct)
    {
        var id = MediaId.Parse(rawId);

        var existing = await db.MediaItems
            .Include(m => m.Tags)
            .FirstOrDefaultAsync(m => m.Id == rawId, ct);
        if (existing is not null)
        {
            if (existing.Type != type)
                throw new NotFoundException("Работа не найдена");
            return existing;
        }

        var snapshot = await LoadSnapshotAsync(id, type, ct)
            ?? throw new NotFoundException("Работа не найдена в источнике");
        var item = MediaItem.FromSnapshot(snapshot);
        db.MediaItems.Add(item);
        return item;
    }

    public Task<MediaSnapshot?> LoadSnapshotAsync(MediaId id, MediaType type, CancellationToken ct)
    {
        var client = clients.FirstOrDefault(c => c.Source == id.Source)
            ?? throw new SourceUnavailableException(id.Source);
        return client.GetDetailAsync(id, type, ct);
    }
}
