namespace Konqvist.Web.Authentication;

public record User(string Name, string Password, GameRole GameRole, string TeamName);
