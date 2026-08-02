using System.Net.Http.Headers;
using AnimeBackend.Application.Abstractions;
using AnimeBackend.Infrastructure.Files;
using AnimeBackend.Infrastructure.Persistence;
using AnimeBackend.Infrastructure.Sources.AniLiberty;
using AnimeBackend.Infrastructure.Sources.Shikimori;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AnimeBackend.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => options
            .UseSqlite(
                configuration.GetConnectionString("Default") ?? "Data Source=data/anime.db",
                sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        services.AddScoped<IAppDb>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<ShikimoriOptions>(configuration.GetSection(ShikimoriOptions.Section));
        services.Configure<AniLibertyOptions>(configuration.GetSection(AniLibertyOptions.Section));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        services.Configure<Ai.RouterAiOptions>(configuration.GetSection(Ai.RouterAiOptions.Section));

        services.AddHybridCache();

        // The frontend times out at 15s and retries 429/5xx itself, so the
        // total budget per source stays under that: 2 extra attempts, 5s each.
        services.AddHttpClient<ShikimoriClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<ShikimoriOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                resilience.Retry.MaxRetryAttempts = 2;
            });

        services.AddHttpClient<AniLibertyClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<AniLibertyOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                resilience.Retry.MaxRetryAttempts = 2;
            });

        services.AddTransient<ISourceClient>(sp => sp.GetRequiredService<ShikimoriClient>());
        services.AddTransient<ISourceClient>(sp => sp.GetRequiredService<AniLibertyClient>());

        // A tool-calling turn plus model latency can run well past a normal API
        // call, so this client gets a long timeout and no retries — replaying a
        // half-finished agent turn would double-charge and confuse the loop.
        services.AddHttpClient<Application.Ai.IAiChat, Ai.RouterAiChat>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<Ai.RouterAiOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddSingleton<IFileStorage, LocalFileStorage>();

        return services;
    }
}
