using AnimeBackend.Application.Abstractions;
using AnimeBackend.Application.Ai;
using AnimeBackend.Application.Search;
using AnimeBackend.Domain.Media;
using AnimeBackend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AnimeBackend.Api.Tests;

// Boots the real host against a shared in-memory SQLite database and fake
// source clients, so tests cover the full pipeline: routing, snake_case JSON,
// error shape, EF model, tag seed migration.
public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    public static readonly TimeSpan SourceBudget = TimeSpan.FromMilliseconds(300);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public FakeSourceClient Shikimori { get; } = new(MediaSource.Shikimori);

    public FakeSourceClient Aniliberty { get; } = new(MediaSource.Aniliberty);

    public FakeAiChat Ai { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<ISourceClient>();
            services.AddSingleton<ISourceClient>(Shikimori);
            services.AddSingleton<ISourceClient>(Aniliberty);

            services.RemoveAll<IAiChat>();
            services.AddSingleton<IAiChat>(Ai);

            // Same handler, a budget a test can afford to wait out.
            services.RemoveAll<SearchHandler>();
            services.AddScoped(sp => new SearchHandler(
                sp.GetRequiredService<IAppDb>(),
                sp.GetServices<ISourceClient>(),
                sp.GetRequiredService<ILogger<SearchHandler>>(),
                SourceBudget));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<TService>(this IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(TService)).ToList())
            services.Remove(descriptor);
    }
}
