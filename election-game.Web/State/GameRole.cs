using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;

namespace ElectionGame.Web.State;


internal class AuthorizeGameRolesAttribute : AuthorizeAttribute
{
    public AuthorizeGameRolesAttribute(params GameRole[] roles)
    {
        Roles = string.Join(",", roles.Select(r => r.ToString()));
    }
}

public enum GameRole
{
    Anonymous,
    GameMaster,
    Player,
    TeamLeader
}