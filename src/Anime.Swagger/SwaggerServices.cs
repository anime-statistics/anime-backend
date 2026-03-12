using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Anime.Swagger;

public static class SwaggerServices
{
	public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
	{
		services.AddOpenApiDocument();

		return services;
	}

	public static IApplicationBuilder UseSwaggerServices(this IApplicationBuilder app)
	{
		app.UseOpenApi(openApi =>
		{
			openApi.Path = "/Api/OpenApi/{documentName}/swagger.json";
		});

		app.UseSwaggerUi(swagger =>
		{
			swagger.Path = "/Api/Swagger";
			swagger.DocumentPath = "/Api/OpenApi/{documentName}/swagger.json";
		});

		app.UseReDoc(reDoc =>
		{
			reDoc.Path = "/Api/ReDoc";
			reDoc.DocumentPath = "/Api/OpenApi/{documentName}/swagger.json";
		});

		return app;
	}
}
