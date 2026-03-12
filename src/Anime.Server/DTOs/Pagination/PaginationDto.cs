using System.ComponentModel.DataAnnotations;

namespace Anime.Server.DTOs.Pagination;

public record PaginationDto
{
	[Range(0, int.MaxValue)]
	public required int Offset { get; init; }

	[Range(1, 100)]
	public required int Limit { get; init; } = 10;
}
