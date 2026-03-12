namespace Anime.Database.Entities.Templates;

public abstract record ProfileEntityBase : EntityBase
{
	public required Guid ProfileId { get; init; }
	public ProfileEntity? Profile { get; init; }
}
