using Anime.Database.Entities.Templates;

namespace Anime.Database.Entities;

public record TagEntity : AnimeEntityBase
{
	public required string Name { get; init; }
}
