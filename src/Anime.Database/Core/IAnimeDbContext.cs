using Anime.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Anime.Database.Core;

public interface IAnimeDbContext
{
	public const string ConnectionStringName = "AnimeDatabase";

	public DbSet<ProfileEntity> Profiles { get; }
	public DbSet<AnimeEntity> Animes { get; }
	public DbSet<TagEntity> Tags { get; }
	public DbSet<FileEntity> Files { get; }
}
