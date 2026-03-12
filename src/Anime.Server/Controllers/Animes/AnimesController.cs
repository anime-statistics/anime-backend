using Anime.Database.Core;
using Anime.Server.DTOs.Anime;
using Anime.Server.DTOs.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Anime.Server.Controllers.Animes;

[Route(ApiPaths.AnimesPath)]
public class AnimesController(IAnimeDbContext dbContext) : ApiControllerBase
{
	[HttpGet]
	public async Task<PaginatedDto<AnimeDto>> GetSomeAsync
		([FromQuery] PaginationDto pagination, CancellationToken cancellation)
	{
		var items = await dbContext.Animes
			.AsNoTracking()
			.Include(anime => anime.Creator)
			.Include(anime => anime.Modifier)
			.Include(anime => anime.Tags)
			.Skip(pagination.Offset)
			.Take(pagination.Limit)
			.Select(anime => new AnimeDto
			{
				Id = anime.Id,

				CreatedAt = anime.CreatedAt,
				CreatorId = anime.CreatorId,
				Creator = anime.Creator,

				ModifiedAt = anime.ModifiedAt,
				ModifierId = anime.ModifierId,
				Modifier = anime.Modifier,

				ProfileId = anime.ProfileId,
				Profile = anime.Profile,

				Title = anime.Title,

				Tags = anime.Tags,
				TotalFiles = anime.Files.Count,
			})
			.ToListAsync(cancellation);

		var total = await dbContext.Animes.CountAsync(cancellation);

		return new PaginatedDto<AnimeDto>
		{
			Items = items,
			Total = total,

			Offset = pagination.Offset,
			Limit = pagination.Limit,
		};
	}
}
