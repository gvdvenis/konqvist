using MudBlazor;

namespace Konqvist.Client.Theme;

public static class KonqvistClientTheme
{
    public static readonly MudTheme Instance = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1565C0",
            Secondary = "#E65100",
            AppbarBackground = "#1565C0",
            Background = "#F5F7FA",
            Surface = "#FFFFFF",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#42A5F5",
            Secondary = "#FF7043",
            AppbarBackground = "#0D1B2A",
            Background = "#0D1B2A",
            Surface = "#1A2940",
            DrawerBackground = "#0D1B2A",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Roboto", "sans-serif"]
            }
        }
    };
}
