namespace ElectionGame.Web.Authentication;

internal class AuthorizeGameRolesAttribute : AuthorizeAttribute
{
    public AuthorizeGameRolesAttribute(params GameRole[] roles)
    {
        Roles = string.Join(",", roles.Select(r => r.ToString()));
    }
}