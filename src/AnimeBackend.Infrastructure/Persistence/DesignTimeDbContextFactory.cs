using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AnimeBackend.Infrastructure.Persistence;

// Lets `dotnet ef migrations add` run without booting the API host.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=data/anime.db")
            .Options;
        return new AppDbContext(options);
    }
}
