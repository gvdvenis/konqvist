using Microsoft.EntityFrameworkCore;

namespace Konqvist.Data.Infrastructure;

/// <summary>
///   EF Core context for the Azure SQL gameplay-state persistence layer.
///   Wiring (connection string, DI registration) is performed in #20.
/// </summary>
public class GameplayStateDbContext : DbContext
{
    public GameplayStateDbContext(DbContextOptions<GameplayStateDbContext> options)
        : base(options)
    {
    }

    public DbSet<GameplayStateEntity> GameplayStates => Set<GameplayStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameplayStateEntity>(entity =>
        {
            entity.ToTable("GameplayStates");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Slot)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.GameDefinitionId)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Payload)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(e => e.UpdatedAtUtc);

            entity.HasIndex(e => new { e.Slot, e.GameDefinitionId })
                .IsUnique()
                .HasDatabaseName("IX_GameplayStates_Slot_GameDefinitionId");

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_GameplayStates_Payload_IsJson",
                "[ISJSON]([Payload]) = 1"));
        });
    }
}
