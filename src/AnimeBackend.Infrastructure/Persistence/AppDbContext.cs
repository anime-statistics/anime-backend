using System.Text.Json;
using AnimeBackend.Application.Abstractions;
using AnimeBackend.Domain.Media;
using AnimeBackend.Domain.Notes;
using AnimeBackend.Domain.Tags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AnimeBackend.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDb
{
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaItem>(media =>
        {
            media.HasKey(m => m.Id);
            media.Property(m => m.Id).ValueGeneratedNever().HasMaxLength(300);
            media.Property(m => m.Type).HasConversion<string>().HasMaxLength(10);
            media.Property(m => m.Source).HasConversion<string>().HasMaxLength(20);
            media.Property(m => m.SecondarySource).HasConversion<string>().HasMaxLength(20);
            media.Property(m => m.Title).HasMaxLength(500);

            // Complex collections persist as one JSON string column: honest,
            // portable across providers, and never queried inside SQL.
            ConfigureJsonColumn(media.Property(m => m.Related));
            ConfigureJsonColumn(media.Property(m => m.ExternalLinks));

            media.HasIndex(m => m.Type);

            media.HasMany(m => m.Tags)
                .WithMany(t => t.MediaItems)
                .UsingEntity("media_item_tags");
        });

        modelBuilder.Entity<Tag>(tag =>
        {
            tag.HasKey(t => t.Id);
            tag.Property(t => t.Id).ValueGeneratedNever();
            tag.Property(t => t.Name).HasMaxLength(Tag.NameMaxLength);
            tag.Property(t => t.Color).HasMaxLength(7);
            tag.Property(t => t.Icon).HasMaxLength(50);

            // First-run vocabulary with UUIDs shared with the frontend; plain
            // rows afterwards — rename, recolour, delete like any other tag.
            tag.HasData(SeededTags.All.Select(seed => new
            {
                seed.Id,
                seed.Name,
                seed.Color,
                Icon = (string?)seed.Icon,
                IsHidden = false,
                seed.SortOrder,
            }));
        });

        modelBuilder.Entity<Note>(note =>
        {
            note.HasKey(n => n.Id);
            note.Property(n => n.Id).ValueGeneratedNever();
            note.Property(n => n.MediaId).HasMaxLength(300);
            note.HasIndex(n => n.MediaId);
        });
    }

    // SQLite cannot ORDER BY DateTimeOffset stored as TEXT; as a sortable
    // 64-bit value it can. All values in this app are UTC, so binary order
    // matches chronological order.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter>();
    }

    private static void ConfigureJsonColumn<T>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<List<T>> property)
    {
        var converter = new ValueConverter<List<T>, string>(
            value => JsonSerializer.Serialize(value, JsonSerializerOptions.Default),
            stored => JsonSerializer.Deserialize<List<T>>(stored, JsonSerializerOptions.Default) ?? new List<T>());

        var comparer = new ValueComparer<List<T>>(
            (left, right) => (left ?? new List<T>()).SequenceEqual(right ?? new List<T>()),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            value => new List<T>(value));

        property.HasConversion(converter, comparer);
    }
}
