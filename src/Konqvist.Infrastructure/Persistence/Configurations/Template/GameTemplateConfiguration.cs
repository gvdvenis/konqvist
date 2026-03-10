using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Template;

public class GameTemplateConfiguration : IEntityTypeConfiguration<GameTemplate>
{
    public void Configure(EntityTypeBuilder<GameTemplate> builder)
    {
        builder.ToTable("GameTemplates");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entity => entity.GmLoginToken)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entity => entity.DistrictImportSourceUrl)
            .HasMaxLength(2048)
            .IsRequired(false);

        builder.Property(entity => entity.TotalRounds)
            .HasDefaultValue(4)
            .IsRequired();

        builder.Property(entity => entity.LocationUpdateIntervalSeconds)
            .HasDefaultValue(30)
            .IsRequired();

        builder.Property(entity => entity.MinLocationUpdateIntervalSeconds)
            .HasDefaultValue(5)
            .IsRequired();

        builder.Property(entity => entity.VotingDurationSeconds)
            .HasDefaultValue(30)
            .IsRequired();

        builder.Property(entity => entity.PredictionBonusPoints)
            .HasDefaultValue(150)
            .IsRequired();

        builder.Property(entity => entity.VoteTimeoutPenalty)
            .IsRequired();

        builder.Property(entity => entity.DistrictCaptureRadiusMeters)
            .HasDefaultValue(50d)
            .IsRequired();

        builder.HasData(TemplateSeedData.DemoGameTemplate);
    }
}
