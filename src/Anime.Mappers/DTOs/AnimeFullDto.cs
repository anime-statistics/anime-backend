namespace Anime.Mappers.DTOs;

public record AnimeFullDto
{
	public required Guid Id { get; init; }

	public required DateTime CreatedAt { get; init; }
	public required Guid CreatorId { get; init; }
	public required ProfileMiniDto Creator { get; init; }

	public DateTime? ModifiedAt { get; init; }
	public Guid? ModifierId { get; init; }
	public ProfileMiniDto? Modifier { get; init; }

	// ===== ===== ===== ===== =====

	public required Guid ProfileId { get; init; }
	public required ProfileMiniDto Profile { get; init; }

	// ===== ===== ===== ===== =====

	public required string Title { get; init; }

	public required IReadOnlyList<TagMiniDto> Tags { get; init; }
	public required int TotalFiles { get; init; }
}
