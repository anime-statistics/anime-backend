using AnimeBackend.Domain.Ai;
using AnimeBackend.Domain.Media;
using AnimeBackend.Domain.Notes;
using AnimeBackend.Domain.Tags;
using Microsoft.EntityFrameworkCore;

namespace AnimeBackend.Application.Abstractions;

public interface IAppDb
{
    DbSet<MediaItem> MediaItems { get; }
    DbSet<Tag> Tags { get; }
    DbSet<Note> Notes { get; }
    DbSet<AiModelUsage> AiModelUsages { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
