using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Session;

public class RoundSessionConfiguration : IEntityTypeConfiguration<RoundSession>
{
    public void Configure(EntityTypeBuilder<RoundSession> builder)
    {
        builder.ToTable("RoundSessions");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasDefaultValue(RoundStatus.Gathering)
            .IsRequired();

        builder.Property(entity => entity.VotingEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(entity => entity.VotingStartedAt)
            .IsRequired(false);

        builder.Property(entity => entity.WinnerTeamSessionId)
            .IsRequired(false);

        builder.HasOne(entity => entity.GameSession)
            .WithMany(entity => entity.Rounds)
            .HasForeignKey(entity => entity.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.RoundTemplate)
            .WithMany(entity => entity.RoundSessions)
            .HasForeignKey(entity => entity.RoundTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.WinnerTeamSession)
            .WithMany(entity => entity.WonRounds)
            .HasForeignKey(entity => entity.WinnerTeamSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
