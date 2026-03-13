namespace Anime.Mappers.DTOs;

public record ProfileMiniDto
{
	public required Guid Id { get; init; }
	public required string NickName { get; init; }
}
