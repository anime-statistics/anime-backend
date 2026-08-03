using AnimeBackend.Domain.Tags;

namespace AnimeBackend.Domain.Media;

// One work the user has touched. Rows come into being lazily — the first tag,
// progress update or note materialises a catalogue snapshot locally — and are
// never deleted when the last tag goes away: notes and progress outlive
// collection membership, the library simply filters on `Tags.Count > 0`.
public sealed class MediaItem
{
    public string Id { get; private set; } = null!;
    public MediaType Type { get; private set; }
    public MediaSource Source { get; private set; }
    public MediaSource? SecondarySource { get; private set; }

    // Catalogue snapshot, refreshed whenever the source is consulted again.
    public string Title { get; private set; } = null!;
    public string? TitleRussian { get; private set; }
    public string? TitleJapanese { get; private set; }
    public string? TitleEnglish { get; private set; }
    public int EpisodesTotal { get; private set; }
    public int VolumesTotal { get; private set; }
    public int ChaptersTotal { get; private set; }
    public double? SourceScore { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? Synopsis { get; private set; }
    public List<string> Genres { get; private set; } = [];
    public string? AiredFrom { get; private set; }
    public string? AiredTo { get; private set; }
    public string? PublishedFrom { get; private set; }
    public string? PublishedTo { get; private set; }
    public string? Rating { get; private set; }
    public int? DurationMinutes { get; private set; }
    public List<string> Authors { get; private set; } = [];
    public List<RelatedWork> Related { get; private set; } = [];
    public List<ExternalLink> ExternalLinks { get; private set; } = [];
    public DateTimeOffset FetchedAt { get; private set; }

    // Set once the user corrects the links by hand: from then on they are user
    // state, and a fresh source snapshot leaves them alone.
    public bool ExternalLinksEdited { get; private set; }

    // Lower-cased concatenation of every title, maintained on refresh. SQLite's
    // LIKE/lower() are ASCII-only, so case folding happens here in .NET where
    // Cyrillic folds correctly; library search runs Contains() on this column.
    public string SearchText { get; private set; } = "";

    // User state — the only data the user owns.
    public double? UserScore { get; private set; }
    public int WatchedEpisodes { get; private set; }
    public int VolumesRead { get; private set; }
    public int ChaptersRead { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<Tag> Tags { get; private set; } = [];

    // The score shown to the user: their own mark wins over the catalogue's.
    public double? EffectiveScore => UserScore ?? SourceScore;

    // The other catalogue this work is known at, as the source badges show it.
    // A search merge records one; so does a link the user adds by hand, which
    // is the same statement made a different way — and the only way to say it
    // for a title the merge never matched. Derived rather than stored, so the
    // badge is right for rows written before links became editable.
    public MediaSource? EffectiveSecondarySource
        => SecondarySource ?? ExternalLinks
            .Select(link => MediaSourceExtensions.FromWire(link.Source?.ToLowerInvariant()))
            .FirstOrDefault(linked => linked is not null && linked != Source);

    private MediaItem() { }

    public static MediaItem FromSnapshot(MediaSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new MediaItem
        {
            Id = snapshot.Id.ToString(),
            Type = snapshot.Type,
            Source = snapshot.Source,
            CreatedAt = now,
            UpdatedAt = now,
        };
        item.RefreshSnapshot(snapshot);
        return item;
    }

    public void RefreshSnapshot(MediaSnapshot snapshot)
    {
        Title = snapshot.Title;
        TitleRussian = snapshot.TitleRussian;
        TitleJapanese = snapshot.TitleJapanese;
        TitleEnglish = snapshot.TitleEnglish;
        EpisodesTotal = snapshot.EpisodesTotal;
        VolumesTotal = snapshot.VolumesTotal;
        ChaptersTotal = snapshot.ChaptersTotal;
        SourceScore = snapshot.SourceScore;
        ImageUrl = snapshot.ImageUrl;
        Synopsis = snapshot.Synopsis;
        Genres = [.. snapshot.Genres ?? []];
        AiredFrom = snapshot.AiredFrom;
        AiredTo = snapshot.AiredTo;
        PublishedFrom = snapshot.PublishedFrom;
        PublishedTo = snapshot.PublishedTo;
        Rating = snapshot.Rating;
        DurationMinutes = snapshot.DurationMinutes;
        Authors = [.. snapshot.Authors ?? []];
        Related = [.. snapshot.Related ?? []];
        if (!ExternalLinksEdited) ExternalLinks = [.. snapshot.ExternalLinks ?? []];
        SecondarySource ??= snapshot.SecondarySource;
        SearchText = string.Join('\n',
            new[] { Title, TitleRussian, TitleJapanese, TitleEnglish }
                .Where(t => !string.IsNullOrEmpty(t)))
            .ToLowerInvariant();
        FetchedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProgress(double? score, int? watchedEpisodes, int? volumesRead, int? chaptersRead)
    {
        if (score is < 0 or > 10)
            throw new DomainException("Оценка должна быть от 0 до 10");
        if (watchedEpisodes < 0 || volumesRead < 0 || chaptersRead < 0)
            throw new DomainException("Прогресс не может быть отрицательным");
        if (Type == MediaType.Manga && watchedEpisodes is not null)
            throw new DomainException("У манги нет просмотренных эпизодов");
        if (Type == MediaType.Anime && (volumesRead is not null || chaptersRead is not null))
            throw new DomainException("У аниме нет прочитанных томов и глав");

        if (score is not null) UserScore = score;
        if (watchedEpisodes is not null) WatchedEpisodes = watchedEpisodes.Value;
        if (volumesRead is not null) VolumesRead = volumesRead.Value;
        if (chaptersRead is not null) ChaptersRead = chaptersRead.Value;
        Touch();
    }

    // Full replacement: the list the user sends is the list stored, an empty
    // one leaves the work with no links at all. Marks the links as hand-edited,
    // so `RefreshSnapshot` stops overwriting them from the source.
    public void ReplaceExternalLinks(IReadOnlyList<ExternalLink> links)
    {
        ExternalLinks = [.. links.Select(link => link.Normalized())];
        ExternalLinksEdited = true;
        Touch();
    }

    // Full replacement; an empty list removes the work from the collection.
    public void ReplaceTags(IReadOnlyCollection<Tag> tags)
    {
        Tags.Clear();
        foreach (var tag in tags.DistinctBy(t => t.Id))
            Tags.Add(tag);
        Touch();
    }

    public bool ApplyBulk(IReadOnlyCollection<Tag> add, IReadOnlySet<Guid> remove, bool clear)
    {
        var changed = false;

        if (clear)
        {
            changed = Tags.Count > 0;
            Tags.Clear();
        }
        else
        {
            foreach (var tag in Tags.Where(t => remove.Contains(t.Id)).ToList())
            {
                Tags.Remove(tag);
                changed = true;
            }
            foreach (var tag in add)
            {
                if (Tags.All(t => t.Id != tag.Id))
                {
                    Tags.Add(tag);
                    changed = true;
                }
            }
        }

        if (changed) Touch();
        return changed;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
