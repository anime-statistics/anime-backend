using Microsoft.EntityFrameworkCore;

namespace Anime.Database.Core;

public class AnimeDbContext(DbContextOptions<AnimeDbContext> options)
	: DbContext(options), IAnimeDbContext
{
}
