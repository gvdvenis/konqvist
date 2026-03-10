using System.Text.RegularExpressions;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Infrastructure.Tokens;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Admin.Features.Teams;

public sealed partial class TeamTemplateAdminService(IDbContextFactory<KonqvistDbContext> dbContextFactory)
{
    private const int MinGeneratedTeamCount = 1;
    private const int MaxGeneratedTeamCount = 10;
    private const int MaxTokenGenerationAttempts = 10;
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

        await EnsureRoleTokensAsync(dbContext, templateId, cancellationToken);

        var teams = await dbContext.TeamTemplates
            .AsNoTracking()
            .Where(entity => entity.GameTemplateId == templateId)
            .OrderBy(entity => entity.Name)
            .Select(entity => new TeamTemplateListItem(
                entity.Id,
                entity.Name,
                entity.Color,
                entity.Players
                    .Where(player => player.Role == PlayerRole.Runner)
                    .Select(player => player.LoginToken)
                    .FirstOrDefault() ?? string.Empty,
                entity.Players
                    .Where(player => player.Role == PlayerRole.TeamLeader)
                    .Select(player => player.LoginToken)
                    .FirstOrDefault() ?? string.Empty))
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

        var teamTemplate = new TeamTemplate
        {
            GameTemplateId = templateId,
            Name = normalizedName,
            Color = normalizedColor
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.TeamTemplates.Add(teamTemplate);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateNameViolation(exception))
        {
            return SaveTeamTemplateResult.DuplicateName;
        }

        var roleTokensSaved = await CreateRolePlayersForTeamAsync(dbContext, teamTemplate, cancellationToken);
        if (!roleTokensSaved)
        {
            await transaction.RollbackAsync(cancellationToken);
            return SaveTeamTemplateResult.TokenGenerationFailed;
        }

        await transaction.CommitAsync(cancellationToken);
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingTeams = await dbContext.TeamTemplates
            .Where(entity => entity.GameTemplateId == templateId)
            .ToListAsync(cancellationToken);
        dbContext.TeamTemplates.RemoveRange(existingTeams);
        await dbContext.SaveChangesAsync(cancellationToken);

        var generatedTeams = GenerateTeams(templateId, teamCount);
        dbContext.TeamTemplates.AddRange(generatedTeams);

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

        var roleTokensSaved = await CreateRolePlayersForTeamsAsync(dbContext, generatedTeams, cancellationToken);
        if (!roleTokensSaved)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BulkReplaceTeamTemplateResult.TokenGenerationFailed;
        }

        await transaction.CommitAsync(cancellationToken);
        return BulkReplaceTeamTemplateResult.Replaced;
    }

    public async Task<RegenerateTeamTokenResult> RegenerateTokenAsync(
        int templateId,
        int teamTemplateId,
        PlayerRole role,
        CancellationToken cancellationToken = default)
    {
        if (role is not (PlayerRole.Runner or PlayerRole.TeamLeader))
        {
            return RegenerateTeamTokenResult.PlayerNotFound;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await TemplateExistsAsync(dbContext, templateId, cancellationToken))
        {
            return RegenerateTeamTokenResult.TemplateNotFound;
        }

        var teamTemplate = await dbContext.TeamTemplates
            .Include(entity => entity.Players)
            .FirstOrDefaultAsync(
                entity => entity.Id == teamTemplateId && entity.GameTemplateId == templateId,
                cancellationToken);
        if (teamTemplate is null)
        {
            return RegenerateTeamTokenResult.TeamNotFound;
        }

        var playerTemplate = teamTemplate.Players.FirstOrDefault(entity => entity.Role == role);
        if (playerTemplate is null)
        {
            return RegenerateTeamTokenResult.PlayerNotFound;
        }

        Func<string> tokenFactory = role == PlayerRole.Runner
            ? () => LoginTokenGenerator.GenerateRunnerToken(teamTemplate.Name)
            : () => LoginTokenGenerator.GenerateTeamCaptainToken(teamTemplate.Name);

        for (var attempt = 0; attempt < MaxTokenGenerationAttempts; attempt++)
        {
            playerTemplate.LoginToken = await GenerateUniquePlayerTokenAsync(dbContext, tokenFactory, cancellationToken);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return RegenerateTeamTokenResult.Regenerated;
            }
            catch (DbUpdateException exception) when (IsDuplicateTokenViolation(exception))
            {
                continue;
            }
        }

        return RegenerateTeamTokenResult.TokenGenerationFailed;
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

    private static bool IsDuplicateTokenViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
               && sqliteException.SqliteErrorCode == 19
               && sqliteException.SqliteExtendedErrorCode == 2067
               && sqliteException.Message.Contains(
                    "UNIQUE constraint failed: PlayerTemplates.LoginToken",
                    StringComparison.Ordinal);
    }

    private static bool IsTemplateForeignKeyViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
               && sqliteException.SqliteErrorCode == 19
               && sqliteException.SqliteExtendedErrorCode == 787
               && sqliteException.Message.Contains("TeamTemplates.GameTemplateId", StringComparison.Ordinal);
    }

    private static async Task EnsureRoleTokensAsync(
        KonqvistDbContext dbContext,
        int templateId,
        CancellationToken cancellationToken)
    {
        var teams = await dbContext.TeamTemplates
            .Include(entity => entity.Players)
            .Where(entity => entity.GameTemplateId == templateId)
            .ToListAsync(cancellationToken);

        foreach (var team in teams)
        {
            var hasRunner = team.Players.Any(player => player.Role == PlayerRole.Runner);
            var hasTeamLeader = team.Players.Any(player => player.Role == PlayerRole.TeamLeader);
            if (hasRunner && hasTeamLeader)
            {
                continue;
            }

            var created = await CreateMissingRolePlayersForTeamAsync(
                dbContext,
                team,
                createRunner: !hasRunner,
                createTeamLeader: !hasTeamLeader,
                cancellationToken);
            if (!created)
            {
                throw new InvalidOperationException($"Could not create missing role tokens for team '{team.Name}'.");
            }
        }
    }

    private static Task<bool> CreateRolePlayersForTeamAsync(
        KonqvistDbContext dbContext,
        TeamTemplate teamTemplate,
        CancellationToken cancellationToken)
    {
        return CreateMissingRolePlayersForTeamAsync(
            dbContext,
            teamTemplate,
            createRunner: true,
            createTeamLeader: true,
            cancellationToken);
    }

    private static async Task<bool> CreateRolePlayersForTeamsAsync(
        KonqvistDbContext dbContext,
        IReadOnlyList<TeamTemplate> teams,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxTokenGenerationAttempts; attempt++)
        {
            var reservedTokens = new HashSet<string>(StringComparer.Ordinal);
            var playerTemplates = new List<PlayerTemplate>(teams.Count * 2);

            foreach (var team in teams)
            {
                var runnerToken = await GenerateUniquePlayerTokenAsync(
                    dbContext,
                    () => LoginTokenGenerator.GenerateRunnerToken(team.Name),
                    cancellationToken,
                    reservedTokens);
                var teamCaptainToken = await GenerateUniquePlayerTokenAsync(
                    dbContext,
                    () => LoginTokenGenerator.GenerateTeamCaptainToken(team.Name),
                    cancellationToken,
                    reservedTokens);

                playerTemplates.Add(new PlayerTemplate
                {
                    TeamTemplateId = team.Id,
                    Role = PlayerRole.Runner,
                    LoginToken = runnerToken
                });
                playerTemplates.Add(new PlayerTemplate
                {
                    TeamTemplateId = team.Id,
                    Role = PlayerRole.TeamLeader,
                    LoginToken = teamCaptainToken
                });
            }

            dbContext.PlayerTemplates.AddRange(playerTemplates);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (IsDuplicateTokenViolation(exception))
            {
                foreach (var playerTemplate in playerTemplates)
                {
                    dbContext.Entry(playerTemplate).State = EntityState.Detached;
                }
            }
        }

        return false;
    }

    private static async Task<bool> CreateMissingRolePlayersForTeamAsync(
        KonqvistDbContext dbContext,
        TeamTemplate teamTemplate,
        bool createRunner,
        bool createTeamLeader,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxTokenGenerationAttempts; attempt++)
        {
            var reservedTokens = new HashSet<string>(
                teamTemplate.Players.Select(player => player.LoginToken),
                StringComparer.Ordinal);
            var playerTemplates = new List<PlayerTemplate>(2);

            if (createRunner)
            {
                var runnerToken = await GenerateUniquePlayerTokenAsync(
                    dbContext,
                    () => LoginTokenGenerator.GenerateRunnerToken(teamTemplate.Name),
                    cancellationToken,
                    reservedTokens);
                playerTemplates.Add(new PlayerTemplate
                {
                    TeamTemplateId = teamTemplate.Id,
                    Role = PlayerRole.Runner,
                    LoginToken = runnerToken
                });
            }

            if (createTeamLeader)
            {
                var captainToken = await GenerateUniquePlayerTokenAsync(
                    dbContext,
                    () => LoginTokenGenerator.GenerateTeamCaptainToken(teamTemplate.Name),
                    cancellationToken,
                    reservedTokens);
                playerTemplates.Add(new PlayerTemplate
                {
                    TeamTemplateId = teamTemplate.Id,
                    Role = PlayerRole.TeamLeader,
                    LoginToken = captainToken
                });
            }

            dbContext.PlayerTemplates.AddRange(playerTemplates);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (IsDuplicateTokenViolation(exception))
            {
                foreach (var playerTemplate in playerTemplates)
                {
                    dbContext.Entry(playerTemplate).State = EntityState.Detached;
                }
            }
        }

        return false;
    }

    private static async Task<string> GenerateUniquePlayerTokenAsync(
        KonqvistDbContext dbContext,
        Func<string> tokenFactory,
        CancellationToken cancellationToken,
        ISet<string>? reservedTokens = null)
    {
        for (var attempt = 0; attempt < MaxTokenGenerationAttempts * 2; attempt++)
        {
            var candidate = tokenFactory();
            if (reservedTokens is not null && reservedTokens.Contains(candidate))
            {
                continue;
            }

            var exists = await dbContext.PlayerTemplates
                .AsNoTracking()
                .AnyAsync(entity => entity.LoginToken == candidate, cancellationToken);
            if (exists)
            {
                continue;
            }

            reservedTokens?.Add(candidate);
            return candidate;
        }

        throw new InvalidOperationException("Could not generate a unique login token.");
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
