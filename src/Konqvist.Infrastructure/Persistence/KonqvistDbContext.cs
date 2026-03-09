using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Entities.Template;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Infrastructure.Persistence;

public class KonqvistDbContext(DbContextOptions<KonqvistDbContext> options) : DbContext(options)
{
    public DbSet<GameTemplate> GameTemplates => Set<GameTemplate>();
    public DbSet<TeamTemplate> TeamTemplates => Set<TeamTemplate>();
    public DbSet<PlayerTemplate> PlayerTemplates => Set<PlayerTemplate>();
    public DbSet<DistrictTemplate> DistrictTemplates => Set<DistrictTemplate>();
    public DbSet<RoundTemplate> RoundTemplates => Set<RoundTemplate>();

    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<TeamSession> TeamSessions => Set<TeamSession>();
    public DbSet<PlayerSession> PlayerSessions => Set<PlayerSession>();
    public DbSet<DistrictSession> DistrictSessions => Set<DistrictSession>();
    public DbSet<RoundSession> RoundSessions => Set<RoundSession>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<GameEvent> GameEvents => Set<GameEvent>();
    public DbSet<RoundSnapshot> RoundSnapshots => Set<RoundSnapshot>();
    public DbSet<DistrictOwnershipSnapshot> DistrictOwnershipSnapshots => Set<DistrictOwnershipSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KonqvistDbContext).Assembly);
    }
}
