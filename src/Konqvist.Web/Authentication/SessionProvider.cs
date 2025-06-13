using System.Diagnostics.CodeAnalysis;

namespace Konqvist.Web.Authentication;

public class SessionProvider
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private Lazy<Task<UserSession>> _session;

    public UserSession Session => _session.Value.GetAwaiter().GetResult();
    
    public Task<UserSession> SessionAsync => _session.Value;

    public SessionProvider(AuthenticationStateProvider authenticationState)
    {
        _authStateProvider = authenticationState;
        ClearSession();
    }

    private async Task<UserSession> LoadSessionAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        return UserSession.CreateWithAuthState(authState);
    }
    
    [MemberNotNull(nameof(_session))]
    public void ClearSession()
    {
        _session = new Lazy<Task<UserSession>>(LoadSessionAsync);
    }
}
