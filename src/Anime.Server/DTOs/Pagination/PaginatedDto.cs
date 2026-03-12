namespace Anime.Server.DTOs.Pagination;

public record PaginatedDto<T> : PaginationDto
{
	public required IReadOnlyList<T> Items { get; init; }
	public required int Total { get; init; }
}
