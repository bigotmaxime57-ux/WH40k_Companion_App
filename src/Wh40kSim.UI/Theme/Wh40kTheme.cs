using MudBlazor;

namespace Wh40kSim.UI.Theme;

/// <summary>Style du thème sombre (le thème clair, parchemin, est commun).</summary>
public enum DarkStyle
{
    /// <summary>Presque noir, or terni, os, rouge sang ; matière et filets ajoutés par wh40ksim.css (classe wh-grimdark).</summary>
    Grimdark,

    /// <summary>Acier sombre et laiton, plus doux.</summary>
    Steel,
}

/// <summary>
/// Thèmes de l'application, à l'identité Warhammer 40k : deux styles sombres (<see cref="DarkStyle"/>) et un clair
/// couleur parchemin. Titres en Bahnschrift (police condensée fournie avec Windows 10 et 11), texte en Segoe UI ;
/// les polices suivantes servent de repli ailleurs (hôte web).
/// </summary>
public static class Wh40kTheme
{
    private static readonly string[] BodyFont = ["Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif"];
    private static readonly string[] TitleFont = ["Bahnschrift", "Segoe UI", "Roboto", "Arial Narrow", "sans-serif"];

    public static MudTheme Grimdark { get; } = Create(GrimdarkPalette(), borderRadius: "2px");

    public static MudTheme Steel { get; } = Create(SteelPalette(), borderRadius: "6px");

    public static MudTheme For(DarkStyle style) => style == DarkStyle.Steel ? Steel : Grimdark;

    private static MudTheme Create(PaletteDark dark, string borderRadius) => new()
    {
        PaletteDark = dark,
        PaletteLight = ParchmentPalette(),
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = BodyFont },
            H5 = new H5Typography { FontFamily = TitleFont, FontWeight = "600", LetterSpacing = ".06em", TextTransform = "uppercase" },
            H6 = new H6Typography { FontFamily = TitleFont, FontWeight = "600", LetterSpacing = ".08em", TextTransform = "uppercase" },
            Subtitle2 = new Subtitle2Typography { FontFamily = TitleFont, FontWeight = "600", LetterSpacing = ".05em" },
            Button = new ButtonTypography { FontFamily = TitleFont, FontWeight = "600", LetterSpacing = ".06em" },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = borderRadius },
    };

    private static PaletteDark GrimdarkPalette() => new()
    {
        Primary = "#b08d3c",               // or terni
        PrimaryContrastText = "#0a0a0b",
        Secondary = "#7d1418",             // rouge sang
        SecondaryContrastText = "#e6dcc4",
        Tertiary = "#5c6b5a",              // vert-de-gris corrodé
        Info = "#5b7a8c",
        Success = "#4f7f4a",
        Warning = "#b8741a",
        Error = "#b3261e",
        Background = "#0a0a0b",            // presque noir
        BackgroundGray = "#141416",
        Surface = "#151517",
        AppbarBackground = "#070708",
        AppbarText = "#c2a462",
        DrawerBackground = "#101012",
        TextPrimary = "#d6ccb4",           // os
        TextSecondary = "#8f8774",
        ActionDefault = "#8f8774",
        LinesDefault = "#2b2b2f",
        LinesInputs = "#3d3d42",
        Divider = "#232326",
        TableLines = "#232326",
        TableStriped = "rgba(176, 141, 60, 0.035)",
        TableHover = "rgba(176, 141, 60, 0.08)",
    };

    private static PaletteDark SteelPalette() => new()
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
    };

    private static PaletteLight ParchmentPalette() => new()
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
        DrawerBackground = "#f7f2e8",
        TextPrimary = "#2a251c",
        TextSecondary = "#6a6152",
        ActionDefault = "#6a6152",
        LinesDefault = "#d8cfbd",
        LinesInputs = "#b9ad96",
        Divider = "#e1d8c6",
        TableLines = "#e1d8c6",
        TableStriped = "rgba(140, 106, 28, 0.05)",
        TableHover = "rgba(140, 106, 28, 0.09)",
    };
}
