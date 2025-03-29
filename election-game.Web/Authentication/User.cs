namespace ElectionGame.Web.Authentication;

internal record User(string Name, string Password, GameRole GameRole, string TeamName);
