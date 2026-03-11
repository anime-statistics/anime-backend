using Anime.Database.Entities.Templates;

namespace Anime.Database.Entities;

public record FileEntity : AnimeEntityBase
{
	public required string Name { get; init; }
}
