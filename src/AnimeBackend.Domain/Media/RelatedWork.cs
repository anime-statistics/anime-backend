namespace AnimeBackend.Domain.Media;

public sealed record RelatedWork(string Id, string Title, string Relation);

public sealed record ExternalLink(string Source, string Url);
