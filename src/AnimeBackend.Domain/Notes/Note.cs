using AnimeBackend.Domain.Media;

namespace AnimeBackend.Domain.Notes;

// Markdown note attached to a work by its composite media id. Notes are
// independent of collection membership: they survive the last tag being
// removed.
public sealed class Note
{
    public Guid Id { get; private set; }
    public string MediaId { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Note() { }

    public Note(string mediaId, string content)
    {
        Media.MediaId.Parse(mediaId);
        Id = Guid.NewGuid();
        MediaId = mediaId;
        Content = content;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void UpdateContent(string content)
    {
        Content = content;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
