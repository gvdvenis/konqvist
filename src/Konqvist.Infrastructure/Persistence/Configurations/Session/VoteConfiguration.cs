using Konqvist.Infrastructure.Entities.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Session;

public class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("Votes");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.VoteValue)
            .IsRequired();

        builder.Property(entity => entity.IsAutocast)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(entity => entity.CastAt)
            .IsRequired();

        builder.HasIndex(entity => new { entity.RoundSessionId, entity.VotingTeamSessionId })
            .IsUnique();

        builder.HasOne(entity => entity.RoundSession)
            .WithMany(entity => entity.Votes)
            .HasForeignKey(entity => entity.RoundSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.VotingTeamSession)
            .WithMany(entity => entity.VotesCast)
            .HasForeignKey(entity => entity.VotingTeamSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.TargetTeamSession)
            .WithMany(entity => entity.VotesTargeted)
            .HasForeignKey(entity => entity.TargetTeamSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
