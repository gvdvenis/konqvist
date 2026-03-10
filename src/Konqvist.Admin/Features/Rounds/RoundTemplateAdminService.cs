using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Admin.Features.Rounds;

public sealed class RoundTemplateAdminService(IDbContextFactory<KonqvistDbContext> dbContextFactory)
{
    private const int MinimumRoundCount = 1;

    public async Task<RoundTemplateManagementSnapshot?> GetAsync(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = await dbContext.GameTemplates
            .Include(entity => entity.Rounds)
            .FirstOrDefaultAsync(entity => entity.Id == templateId, cancellationToken);
        if (template is null)
        {
            return null;
        }

        var hasChanges = EnsureTemplateRounds(template);
        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var rounds = template.Rounds
            .OrderBy(entity => entity.RoundNumber)
            .Select(entity => new RoundTemplateListItem(
                entity.Id,
                entity.RoundNumber,
                entity.RoiResource,
                entity.Stake))
            .ToList();

        return new RoundTemplateManagementSnapshot(template.Id, template.Name, rounds);
    }

    public async Task<AddRoundTemplateResult> AddRoundAsync(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = await dbContext.GameTemplates
            .Include(entity => entity.Rounds)
            .FirstOrDefaultAsync(entity => entity.Id == templateId, cancellationToken);
        if (template is null)
        {
            return AddRoundTemplateResult.TemplateNotFound;
        }

        var nextRoundNumber = template.Rounds.Count == 0
            ? 1
            : template.Rounds.Max(entity => entity.RoundNumber) + 1;
        template.Rounds.Add(RoundTemplateDefaults.Create(nextRoundNumber));
        template.TotalRounds = template.Rounds.Count;
        await dbContext.SaveChangesAsync(cancellationToken);
        return AddRoundTemplateResult.Added;
    }

    public async Task<DeleteRoundTemplateResult> DeleteAsync(
        int templateId,
        int roundTemplateId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = await dbContext.GameTemplates
            .Include(entity => entity.Rounds)
            .FirstOrDefaultAsync(entity => entity.Id == templateId, cancellationToken);
        if (template is null)
        {
            return DeleteRoundTemplateResult.TemplateNotFound;
        }

        var roundTemplate = template.Rounds.FirstOrDefault(entity => entity.Id == roundTemplateId);
        if (roundTemplate is null)
        {
            return DeleteRoundTemplateResult.RoundNotFound;
        }

        if (template.Rounds.Count <= MinimumRoundCount)
        {
            return DeleteRoundTemplateResult.LastRoundRequired;
        }

        template.Rounds.Remove(roundTemplate);
        ReindexRoundNumbers(template.Rounds);
        template.TotalRounds = template.Rounds.Count;
        await dbContext.SaveChangesAsync(cancellationToken);
        return DeleteRoundTemplateResult.Deleted;
    }

    public async Task<SaveRoundTemplateResult> UpdateAsync(
        int templateId,
        int roundTemplateId,
        RoundTemplateEditorInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!TryNormalizeInput(input, out var normalizedStake))
        {
            return SaveRoundTemplateResult.InvalidInput;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await TemplateExistsAsync(dbContext, templateId, cancellationToken))
        {
            return SaveRoundTemplateResult.TemplateNotFound;
        }

        var roundTemplate = await dbContext.RoundTemplates.FirstOrDefaultAsync(
            entity => entity.Id == roundTemplateId && entity.GameTemplateId == templateId,
            cancellationToken);
        if (roundTemplate is null)
        {
            return SaveRoundTemplateResult.RoundNotFound;
        }

        roundTemplate.RoiResource = input.RoiResource;
        roundTemplate.Stake = normalizedStake;
        await dbContext.SaveChangesAsync(cancellationToken);
        return SaveRoundTemplateResult.Saved;
    }

    private static bool EnsureTemplateRounds(GameTemplate template)
    {
        var hasChanges = false;
        if (template.Rounds.Count == 0)
        {
            var initialRoundCount = template.TotalRounds > 0
                ? template.TotalRounds
                : RoundTemplateDefaults.DefaultRoundCount;
            for (var roundNumber = 1; roundNumber <= initialRoundCount; roundNumber++)
            {
                template.Rounds.Add(RoundTemplateDefaults.Create(roundNumber));
            }

            hasChanges = true;
        }

        hasChanges |= ReindexRoundNumbers(template.Rounds);
        if (template.TotalRounds == template.Rounds.Count)
        {
            return hasChanges;
        }

        template.TotalRounds = template.Rounds.Count;
        return true;
    }

    private static bool ReindexRoundNumbers(ICollection<RoundTemplate> rounds)
    {
        var hasChanges = false;
        var orderedRounds = rounds.OrderBy(entity => entity.RoundNumber).ToList();
        for (var index = 0; index < orderedRounds.Count; index++)
        {
            var expectedRoundNumber = index + 1;
            if (orderedRounds[index].RoundNumber == expectedRoundNumber)
            {
                continue;
            }

            orderedRounds[index].RoundNumber = expectedRoundNumber;
            hasChanges = true;
        }

        return hasChanges;
    }

    private static bool TryNormalizeInput(RoundTemplateEditorInput input, out string normalizedStake)
    {
        normalizedStake = input.Stake.Trim();
        return normalizedStake.Length is > 0 and <= 500;
    }

    private static async Task<bool> TemplateExistsAsync(
        KonqvistDbContext dbContext,
        int templateId,
        CancellationToken cancellationToken)
    {
        return await dbContext.GameTemplates
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == templateId, cancellationToken);
    }
}
