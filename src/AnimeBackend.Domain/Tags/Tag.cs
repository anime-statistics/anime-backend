using System.Text.RegularExpressions;

namespace AnimeBackend.Domain.Tags;

public sealed partial class Tag
{
    public const int NameMaxLength = 50;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Color { get; private set; } = null!;
    public string? Icon { get; private set; }
    public bool IsHidden { get; private set; }
    public int SortOrder { get; private set; }

    public ICollection<Media.MediaItem> MediaItems { get; private set; } = [];

    private Tag() { }

    public Tag(Guid id, string name, string color, string? icon, bool isHidden, int sortOrder)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Rename(name);
        Recolor(color);
        Icon = icon;
        IsHidden = isHidden;
        SetSortOrder(sortOrder);
    }

    public void Rename(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > NameMaxLength)
            throw new DomainException($"Название тега должно быть от 1 до {NameMaxLength} символов");
        Name = trimmed;
    }

    public void Recolor(string color)
    {
        if (!ColorPattern().IsMatch(color))
            throw new DomainException("Цвет тега должен быть в формате #rrggbb");
        Color = color.ToLowerInvariant();
    }

    public void SetIcon(string? icon) => Icon = string.IsNullOrWhiteSpace(icon) ? null : icon;

    public void SetHidden(bool hidden) => IsHidden = hidden;

    public void SetSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
            throw new DomainException("Порядок сортировки не может быть отрицательным");
        SortOrder = sortOrder;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorPattern();
}
