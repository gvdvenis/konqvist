using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Admin.Features.Session;

public sealed class SessionAdminService(IDbContextFactory<KonqvistDbContext> dbContextFactory)
{
    public async Task<SessionManagementSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var templates = await dbContext.GameTemplates
            .AsNoTracking()
            .OrderBy(entity => entity.Name)
            .Select(entity => new SessionTemplateOption(entity.Id, entity.Name))
            .ToListAsync(cancellationToken);

        var currentSession = await dbContext.GameSessions
            .AsNoTracking()
            .Include(entity => entity.GameTemplate)
            .OrderByDescending(entity => entity.Status == GameStatus.Pending || entity.Status == GameStatus.Running)
            .ThenByDescending(entity => entity.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new SessionManagementSnapshot(
            templates,
            currentSession?.Id,
            currentSession?.GameTemplateId,
            currentSession?.GameTemplate.Name,
            currentSession?.Status,
            currentSession?.StartedAt);
    }

    public async Task<CreateGameSessionResult> CreateAsync(int templateId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var hasActiveSession = await dbContext.GameSessions
            .AsNoTracking()
            .AnyAsync(entity => entity.Status == GameStatus.Pending || entity.Status == GameStatus.Running, cancellationToken);
        if (hasActiveSession)
        {
            return CreateGameSessionResult.ActiveSessionExists;
        }

        return await CreatePendingSessionAsync(dbContext, templateId, cancellationToken)
            ? CreateGameSessionResult.Created
            : CreateGameSessionResult.TemplateNotFound;
    }

    public async Task<StartGameSessionResult> StartAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.GameSessions.FirstOrDefaultAsync(entity => entity.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return StartGameSessionResult.SessionNotFound;
        }

        if (session.Status != GameStatus.Pending)
        {
            return StartGameSessionResult.InvalidStatus;
        }

        session.Status = GameStatus.Running;
        session.StartedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return StartGameSessionResult.Started;
    }

    public async Task<ResetGameSessionResult> ResetAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.GameSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return ResetGameSessionResult.SessionNotFound;
        }

        if (session.Status != GameStatus.Finished)
        {
            return ResetGameSessionResult.InvalidStatus;
        }

        dbContext.GameSessions.RemoveRange(dbContext.GameSessions);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CreatePendingSessionAsync(dbContext, session.GameTemplateId, cancellationToken);
        return ResetGameSessionResult.Reset;
    }

    private static async Task<bool> CreatePendingSessionAsync(
        KonqvistDbContext dbContext,
        int templateId,
        CancellationToken cancellationToken)
    {
        var template = await dbContext.GameTemplates
            .Include(entity => entity.Teams)
            .ThenInclude(entity => entity.Players)
            .Include(entity => entity.Districts)
            .Include(entity => entity.Rounds)
            .FirstOrDefaultAsync(entity => entity.Id == templateId, cancellationToken);
        if (template is null)
        {
            return false;
        }

        var session = new GameSession
        {
            GameTemplateId = templateId,
            Status = GameStatus.Pending,
            CurrentPhase = GamePhase.WaitingForPlayers,
            StartedAt = null,
            FinishedAt = null,
            CurrentRoundSessionId = null
        };

        foreach (var teamTemplate in template.Teams.OrderBy(entity => entity.Id))
        {
            session.Teams.Add(new TeamSession
            {
                TeamTemplateId = teamTemplate.Id
            });

            foreach (var playerTemplate in teamTemplate.Players.OrderBy(entity => entity.Id))
            {
                session.Players.Add(new PlayerSession
                {
                    PlayerTemplateId = playerTemplate.Id
                });
            }
        }

        foreach (var districtTemplate in template.Districts.OrderBy(entity => entity.Id))
        {
            session.Districts.Add(new DistrictSession
            {
                DistrictTemplateId = districtTemplate.Id
            });
        }

        foreach (var roundTemplate in template.Rounds.OrderBy(entity => entity.RoundNumber))
        {
            session.Rounds.Add(new RoundSession
            {
                RoundTemplateId = roundTemplate.Id
            });
        }

        dbContext.GameSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
