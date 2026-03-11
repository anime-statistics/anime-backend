namespace Anime.Database.Entities.Templates;

public abstract record AnimeEntityBase : ProfileEntityBase
{
	public required Guid AnimeId { get; init; }
	public required AnimeEntity Anime { get; init; }
}
