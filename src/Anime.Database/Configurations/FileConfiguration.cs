using Anime.Database.Configurations.Templates;
using Anime.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anime.Database.Configurations;

public class FileConfiguration : BaseEntityConfiguration<FileEntity>
{
	public override void Configure(EntityTypeBuilder<FileEntity> builder)
	{
		base.Configure(builder);
		builder.ToTable("Files");

		builder.HasOne(file => file.Anime)
			.WithMany(anime => anime.Files)
			.HasForeignKey(file => file.AnimeId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Property(file => file.Name)
			.IsRequired()
			.HasMaxLength(255);
	}
}
