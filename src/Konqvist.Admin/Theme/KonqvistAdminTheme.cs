using MudBlazor;

namespace Konqvist.Admin.Theme;

public static class KonqvistAdminTheme
{
    public static readonly MudTheme Instance = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1976D2",
            Secondary = "#546E7A",
            AppbarBackground = "#1976D2",
            Background = "#FAFAFA",
            BackgroundGray = "#F0F2F5",
            DrawerBackground = "#FFFFFF",
            DrawerText = "rgba(0,0,0,0.87)",
            Surface = "#FFFFFF",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Roboto", "sans-serif"]
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "260px"
        }
    };
}
