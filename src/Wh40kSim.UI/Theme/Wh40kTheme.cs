using MudBlazor;

namespace Wh40kSim.UI.Theme;

/// <summary>
/// Thème de l'application, à l'identité Warhammer 40k : sombre « grimdark » (acier, laiton, rouge sombre) et pendant
/// clair couleur parchemin. Titres en Bahnschrift (police condensée fournie avec Windows 10 et 11), texte en Segoe UI ;
/// les polices suivantes servent de repli ailleurs (hôte web).
/// </summary>
public static class Wh40kTheme
{
    private static readonly string[] BodyFont = ["Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif"];
    private static readonly string[] TitleFont = ["Bahnschrift", "Segoe UI", "Roboto", "Arial Narrow", "sans-serif"];

    public static MudTheme Theme { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#c9a03c",               // laiton
            PrimaryContrastText = "#14120c",
            Secondary = "#a3242a",             // rouge sombre
            SecondaryContrastText = "#f3e9d2",
            Tertiary = "#5a8f7b",              // vert-de-gris
            Info = "#4f86a8",
            Success = "#5b9a5a",
            Warning = "#d18b2f",
            Error = "#d0463c",
            Background = "#121417",            // acier sombre
            BackgroundGray = "#191c20",
            Surface = "#1c1f24",
            AppbarBackground = "#0b0c0e",
            AppbarText = "#d9b45a",
            DrawerBackground = "#16181c",
            TextPrimary = "#e8e2d0",           // parchemin clair
            TextSecondary = "#a9a291",
            ActionDefault = "#a9a291",
            LinesDefault = "#34383f",
            LinesInputs = "#4a4f57",
            Divider = "#2c3036",
            TableLines = "#2c3036",
            TableStriped = "rgba(201, 160, 60, 0.04)",
            TableHover = "rgba(201, 160, 60, 0.08)",
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#8c6a1c",               // laiton foncé, lisible sur fond clair
            PrimaryContrastText = "#ffffff",
            Secondary = "#8e1f24",
            SecondaryContrastText = "#ffffff",
            Tertiary = "#3f7564",
            Background = "#f2ece0",            // parchemin
            BackgroundGray = "#e9e1d1",
            Surface = "#fbf8f2",
            AppbarBackground = "#1d2024",
            AppbarText = "#d9b45a",
            TextPrimary = "#2a251c",
            TextSecondary = "#6a6152",
            ActionDefault = "#6a6152",
            LinesDefault = "#d8cfbd",
            LinesInputs = "#b9ad96",
            Divider = "#e1d8c6",
            TableLines = "#e1d8c6",
            TableStriped = "rgba(140, 106, 28, 0.05)",
            TableHover = "rgba(140, 106, 28, 0.09)",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = BodyFont },
            H5 = new H5Typography { FontFamily = TitleFont, FontWeight = "600", LetterSpacing = ".06em", TextTransform = "uppercase" },
            H6 = new H6Typography { FontFamily = TitleFont, FontWeight = "600", LetterSpacing = ".08em", TextTransform = "uppercase" },
            Subtitle2 = new Subtitle2Typography { FontFamily = TitleFont, FontWeight = "600", LetterSpacing = ".05em" },
            Button = new ButtonTypography { FontFamily = TitleFont, FontWeight = "600", LetterSpacing = ".06em" },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "6px" },
    };
}
