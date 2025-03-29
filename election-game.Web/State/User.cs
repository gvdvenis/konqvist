namespace ElectionGame.Web.State;

internal record User(string Name, string Password, GameRole GameRole, string TeamName);
