using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Admin.Features.Templates;

public sealed class GameTemplateAdminService(IDbContextFactory<KonqvistDbContext> dbContextFactory)
{
    public async Task<IReadOnlyList<GameTemplateListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.GameTemplates
            .AsNoTracking()
            .OrderBy(template => template.Name)
            .Select(template => new GameTemplateListItem(
                template.Id,
                template.Name,
                template.TotalRounds,
                template.LocationUpdateIntervalSeconds,
                template.MinLocationUpdateIntervalSeconds,
                template.VotingDurationSeconds,
                template.PredictionBonusPoints,
                template.VoteTimeoutPenalty,
                template.DistrictCaptureRadiusMeters,
                template.GameSessions.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateAsync(CreateGameTemplateInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var template = new GameTemplate
        {
            Name = input.Name.Trim(),
            TotalRounds = input.TotalRounds,
            LocationUpdateIntervalSeconds = input.LocationUpdateIntervalSeconds,
            MinLocationUpdateIntervalSeconds = input.MinLocationUpdateIntervalSeconds,
            VotingDurationSeconds = input.VotingDurationSeconds,
            PredictionBonusPoints = input.PredictionBonusPoints,
            VoteTimeoutPenalty = input.VoteTimeoutPenalty,
            DistrictCaptureRadiusMeters = input.DistrictCaptureRadiusMeters
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.GameTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        return template.Id;
    }

    public async Task<DeleteGameTemplateResult> DeleteAsync(int templateId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var hasLinkedSessions = await dbContext.Set<GameSession>()
            .AsNoTracking()
            .AnyAsync(session => session.GameTemplateId == templateId, cancellationToken);
        if (hasLinkedSessions)
        {
            return DeleteGameTemplateResult.HasLinkedSessions;
        }

        var template = await dbContext.GameTemplates.FirstOrDefaultAsync(
            entity => entity.Id == templateId,
            cancellationToken);
        if (template is null)
        {
            return DeleteGameTemplateResult.NotFound;
        }

        dbContext.GameTemplates.Remove(template);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return DeleteGameTemplateResult.HasLinkedSessions;
        }

        return DeleteGameTemplateResult.Deleted;
    }
}
