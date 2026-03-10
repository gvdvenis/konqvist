using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Admin.Features.Templates;

public sealed class GameTemplateAdminService(IDbContextFactory<KonqvistDbContext> dbContextFactory)
{
    private const int MaxTokenGenerationAttempts = 10;

    public async Task<IReadOnlyList<GameTemplateListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureGmTokensAsync(dbContext, cancellationToken);

        return await dbContext.GameTemplates
            .AsNoTracking()
            .OrderBy(template => template.Name)
            .Select(template => new GameTemplateListItem(
                template.Id,
                template.Name,
                template.GmLoginToken,
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

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = new GameTemplate
        {
            Name = input.Name.Trim(),
            GmLoginToken = await GenerateUniqueGmTokenAsync(dbContext, cancellationToken),
            TotalRounds = input.TotalRounds,
            LocationUpdateIntervalSeconds = input.LocationUpdateIntervalSeconds,
            MinLocationUpdateIntervalSeconds = input.MinLocationUpdateIntervalSeconds,
            VotingDurationSeconds = input.VotingDurationSeconds,
            PredictionBonusPoints = input.PredictionBonusPoints,
            VoteTimeoutPenalty = input.VoteTimeoutPenalty,
            DistrictCaptureRadiusMeters = input.DistrictCaptureRadiusMeters
        };

        dbContext.GameTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        return template.Id;
    }

    public async Task<SaveGameTemplateResult> UpdateAsync(
        int templateId,
        CreateGameTemplateInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = await dbContext.GameTemplates.FirstOrDefaultAsync(entity => entity.Id == templateId, cancellationToken);
        if (template is null)
        {
            return SaveGameTemplateResult.NotFound;
        }

        template.Name = input.Name.Trim();
        template.TotalRounds = input.TotalRounds;
        template.LocationUpdateIntervalSeconds = input.LocationUpdateIntervalSeconds;
        template.MinLocationUpdateIntervalSeconds = input.MinLocationUpdateIntervalSeconds;
        template.VotingDurationSeconds = input.VotingDurationSeconds;
        template.PredictionBonusPoints = input.PredictionBonusPoints;
        template.VoteTimeoutPenalty = input.VoteTimeoutPenalty;
        template.DistrictCaptureRadiusMeters = input.DistrictCaptureRadiusMeters;

        await dbContext.SaveChangesAsync(cancellationToken);
        return SaveGameTemplateResult.Saved;
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

    public async Task<RegenerateLoginTokenResult> RegenerateLoginTokenAsync(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = await dbContext.GameTemplates.FirstOrDefaultAsync(entity => entity.Id == templateId, cancellationToken);
        if (template is null)
        {
            return RegenerateLoginTokenResult.TemplateNotFound;
        }

        template.GmLoginToken = await GenerateUniqueGmTokenAsync(dbContext, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RegenerateLoginTokenResult.Regenerated;
    }

    private static async Task EnsureGmTokensAsync(
        KonqvistDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var templatesWithMissingToken = await dbContext.GameTemplates
            .Where(entity => string.IsNullOrWhiteSpace(entity.GmLoginToken))
            .ToListAsync(cancellationToken);
        if (templatesWithMissingToken.Count == 0)
        {
            return;
        }

        foreach (var template in templatesWithMissingToken)
        {
            template.GmLoginToken = await GenerateUniqueGmTokenAsync(dbContext, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<string> GenerateUniqueGmTokenAsync(
        KonqvistDbContext dbContext,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxTokenGenerationAttempts * 2; attempt++)
        {
            var candidate = LoginTokenGenerator.GenerateGmToken();
            var exists = await dbContext.GameTemplates
                .AsNoTracking()
                .AnyAsync(entity => entity.GmLoginToken == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate a unique login token.");
    }
}
