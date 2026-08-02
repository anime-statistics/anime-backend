using AnimeBackend.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AnimeBackend.Infrastructure.Files;

// Attachments land in a local folder that the host serves as static files.
// Files are uploaded before the note referencing them is saved, so rows are
// intentionally not tracked in the database — orphans are cheap and harmless
// for a single-user app.
public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct)
    {
        var root = Path.GetFullPath(options.Value.AttachmentsPath);
        Directory.CreateDirectory(root);

        var target = Path.Combine(root, fileName);
        await using (var file = File.Create(target))
        {
            await content.CopyToAsync(file, ct);
        }

        return $"{options.Value.PublicPath.TrimEnd('/')}/{fileName}";
    }
}
