using Konqvist.Infrastructure.Entities.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Session;

public class GameEventConfiguration : IEntityTypeConfiguration<GameEvent>
{
    public void Configure(EntityTypeBuilder<GameEvent> builder)
    {
        builder.ToTable("GameEvents");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.RoundSessionId)
            .IsRequired(false);

        builder.Property(entity => entity.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entity => entity.Payload)
            .IsRequired();

        builder.Property(entity => entity.OccurredAt)
            .IsRequired();

        builder.Property(entity => entity.ActorPlayerSessionId)
            .IsRequired(false);

        builder.HasOne(entity => entity.GameSession)
            .WithMany(entity => entity.Events)
            .HasForeignKey(entity => entity.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.RoundSession)
            .WithMany(entity => entity.Events)
            .HasForeignKey(entity => entity.RoundSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.ActorPlayerSession)
            .WithMany(entity => entity.ActorEvents)
            .HasForeignKey(entity => entity.ActorPlayerSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
