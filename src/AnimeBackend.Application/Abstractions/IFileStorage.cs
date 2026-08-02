namespace AnimeBackend.Application.Abstractions;

public interface IFileStorage
{
    // Stores the payload and returns the public URL path it will be served from.
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct);
}
