using Anime.Database.Configurations.Templates;
using Anime.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anime.Database.Configurations;

public class AnimeConfiguration : BaseEntityConfiguration<AnimeEntity>
{
	public override void Configure(EntityTypeBuilder<AnimeEntity> builder)
	{
		base.Configure(builder);
		builder.ToTable("Animes");

		builder.HasOne(anime => anime.Profile)
			.WithMany(profile => profile.Animes)
			.HasForeignKey(anime => anime.ProfileId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Property(anime => anime.Title)
			.IsRequired()
			.HasMaxLength(255);
	}
}
