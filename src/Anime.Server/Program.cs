
using Anime.Database;
using Anime.Logging;
using Anime.Mappers;
using Anime.Swagger;
using Serilog;

try
{
	var builder = WebApplication.CreateBuilder(args);
	ServerSetup(builder);

	var app = builder.Build();
	ServerLaunch(app);
}
catch (Exception exception)
{
	Log.Fatal(exception, LoggingServices.AppFatal);
}
finally
{
	Log.CloseAndFlush();
}

return;

// Настройка Сервера
void ServerSetup(WebApplicationBuilder builder)
{
	builder.Services.AddLoggingServices(builder.Configuration);
	builder.Services.AddDatabaseServices(builder.Configuration);
	builder.Services.AddMapperServices();

	builder.Services.AddControllers();
	builder.Services.AddSwaggerServices();
}

// Запуск Сервера
void ServerLaunch(WebApplication app)
{
	if (app.Environment.IsDevelopment())
	{
		app.UseDeveloperExceptionPage();
	}

	app.UseRouting();
	app.UseSerilogRequestLogging();

	app.UseStaticFiles();
	app.UseDefaultFiles();

	app.UseSwaggerServices();
	app.MapControllers();

	app.Run();
}
