using Konqvist.Infrastructure.Entities.Template;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Template;

public class RoundTemplateConfiguration : IEntityTypeConfiguration<RoundTemplate>
{
    public void Configure(EntityTypeBuilder<RoundTemplate> builder)
    {
        builder.ToTable("RoundTemplates");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.RoundNumber)
            .IsRequired();

        builder.Property(entity => entity.RoiResource)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(entity => entity.Stake)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(entity => new { entity.GameTemplateId, entity.RoundNumber })
            .IsUnique();

        builder.HasOne(entity => entity.GameTemplate)
            .WithMany(entity => entity.Rounds)
            .HasForeignKey(entity => entity.GameTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
