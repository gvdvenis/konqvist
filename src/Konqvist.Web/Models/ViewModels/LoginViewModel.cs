namespace Konqvist.Web.Models.ViewModels;

public record LoginViewModel(string Password)
{
    public static LoginViewModel Empty { get; } = new("");
    public static bool IsEmpty(LoginViewModel model) => model.Password == Empty.Password;
}
