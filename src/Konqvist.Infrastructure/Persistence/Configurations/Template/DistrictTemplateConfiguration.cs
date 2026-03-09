using Konqvist.Infrastructure.Entities.Template;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Template;

public class DistrictTemplateConfiguration : IEntityTypeConfiguration<DistrictTemplate>
{
    public void Configure(EntityTypeBuilder<DistrictTemplate> builder)
    {
        builder.ToTable("DistrictTemplates");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entity => entity.GeoJson)
            .IsRequired();

        builder.Property(entity => entity.TriggerLat)
            .IsRequired();

        builder.Property(entity => entity.TriggerLng)
            .IsRequired();

        builder.Property(entity => entity.TriggerRadiusMeters)
            .IsRequired(false);

        builder.Property(entity => entity.Gold)
            .IsRequired();

        builder.Property(entity => entity.Voters)
            .IsRequired();

        builder.Property(entity => entity.Likes)
            .IsRequired();

        builder.Property(entity => entity.Oil)
            .IsRequired();

        builder.HasOne(entity => entity.GameTemplate)
            .WithMany(entity => entity.Districts)
            .HasForeignKey(entity => entity.GameTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
