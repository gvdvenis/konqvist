using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Template;

public class TeamTemplateConfiguration : IEntityTypeConfiguration<TeamTemplate>
{
    public void Configure(EntityTypeBuilder<TeamTemplate> builder)
    {
        builder.ToTable("TeamTemplates");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Name)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entity => entity.Color)
            .HasMaxLength(7)
            .IsRequired();

        builder.HasIndex(entity => new { entity.GameTemplateId, entity.Name })
            .IsUnique();

        builder.HasOne(entity => entity.GameTemplate)
            .WithMany(entity => entity.Teams)
            .HasForeignKey(entity => entity.GameTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(TemplateSeedData.TeamTemplates);
    }
}
