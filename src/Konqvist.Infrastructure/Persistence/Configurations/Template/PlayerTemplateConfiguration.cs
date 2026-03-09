using Konqvist.Infrastructure.Entities.Template;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Konqvist.Infrastructure.Persistence.Configurations.Template;

public class PlayerTemplateConfiguration : IEntityTypeConfiguration<PlayerTemplate>
{
    public void Configure(EntityTypeBuilder<PlayerTemplate> builder)
    {
        builder.ToTable("PlayerTemplates");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.LoginToken)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entity => entity.Role)
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(entity => entity.LoginToken)
            .IsUnique();

        builder.HasOne(entity => entity.TeamTemplate)
            .WithMany(entity => entity.Players)
            .HasForeignKey(entity => entity.TeamTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
