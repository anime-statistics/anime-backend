using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Anime.Logging;

public static class LoggingServices
{
	private const string AppStarting = "App Starting";
	public const string AppFatal = "App Fatal";

	public static void AddLoggingServices(this IServiceCollection services, IConfiguration configuration)
	{
		Log.Logger = new LoggerConfiguration()
			.ReadFrom.Configuration(configuration)
			.CreateLogger();

		Log.Information(AppStarting);

		services.AddSerilog(Log.Logger);
	}
}
