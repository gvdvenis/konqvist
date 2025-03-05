namespace ElectionGame.Web.State;

internal record User(string Name, string Password, Role Role, string TeamName) 
{
    public User WithName(string name) => this with { Name = name };
    public User WithPassword(string password) => this with { Password = password };
    public User WithRole(Role role) => this with { Role = role };
    public User WithTeamName(string teamName) => this with { TeamName = teamName };
}
