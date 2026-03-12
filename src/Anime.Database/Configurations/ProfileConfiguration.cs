using Anime.Database.Configurations.Templates;
using Anime.Database.Data;
using Anime.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anime.Database.Configurations;

public class ProfileConfiguration : BaseEntityConfiguration<ProfileEntity>
{
	public override void Configure(EntityTypeBuilder<ProfileEntity> builder)
	{
		base.Configure(builder);
		builder.ToTable("Profiles");

		builder.Property(profile => profile.NickName)
			.IsRequired()
			.HasMaxLength(100);

		builder.HasData
		(
			ProfileEntityCollection.SystemProfile,
			ProfileEntityCollection.KotonaiProfile
		);
	}
}
