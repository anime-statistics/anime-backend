using Anime.Database.Entities;

namespace Anime.Server.DTOs.Anime;

public record AnimeDto
{
	public required Guid Id { get; init; }

	public DateTime CreatedAt { get; init; }
	public required Guid CreatorId { get; init; }
	public ProfileEntity? Creator { get; init; }

	public DateTime? ModifiedAt { get; init; }
	public Guid? ModifierId { get; init; }
	public ProfileEntity? Modifier { get; init; }

	// ===== ===== ===== ===== =====

	public required Guid ProfileId { get; init; }
	public ProfileEntity? Profile { get; init; }

	// ===== ===== ===== ===== =====

	public required string Title { get; init; }

	public required IReadOnlyList<TagEntity> Tags { get; init; }
	public required int TotalFiles { get; init; }
}
