namespace Anime.Mappers.DTOs;

public record TagMiniDto
{
	public required Guid Id { get; init; }
	public required string Name { get; init; }
}
