using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using System.Security.Claims;

namespace Konqvist.Web.Authentication;

public static class AuthenticationHelpers
{
    /// <summary>
    ///     This method verifies if the `SessionVersion` claim for normal players has not
    ///     been changed. If it is, it invalidates the auth session.
    ///     The <see cref="SessionKeyProvider.InvalidateGameInstanceKey"/>
    ///     service method can be used to refresh the key, so all active sessions will be invalidated
    ///     and forcefully logged out.
    /// </summary>
    /// <param name="ctx"></param>
    /// <returns></returns>
    public static Task ValidateSessionKey(this CookieValidatePrincipalContext ctx)
    {
        // Game Master sessions are never invalidated
        string? roleClaim = ctx.Principal?.FindFirstValue(ClaimTypes.Role);
        if (roleClaim == nameof(GameRole.GameMaster))
        {
            return Task.CompletedTask;
        }

        // Additional validation based on the session key.
        string? sessionClaim = ctx.Principal?.FindFirstValue("SessionVersion");

        // We can invalidate authenticated sessions by rotating the session key
        var sessionVersionProvider = ctx.HttpContext.RequestServices
            .GetRequiredService<SessionKeyProvider>();

        if (sessionClaim == sessionVersionProvider.GameInstanceKey)
        {
            return Task.CompletedTask;
        }

        ctx.RejectPrincipal();
        ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     This helper method strips the query parameters from the request uri.
    ///     This is useful to get rid of the ?returnUrl=... part on the default login redirect
    /// </summary>
    /// <param name="redirectContext"></param>
    /// <returns></returns>
    public static Task StripRedirectUrlParams(this RedirectContext<CookieAuthenticationOptions> redirectContext)
    {
        // strip the default returnUrl query parameter from the uri
        string uri = redirectContext.RedirectUri;
        UriHelper.FromAbsolute(uri, out string scheme, out var host, out var path, out var query, out var fragment);
        uri = UriHelper.BuildAbsolute(scheme, host, path);
        redirectContext.Response.Redirect(uri);
        return Task.CompletedTask;
    }
}
