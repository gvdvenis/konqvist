using System.Text.RegularExpressions;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Admin.Features.Teams;

public sealed partial class TeamTemplateAdminService(IDbContextFactory<KonqvistDbContext> dbContextFactory)
{
    private const int MinGeneratedTeamCount = 1;
    private const int MaxGeneratedTeamCount = 10;
    private static readonly IReadOnlyList<string> NatoTeamNames =
    [
        "Alpha",
        "Bravo",
        "Charlie",
        "Delta",
        "Echo",
        "Foxtrot",
        "Golf",
        "Hotel",
        "India",
        "Juliett"
    ];

    public async Task<TeamTemplateManagementSnapshot?> GetAsync(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .Where(entity => entity.Id == templateId)
            .Select(entity => new { entity.Id, entity.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (template is null)
        {
            return null;
        }

        var teams = await dbContext.TeamTemplates
            .AsNoTracking()
            .Where(entity => entity.GameTemplateId == templateId)
            .OrderBy(entity => entity.Name)
            .Select(entity => new TeamTemplateListItem(
                entity.Id,
                entity.Name,
                entity.Color))
            .ToListAsync(cancellationToken);

        return new TeamTemplateManagementSnapshot(template.Id, template.Name, teams);
    }

    public async Task<SaveTeamTemplateResult> CreateAsync(
        int templateId,
        TeamTemplateEditorInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!TryNormalizeInput(input, out var normalizedName, out var normalizedColor))
        {
            return SaveTeamTemplateResult.InvalidInput;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await TemplateExistsAsync(dbContext, templateId, cancellationToken))
        {
            return SaveTeamTemplateResult.TemplateNotFound;
        }

        if (await DuplicateNameExistsAsync(dbContext, templateId, normalizedName, null, cancellationToken))
        {
            return SaveTeamTemplateResult.DuplicateName;
        }

        dbContext.TeamTemplates.Add(new TeamTemplate
        {
            GameTemplateId = templateId,
            Name = normalizedName,
            Color = normalizedColor
        });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateNameViolation(exception))
        {
            return SaveTeamTemplateResult.DuplicateName;
        }

        return SaveTeamTemplateResult.Saved;
    }

    public async Task<SaveTeamTemplateResult> UpdateAsync(
        int templateId,
        int teamTemplateId,
        TeamTemplateEditorInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!TryNormalizeInput(input, out var normalizedName, out var normalizedColor))
        {
            return SaveTeamTemplateResult.InvalidInput;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await TemplateExistsAsync(dbContext, templateId, cancellationToken))
        {
            return SaveTeamTemplateResult.TemplateNotFound;
        }

        var teamTemplate = await dbContext.TeamTemplates.FirstOrDefaultAsync(
            entity => entity.Id == teamTemplateId && entity.GameTemplateId == templateId,
            cancellationToken);
        if (teamTemplate is null)
        {
            return SaveTeamTemplateResult.TeamNotFound;
        }

        if (await DuplicateNameExistsAsync(dbContext, templateId, normalizedName, teamTemplateId, cancellationToken))
        {
            return SaveTeamTemplateResult.DuplicateName;
        }

        teamTemplate.Name = normalizedName;
        teamTemplate.Color = normalizedColor;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateNameViolation(exception))
        {
            return SaveTeamTemplateResult.DuplicateName;
        }

        return SaveTeamTemplateResult.Saved;
    }

    public async Task<DeleteTeamTemplateResult> DeleteAsync(
        int templateId,
        int teamTemplateId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await TemplateExistsAsync(dbContext, templateId, cancellationToken))
        {
            return DeleteTeamTemplateResult.TemplateNotFound;
        }

        var teamTemplate = await dbContext.TeamTemplates.FirstOrDefaultAsync(
            entity => entity.Id == teamTemplateId && entity.GameTemplateId == templateId,
            cancellationToken);
        if (teamTemplate is null)
        {
            return DeleteTeamTemplateResult.TeamNotFound;
        }

        dbContext.TeamTemplates.Remove(teamTemplate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return DeleteTeamTemplateResult.Deleted;
    }

    public async Task<BulkReplaceTeamTemplateResult> ReplaceAllWithGeneratedAsync(
        int templateId,
        int teamCount,
        CancellationToken cancellationToken = default)
    {
        if (teamCount is < MinGeneratedTeamCount or > MaxGeneratedTeamCount)
        {
            return BulkReplaceTeamTemplateResult.InvalidTeamCount;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await TemplateExistsAsync(dbContext, templateId, cancellationToken))
        {
            return BulkReplaceTeamTemplateResult.TemplateNotFound;
        }

        var existingTeams = await dbContext.TeamTemplates
            .Where(entity => entity.GameTemplateId == templateId)
            .ToListAsync(cancellationToken);
        dbContext.TeamTemplates.RemoveRange(existingTeams);
        dbContext.TeamTemplates.AddRange(GenerateTeams(templateId, teamCount));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateNameViolation(exception))
        {
            return BulkReplaceTeamTemplateResult.DuplicateName;
        }
        catch (DbUpdateException exception) when (IsTemplateForeignKeyViolation(exception))
        {
            return BulkReplaceTeamTemplateResult.TemplateNotFound;
        }

        return BulkReplaceTeamTemplateResult.Replaced;
    }

    private static bool TryNormalizeInput(
        TeamTemplateEditorInput input,
        out string normalizedName,
        out string normalizedColor)
    {
        normalizedName = input.Name.Trim();
        normalizedColor = input.Color.Trim().ToUpperInvariant();
        return normalizedName.Length > 0
               && normalizedName.Length <= 50
               && HexColorRegex().IsMatch(normalizedColor);
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

    private static async Task<bool> DuplicateNameExistsAsync(
        KonqvistDbContext dbContext,
        int templateId,
        string normalizedName,
        int? excludedTeamTemplateId,
        CancellationToken cancellationToken)
    {
        return await dbContext.TeamTemplates
            .AsNoTracking()
            .AnyAsync(entity =>
                entity.GameTemplateId == templateId
                && entity.Id != excludedTeamTemplateId
                && EF.Functions.Collate(entity.Name, "NOCASE") == normalizedName,
                cancellationToken);
    }

    private static bool IsDuplicateNameViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
               && sqliteException.SqliteErrorCode == 19
               && sqliteException.SqliteExtendedErrorCode == 2067
               && sqliteException.Message.Contains(
                    "UNIQUE constraint failed: TeamTemplates.GameTemplateId, TeamTemplates.Name",
                    StringComparison.Ordinal);
    }

    private static bool IsTemplateForeignKeyViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
               && sqliteException.SqliteErrorCode == 19
               && sqliteException.SqliteExtendedErrorCode == 787
               && sqliteException.Message.Contains("TeamTemplates.GameTemplateId", StringComparison.Ordinal);
    }

    private static IReadOnlyList<TeamTemplate> GenerateTeams(int templateId, int teamCount)
    {
        var generatedTeams = new List<TeamTemplate>(teamCount);
        for (var index = 0; index < teamCount; index++)
        {
            generatedTeams.Add(new TeamTemplate
            {
                GameTemplateId = templateId,
                Name = NatoTeamNames[index],
                Color = GenerateHexColor(teamCount, index)
            });
        }

        return generatedTeams;
    }

    private static string GenerateHexColor(int totalCount, int index)
    {
        var hue = 360d * index / totalCount;
        return HsvToHex(hue, saturation: 0.7d, value: 0.9d);
    }

    private static string HsvToHex(double hue, double saturation, double value)
    {
        var normalizedHue = (hue % 360d + 360d) % 360d;
        var chroma = value * saturation;
        var hueSegment = normalizedHue / 60d;
        var secondary = chroma * (1d - Math.Abs(hueSegment % 2d - 1d));
        var match = value - chroma;

        double redPrime;
        double greenPrime;
        double bluePrime;
        if (hueSegment < 1d)
        {
            redPrime = chroma;
            greenPrime = secondary;
            bluePrime = 0d;
        }
        else if (hueSegment < 2d)
        {
            redPrime = secondary;
            greenPrime = chroma;
            bluePrime = 0d;
        }
        else if (hueSegment < 3d)
        {
            redPrime = 0d;
            greenPrime = chroma;
            bluePrime = secondary;
        }
        else if (hueSegment < 4d)
        {
            redPrime = 0d;
            greenPrime = secondary;
            bluePrime = chroma;
        }
        else if (hueSegment < 5d)
        {
            redPrime = secondary;
            greenPrime = 0d;
            bluePrime = chroma;
        }
        else
        {
            redPrime = chroma;
            greenPrime = 0d;
            bluePrime = secondary;
        }

        var red = ClampRgbChannel((redPrime + match) * 255d);
        var green = ClampRgbChannel((greenPrime + match) * 255d);
        var blue = ClampRgbChannel((bluePrime + match) * 255d);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static int ClampRgbChannel(double value)
    {
        return Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();
}
