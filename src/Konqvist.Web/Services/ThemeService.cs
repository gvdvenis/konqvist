using Microsoft.FluentUI.AspNetCore.Components;

namespace Konqvist.Web.Services;

public class ThemeService : IThemeService
{
    private DesignThemeModes _currentTheme = DesignThemeModes.Light;
    
    public event Action<DesignThemeModes>? ThemeChanged;
    
    public DesignThemeModes CurrentTheme 
    { 
        get => _currentTheme;
        private set
        {
            if (_currentTheme == value) return;
            
            _currentTheme = value;
            ThemeChanged?.Invoke(_currentTheme);
        }
    }
    
    public void SetTheme(DesignThemeModes theme)
    {
        CurrentTheme = theme;
    }
    
    public void SetLightTheme()
    {
        CurrentTheme = DesignThemeModes.Light;
    }
    
    public void SetDarkTheme()
    {
        CurrentTheme = DesignThemeModes.Dark;
    }
}