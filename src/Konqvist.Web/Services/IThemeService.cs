using Microsoft.FluentUI.AspNetCore.Components;

namespace Konqvist.Web.Services;

public interface IThemeService
{
    event Action<DesignThemeModes>? ThemeChanged;
    DesignThemeModes CurrentTheme { get; }
    void SetTheme(DesignThemeModes theme);
    void SetLightTheme();
    void SetDarkTheme();
}
