using Anime.Database.Core;
using Anime.Mappers.DTOs;
using Anime.Server.DTOs.Pagination;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anime.Server.Controllers.Animes;

[Route(ApiPaths.AnimesPath)]
public class AnimesController
(
	IAnimeDbContext dbContext,
	IMapper mapper
)
	: ApiControllerBase
{
	[HttpGet]
	public async Task<PaginatedDto<AnimeFullDto>> GetSomeAsync
		([FromQuery] PaginationDto pagination, CancellationToken cancellation)
	{
		var items = await dbContext.Animes
			.AsNoTracking()
			.OrderByDescending(anime => anime.ModifiedAt)
			.ThenByDescending(anime => anime.CreatedAt)
			.Include(anime => anime.Creator)
			.Include(anime => anime.Modifier)
			.Include(anime => anime.Profile)
			.Include(anime => anime.Tags)
			.Skip(pagination.Offset)
			.Take(pagination.Limit)
			.Select(anime => mapper.Map<AnimeFullDto>(anime))
			.ToListAsync(cancellation);

		var total = await dbContext.Animes.CountAsync(cancellation);

		return new PaginatedDto<AnimeFullDto>
		{
			Items = items,
			Total = total,

			Offset = pagination.Offset,
			Limit = pagination.Limit,
		};
	}
}
