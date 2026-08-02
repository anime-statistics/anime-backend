using AnimeBackend.Domain.Media;

namespace AnimeBackend.Domain;

// Business rule violation. Maps to 400 with `{ "message": ... }` — the frontend
// shows the message verbatim, and never retries 4xx.
public class DomainException(string message) : Exception(message);

// Maps to 404.
public sealed class NotFoundException(string message) : DomainException(message);

// An upstream catalogue (Shikimori/AniLiberty) could not be reached. Maps to
// 424: a 5xx would make the frontend retry three times against a source that is
// down anyway.
public sealed class SourceUnavailableException : DomainException
{
    public SourceUnavailableException(MediaSource source)
        : base($"Источник {source.ToWire()} недоступен, попробуйте позже") { }

    public SourceUnavailableException(string message) : base(message) { }
}
