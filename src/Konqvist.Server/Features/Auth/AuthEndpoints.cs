using System.Security.Claims;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/login", LoginAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", MeAsync);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        KonqvistDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Results.BadRequest(new AuthErrorResponse("Login token is required."));
        }

        var playerTemplate = await dbContext.PlayerTemplates
            .Include(entity => entity.TeamTemplate)
            .FirstOrDefaultAsync(entity => entity.LoginToken == request.Token, cancellationToken);
        if (playerTemplate is null)
        {
            return Results.Unauthorized();
        }

        var activeSession = await dbContext.GameSessions
            .OrderByDescending(entity => entity.Status == GameStatus.Running)
            .ThenByDescending(entity => entity.Id)
            .FirstOrDefaultAsync(
                entity => entity.Status == GameStatus.Pending || entity.Status == GameStatus.Running,
                cancellationToken);
        if (activeSession is null)
        {
            return Results.Conflict(new AuthErrorResponse("No active game session is available for login."));
        }

        if (playerTemplate.Role == PlayerRole.Runner)
        {
            var anotherRunnerLoggedIn = await dbContext.PlayerSessions
                .Include(entity => entity.PlayerTemplate)
                .AnyAsync(
                    entity => entity.GameSessionId == activeSession.Id
                              && entity.IsLoggedIn
                              && entity.PlayerTemplate.TeamTemplateId == playerTemplate.TeamTemplateId
                              && entity.PlayerTemplate.Role == PlayerRole.Runner
                              && entity.PlayerTemplateId != playerTemplate.Id,
                    cancellationToken);

            if (anotherRunnerLoggedIn)
            {
                return Results.Conflict(new AuthErrorResponse(
                    $"Runner slot for team '{playerTemplate.TeamTemplate.Name}' is already in use."));
            }
        }

        var playerSession = await dbContext.PlayerSessions
            .FirstOrDefaultAsync(
                entity => entity.GameSessionId == activeSession.Id && entity.PlayerTemplateId == playerTemplate.Id,
                cancellationToken);
        if (playerSession is null)
        {
            playerSession = new PlayerSession
            {
                GameSessionId = activeSession.Id,
                PlayerTemplateId = playerTemplate.Id,
                IsLoggedIn = true
            };
            dbContext.PlayerSessions.Add(playerSession);
        }
        else
        {
            playerSession.IsLoggedIn = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var claims = new[]
        {
            new Claim(AuthConstants.ClaimTypes.PlayerSessionId, playerSession.Id.ToString()),
            new Claim(ClaimTypes.Role, playerTemplate.Role.ToString())
        };
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, AuthConstants.AuthenticationScheme));
        await httpContext.SignInAsync(AuthConstants.AuthenticationScheme, principal);

        return Results.Ok(new AuthIdentityResponse(
            playerTemplate.Role.ToString(),
            playerTemplate.TeamTemplate.Name,
            playerSession.Id));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        KonqvistDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var playerSessionId = GetPlayerSessionId(httpContext.User);
        if (playerSessionId is not null)
        {
            var playerSession = await dbContext.PlayerSessions
                .FirstOrDefaultAsync(entity => entity.Id == playerSessionId.Value, cancellationToken);
            if (playerSession is not null)
            {
                playerSession.IsLoggedIn = false;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await httpContext.SignOutAsync(AuthConstants.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(
        HttpContext httpContext,
        KonqvistDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var playerSessionId = GetPlayerSessionId(httpContext.User);
        if (playerSessionId is null)
        {
            await httpContext.SignOutAsync(AuthConstants.AuthenticationScheme);
            return Results.Unauthorized();
        }

        var playerSession = await dbContext.PlayerSessions
            .Include(entity => entity.PlayerTemplate)
            .ThenInclude(entity => entity.TeamTemplate)
            .FirstOrDefaultAsync(entity => entity.Id == playerSessionId.Value, cancellationToken);
        if (playerSession is null || !playerSession.IsLoggedIn)
        {
            await httpContext.SignOutAsync(AuthConstants.AuthenticationScheme);
            return Results.Unauthorized();
        }

        return Results.Ok(new AuthIdentityResponse(
            playerSession.PlayerTemplate.Role.ToString(),
            playerSession.PlayerTemplate.TeamTemplate.Name,
            playerSession.Id));
    }

    private static int? GetPlayerSessionId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(AuthConstants.ClaimTypes.PlayerSessionId)?.Value;
        return int.TryParse(claim, out var value) ? value : null;
    }
}
