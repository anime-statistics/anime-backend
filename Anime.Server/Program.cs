
{
	var builder = WebApplication.CreateBuilder(args);
	ServerSetup(builder);

	var app = builder.Build();
	ServerLaunch(app);
}

return;

// Настройка Сервера
void ServerSetup(WebApplicationBuilder builder)
{
	builder.Services.AddControllers();
}

// Запуск Сервера
void ServerLaunch(WebApplication app)
{
	app.MapControllers();

	app.Run();
}
