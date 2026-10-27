using MudBlazor;
using MudBlazor.Utilities;

namespace Wh40kSim.UI.Theme;

/// <summary>Thème choisi par l'utilisateur (menu de la barre de titre, mémorisé par l'hôte) : un clair, trois sombres.</summary>
public enum ThemeChoice
{
    /// <summary>Clair, couleur parchemin.</summary>
    Parchment,

    /// <summary>Presque noir, or terni, os, rouge sang ; matière et filets ajoutés par wh40ksim.css (classe wh-grimdark).</summary>
    Grimdark,

    /// <summary>Acier sombre et laiton, plus doux.</summary>
    Steel,

    /// <summary>Néon : nuit indigo, violet pour l'attaquant, bleu pour le défenseur, halos et dégradés (classe wh-neon).</summary>
    Neon,
}

/// <summary>
/// Thèmes de l'application, à l'identité Warhammer 40k (<see cref="ThemeChoice"/>) : trois styles sombres et un clair
/// couleur parchemin, commun à tous (palette claire de chaque <see cref="MudTheme"/>). Titres en Bahnschrift (police
/// condensée fournie avec Windows 10 et 11), texte en Segoe UI ; les polices suivantes servent de repli ailleurs (hôte web).
/// </summary>
public static class Wh40kTheme
{
    private static readonly string[] BodyFont = ["Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif"];
    private static readonly string[] TitleFont = ["Bahnschrift", "Segoe UI", "Roboto", "Arial Narrow", "sans-serif"];

    public static MudTheme Grimdark { get; } = Create(GrimdarkPalette(), borderRadius: "2px");

    public static MudTheme Steel { get; } = Create(SteelPalette(), borderRadius: "6px");

    public static MudTheme Neon { get; } = Create(NeonPalette(), borderRadius: "10px");

    /// <summary>Thème MudBlazor d'un choix (Parchemin : la palette claire, commune, de Grimdark).</summary>
    public static MudTheme For(ThemeChoice choice) => choice switch
    {
        ThemeChoice.Steel => Steel,
        ThemeChoice.Neon => Neon,
        _ => Grimdark,
    };

    /// <summary>Clé du nom affiché dans le menu des thèmes (Resources/Strings.resx).</summary>
    public static string NameKey(ThemeChoice choice) => $"Theme_{choice}";

    public static bool IsDark(ThemeChoice choice) => choice != ThemeChoice.Parchment;

    /// <summary>Couleur de fond d'un thème, pour l'hôte (fond de la fenêtre avant le premier rendu).</summary>
    public static MudColor Background(ThemeChoice choice) =>
        IsDark(choice) ? For(choice).PaletteDark.Background : For(choice).PaletteLight.Background;

    /// <summary>Thème sans choix mémorisé (premier lancement) : celui qui correspond au mode clair / sombre du système.</summary>
    public static ThemeChoice Default(bool systemDarkMode) => systemDarkMode ? ThemeChoice.Grimdark : ThemeChoice.Parchment;

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

    private static PaletteDark NeonPalette() => new()
    {
        Primary = "#8f7bff",               // lavande, entre le violet et le bleu des camps
        PrimaryContrastText = "#ffffff",
        Secondary = "#ff4fd8",             // rose néon
        SecondaryContrastText = "#ffffff",
        Tertiary = "#29e0ff",              // cyan
        Info = "#29b6ff",
        Success = "#2ee6a6",               // vert néon
        Warning = "#ffb02e",
        Error = "#ff4d6d",
        Background = "#06051a",            // nuit indigo
        BackgroundGray = "#0e0b26",
        Surface = "#120d2c",
        AppbarBackground = "#0a0820",
        AppbarText = "#e6dcff",
        DrawerBackground = "#0e0b26",
        TextPrimary = "#ece8ff",
        TextSecondary = "#9d97c7",
        ActionDefault = "#9d97c7",
        LinesDefault = "#2a2452",
        LinesInputs = "#3a3370",
        Divider = "#221d45",
        TableLines = "#221d45",
        TableStriped = "rgba(143, 123, 255, 0.05)",
        TableHover = "rgba(143, 123, 255, 0.10)",
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
