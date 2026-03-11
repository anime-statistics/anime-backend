using Anime.Database.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Anime.Database;

public static class DatabaseServices
{
	public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
	{
		var connectionString = configuration.GetConnectionString(IAnimeDbContext.ConnectionStringName);

		services.AddDbContext<AnimeDbContext>(options =>
		{
			options.UseNpgsql(connectionString);
		});

		services.AddScoped<IAnimeDbContext, AnimeDbContext>();

		return services;
	}
}
