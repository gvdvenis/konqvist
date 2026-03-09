using Konqvist.Infrastructure.Entities.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Session;

public class RoundSnapshotConfiguration : IEntityTypeConfiguration<RoundSnapshot>
{
    public void Configure(EntityTypeBuilder<RoundSnapshot> builder)
    {
        builder.ToTable("RoundSnapshots");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Phase)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(entity => entity.Score)
            .IsRequired();

        builder.Property(entity => entity.Gold)
            .IsRequired();

        builder.Property(entity => entity.Voters)
            .IsRequired();

        builder.Property(entity => entity.Likes)
            .IsRequired();

        builder.Property(entity => entity.Oil)
            .IsRequired();

        builder.Property(entity => entity.SnapshotTaken)
            .IsRequired();

        builder.HasIndex(entity => new { entity.RoundSessionId, entity.TeamSessionId, entity.Phase })
            .IsUnique();

        builder.HasOne(entity => entity.RoundSession)
            .WithMany(entity => entity.Snapshots)
            .HasForeignKey(entity => entity.RoundSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.TeamSession)
            .WithMany(entity => entity.Snapshots)
            .HasForeignKey(entity => entity.TeamSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
