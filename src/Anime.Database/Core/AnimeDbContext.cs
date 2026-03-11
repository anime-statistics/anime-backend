using Anime.Database.Entities;
using Anime.Database.Entities.Templates;
using Microsoft.EntityFrameworkCore;

namespace Anime.Database.Core;

public class AnimeDbContext(DbContextOptions<AnimeDbContext> options)
	: DbContext(options), IAnimeDbContext
{
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnimeDbContext).Assembly);

		modelBuilder.Ignore<ProfileEntityBase>();
		modelBuilder.Ignore<AnimeEntityBase>();
	}

	public DbSet<ProfileEntity> Profiles => Set<ProfileEntity>();
	public DbSet<AnimeEntity> Animes => Set<AnimeEntity>();
	public DbSet<TagEntity> Tags => Set<TagEntity>();
	public DbSet<FileEntity> Files => Set<FileEntity>();
}
