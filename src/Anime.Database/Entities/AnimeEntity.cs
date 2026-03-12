using Anime.Database.Entities.Templates;

namespace Anime.Database.Entities;

public record AnimeEntity : ProfileEntityBase
{
	public required string Title { get; init; }

	public List<TagEntity> Tags { get; init; } = [];
	public List<FileEntity> Files { get; init; } = [];
}
