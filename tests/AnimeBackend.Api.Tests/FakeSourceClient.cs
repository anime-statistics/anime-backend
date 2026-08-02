using AnimeBackend.Application.Abstractions;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;

namespace AnimeBackend.Api.Tests;

public sealed class FakeSourceClient(MediaSource source) : ISourceClient
{
    private readonly List<MediaSnapshot> _catalogue = [];

    public bool IsDown { get; set; }

    public MediaSource Source => source;

    public FakeSourceClient Add(MediaSnapshot snapshot)
    {
        _catalogue.Add(snapshot);
        return this;
    }

    public FakeSourceClient AddAnime(long id, string title, int episodes = 12, string? titleRussian = null)
    {
        Add(new MediaSnapshot
        {
            Id = MediaId.Build(source, id, title),
            Type = MediaType.Anime,
            Title = title,
            TitleRussian = titleRussian,
            EpisodesTotal = episodes,
        });
        return this;
    }

    public Task<IReadOnlyList<MediaSnapshot>> SearchAsync(string query, MediaType type, CancellationToken ct)
    {
        if (IsDown) throw new SourceUnavailableException(source);

        var trimmed = query.Trim();
        IReadOnlyList<MediaSnapshot> result = [.. _catalogue
            .Where(s => s.Type == type)
            .Where(s => trimmed.Length == 0
                || s.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || s.TitleRussian?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) == true)];
        return Task.FromResult(result);
    }

    public Task<MediaSnapshot?> GetDetailAsync(MediaId id, MediaType type, CancellationToken ct)
    {
        if (IsDown) throw new SourceUnavailableException(source);

        return Task.FromResult(_catalogue.FirstOrDefault(
            s => s.Id.NumericId == id.NumericId && s.Type == type));
    }
}
