using Konqvist.Infrastructure.Entities.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Session;

public class DistrictSessionConfiguration : IEntityTypeConfiguration<DistrictSession>
{
    public void Configure(EntityTypeBuilder<DistrictSession> builder)
    {
        builder.ToTable("DistrictSessions");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.CurrentOwnerTeamSessionId)
            .IsRequired(false);

        builder.Property(entity => entity.IsClaimedThisRound)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(entity => entity.LastClaimedAt)
            .IsRequired(false);

        builder.HasOne(entity => entity.GameSession)
            .WithMany(entity => entity.Districts)
            .HasForeignKey(entity => entity.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.DistrictTemplate)
            .WithMany(entity => entity.DistrictSessions)
            .HasForeignKey(entity => entity.DistrictTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.CurrentOwnerTeamSession)
            .WithMany(entity => entity.OwnedDistricts)
            .HasForeignKey(entity => entity.CurrentOwnerTeamSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
