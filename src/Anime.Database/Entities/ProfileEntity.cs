using Anime.Database.Entities.Templates;

namespace Anime.Database.Entities;

public record ProfileEntity : EntityBase
{
	public required string NickName { get; init; }

	public List<AnimeEntity> Animes { get; init; } = [];
}
