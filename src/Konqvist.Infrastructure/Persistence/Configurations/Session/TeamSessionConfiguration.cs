using Konqvist.Infrastructure.Entities.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Session;

public class TeamSessionConfiguration : IEntityTypeConfiguration<TeamSession>
{
    public void Configure(EntityTypeBuilder<TeamSession> builder)
    {
        builder.ToTable("TeamSessions");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.TotalScore)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(entity => entity.TotalGold)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(entity => entity.TotalVoters)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(entity => entity.TotalLikes)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(entity => entity.TotalOil)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasOne(entity => entity.GameSession)
            .WithMany(entity => entity.Teams)
            .HasForeignKey(entity => entity.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.TeamTemplate)
            .WithMany(entity => entity.TeamSessions)
            .HasForeignKey(entity => entity.TeamTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
