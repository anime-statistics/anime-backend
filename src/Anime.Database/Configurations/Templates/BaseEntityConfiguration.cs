using Anime.Database.Entities.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Anime.Database.Configurations.Templates;

public abstract class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
	where TEntity : EntityBase
{
	public virtual void Configure(EntityTypeBuilder<TEntity> builder)
	{
		builder.Property(entity => entity.Id)
			.ValueGeneratedNever();

		builder.Property(entity => entity.CreatedAt).IsRequired();
		builder.Property(entity => entity.CreatorId).IsRequired();
		builder.HasOne(entity => entity.Creator)
			.WithMany()
			.HasForeignKey(entity => entity.CreatorId)
			.OnDelete(DeleteBehavior.Restrict); // Запрещаем удаление создателя

		builder.Property(entity => entity.ModifiedAt).IsRequired(false);
		builder.Property(entity => entity.ModifierId).IsRequired(false);
		builder.HasOne(entity => entity.Modifier)
			.WithMany()
			.HasForeignKey(entity => entity.ModifierId)
			.OnDelete(DeleteBehavior.SetNull); // При удалении пользователя ставим NULL в ModifierId
	}
}
