using AnimeBackend.Application.Abstractions;
using AnimeBackend.Application.Common;
using AnimeBackend.Application.Library;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;
using AnimeBackend.Domain.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnimeBackend.Application.Notes;

public sealed class NotesHandler(IAppDb db, Materializer materializer, ILogger<NotesHandler> logger)
{
    public async Task<ItemsResponse<NoteDto>> ListAsync(string? mediaId, CancellationToken ct)
    {
        var query = db.Notes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(mediaId))
            query = query.Where(n => n.MediaId == mediaId);

        var notes = await query.OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
        return new ItemsResponse<NoteDto>([.. notes.Select(NoteDto.From)], notes.Count);
    }

    public async Task<NoteDto> CreateAsync(string? mediaId, string? content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
            throw new DomainException("Не указана работа для заметки");

        var note = new Note(mediaId, content ?? "");

        // A note is user data tied to a media id, so the work is materialised
        // alongside it — but a dead source must never block note-taking.
        await TryMaterializeAsync(mediaId, ct);

        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        return NoteDto.From(note);
    }

    public async Task<NoteDto> UpdateAsync(string rawId, string? content, CancellationToken ct)
    {
        var note = await FindAsync(rawId, ct);
        note.UpdateContent(content ?? "");
        await db.SaveChangesAsync(ct);
        return NoteDto.From(note);
    }

    public async Task DeleteAsync(string rawId, CancellationToken ct)
    {
        var note = await FindAsync(rawId, ct);
        db.Notes.Remove(note);
        await db.SaveChangesAsync(ct);
    }

    private async Task TryMaterializeAsync(string mediaId, CancellationToken ct)
    {
        var exists = await db.MediaItems.AnyAsync(m => m.Id == mediaId, ct);
        if (exists) return;

        var parsed = MediaId.Parse(mediaId);
        foreach (var type in new[] { MediaType.Anime, MediaType.Manga })
        {
            try
            {
                var snapshot = await materializer.LoadSnapshotAsync(parsed, type, ct);
                if (snapshot is not null)
                {
                    db.MediaItems.Add(MediaItem.FromSnapshot(snapshot));
                    return;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Не удалось материализовать {MediaId} при создании заметки", mediaId);
                return;
            }
        }
    }

    private async Task<Note> FindAsync(string rawId, CancellationToken ct)
    {
        if (!Guid.TryParse(rawId, out var id))
            throw new DomainException("Некорректный идентификатор заметки");
        return await db.Notes.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new NotFoundException("Заметка не найдена");
    }
}

public sealed record AttachmentResponse(string Url);

public sealed class AttachmentsHandler(IFileStorage storage)
{
    private const long MaxSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions =
        [".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif"];

    public async Task<AttachmentResponse> SaveAsync(
        Stream content, long length, string fileName, CancellationToken ct)
    {
        if (length is <= 0 or > MaxSizeBytes)
            throw new DomainException("Файл должен быть не больше 10 МБ");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new DomainException("Поддерживаются только изображения (png, jpg, gif, webp, avif)");

        var url = await storage.SaveAsync(content, $"{Guid.NewGuid():N}{extension}", ct);
        return new AttachmentResponse(url);
    }
}
