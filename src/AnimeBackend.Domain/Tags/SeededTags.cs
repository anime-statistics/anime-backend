namespace AnimeBackend.Domain.Tags;

// The six starter tags created on first run. The UUIDs are shared with the
// frontend (`src/core/constants/seededTags.ts`) and must never change; the tags
// themselves carry no special powers and can be edited or deleted like any
// other.
public static class SeededTags
{
    public static readonly IReadOnlyList<(Guid Id, string Name, string Color, string Icon, int SortOrder)> All =
    [
        (Guid.Parse("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f701"), "Смотрю", "#22c55e", "pi-play", 0),
        (Guid.Parse("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f702"), "Запланировано", "#3b82f6", "pi-bookmark", 1),
        (Guid.Parse("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f703"), "Просмотрено", "#8b5cf6", "pi-check-circle", 2),
        (Guid.Parse("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f704"), "Отложено", "#f59e0b", "pi-pause", 3),
        (Guid.Parse("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f705"), "Брошено", "#ef4444", "pi-times-circle", 4),
        (Guid.Parse("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f706"), "Пересматриваю", "#06b6d4", "pi-replay", 5),
    ];
}
