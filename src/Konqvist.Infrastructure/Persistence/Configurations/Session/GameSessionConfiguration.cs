using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Session;

public class GameSessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    public void Configure(EntityTypeBuilder<GameSession> builder)
    {
        builder.ToTable("GameSessions");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasDefaultValue(GameStatus.Pending)
            .IsRequired();

        builder.Property(entity => entity.CurrentPhase)
            .HasConversion<string>()
            .HasDefaultValue(GamePhase.WaitingForPlayers)
            .IsRequired();

        builder.Property(entity => entity.StartedAt)
            .IsRequired(false);

        builder.Property(entity => entity.FinishedAt)
            .IsRequired(false);

        builder.Property(entity => entity.CurrentRoundSessionId)
            .IsRequired(false);

        builder.HasOne(entity => entity.GameTemplate)
            .WithMany(entity => entity.GameSessions)
            .HasForeignKey(entity => entity.GameTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.CurrentRoundSession)
            .WithMany()
            .HasForeignKey(entity => entity.CurrentRoundSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
