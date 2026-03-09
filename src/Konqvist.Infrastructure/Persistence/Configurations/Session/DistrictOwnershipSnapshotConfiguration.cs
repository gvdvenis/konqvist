using Konqvist.Infrastructure.Entities.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Session;

public class DistrictOwnershipSnapshotConfiguration : IEntityTypeConfiguration<DistrictOwnershipSnapshot>
{
    public void Configure(EntityTypeBuilder<DistrictOwnershipSnapshot> builder)
    {
        builder.ToTable("DistrictOwnershipSnapshots");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.OwnerTeamSessionId)
            .IsRequired(false);

        builder.Property(entity => entity.Phase)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(entity => entity.SnapshotTaken)
            .IsRequired();

        builder.HasIndex(entity => new { entity.RoundSessionId, entity.DistrictSessionId, entity.Phase })
            .IsUnique();

        builder.HasOne(entity => entity.RoundSession)
            .WithMany(entity => entity.OwnershipSnapshots)
            .HasForeignKey(entity => entity.RoundSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.DistrictSession)
            .WithMany(entity => entity.OwnershipSnapshots)
            .HasForeignKey(entity => entity.DistrictSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.OwnerTeamSession)
            .WithMany(entity => entity.OwnershipSnapshotsAsOwner)
            .HasForeignKey(entity => entity.OwnerTeamSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
