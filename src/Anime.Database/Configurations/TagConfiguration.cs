using Anime.Database.Configurations.Templates;
using Anime.Database.Data;
using Anime.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anime.Database.Configurations;

public class TagConfiguration : BaseEntityConfiguration<TagEntity>
{
	public override void Configure(EntityTypeBuilder<TagEntity> builder)
	{
		base.Configure(builder);
		builder.ToTable("Tags");

		builder.HasOne(tag => tag.Anime)
			.WithMany(anime => anime.Tags)
			.HasForeignKey(tag => tag.AnimeId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Property(tag => tag.Name)
			.IsRequired()
			.HasMaxLength(100);

		builder.HasData
		(
			TagEntityCollection.Tag1,
			TagEntityCollection.Tag2,
			TagEntityCollection.Tag3
		);
	}
}
