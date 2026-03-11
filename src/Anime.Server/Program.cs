
using Anime.Database;
using Anime.Logging;
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

	builder.Services.AddControllers();
}

// Запуск Сервера
void ServerLaunch(WebApplication app)
{
	app.MapControllers();

	app.Run();
}
