namespace AnimeBackend.Application.Common;

// `{ "items": [...], "total": n }` — tags, notes, search.
public sealed record ItemsResponse<T>(IReadOnlyList<T> Items, int Total);

// `{ "items": [...], "total": n, "page": p, "size": s }` — library lists.
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int Size);

public sealed record BulkUpdateResponse(int Updated);
