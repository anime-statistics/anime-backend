using Anime.Database.Entities;
using Anime.Mappers.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace Anime.Mappers;

public static class MapperServices
{
	public static IServiceCollection AddMapperServices(this IServiceCollection services)
	{
		services.AddAutoMapper(config =>
		{
			config.CreateMap<AnimeEntity, AnimeFullDto>()
				.ForMember(dto => dto.TotalFiles, member => member.MapFrom(entity => entity.Files.Count));

			config.CreateMap<ProfileEntity, ProfileMiniDto>();
			config.CreateMap<TagEntity, TagMiniDto>();
		});

		return services;
	}
}
