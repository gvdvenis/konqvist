using Konqvist.Infrastructure.Entities.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Session;

public class PlayerSessionConfiguration : IEntityTypeConfiguration<PlayerSession>
{
    public void Configure(EntityTypeBuilder<PlayerSession> builder)
    {
        builder.ToTable("PlayerSessions");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.IsLoggedIn)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(entity => entity.IsOnline)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(entity => entity.LastSeen)
            .IsRequired(false);

        builder.Property(entity => entity.LocationLat)
            .IsRequired(false);

        builder.Property(entity => entity.LocationLng)
            .IsRequired(false);

        builder.Property(entity => entity.LocationUpdatedAt)
            .IsRequired(false);

        builder.HasOne(entity => entity.GameSession)
            .WithMany(entity => entity.Players)
            .HasForeignKey(entity => entity.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.PlayerTemplate)
            .WithMany(entity => entity.PlayerSessions)
            .HasForeignKey(entity => entity.PlayerTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
