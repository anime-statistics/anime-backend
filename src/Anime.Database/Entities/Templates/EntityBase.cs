namespace Anime.Database.Entities.Templates;

public abstract record EntityBase
{
	public required Guid Id { get; init; }

	// ===== ===== ===== ===== =====

	public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
	public required Guid CreatorId { get; init; }
	public ProfileEntity? Creator { get; init; }

	// ===== ===== ===== ===== =====

	public DateTime? ModifiedAt { get; set; }
	public Guid? ModifierId { get; set; }
	public ProfileEntity? Modifier { get; set; }
}
