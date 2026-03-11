using Anime.Database.Entities.Templates;

namespace Anime.Database.Entities;

public record ProfileEntity : EntityBase
{
	public required string Name { get; init; }

	public required List<AnimeEntity> Animes { get; init; }
}
