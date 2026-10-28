using System.Reflection;
using Microsoft.AspNetCore.Components.WebView;
using Wh40kSim.UI.Theme;

namespace Wh40kSim.Desktop;

public partial class MainPage : ContentPage
{
    /// <param name="theme">Thème au démarrage : AppShell l'affiche dès le premier rendu, sans flash d'un autre thème.</param>
    public MainPage(ThemeChoice theme)
    {
        InitializeComponent();
        var background = Wh40kTheme.Background(theme);
        BackgroundColor = Color.FromRgb(background.R, background.G, background.B);
        Shell.Parameters = new Dictionary<string, object?>
        {
            ["InitialTheme"] = theme,
            ["Version"] = AppVersion(),
        };
        // Thème dans l'adresse de départ : lu par wwwroot/index.html pour colorer la barre de chargement dès le premier
        // affichage. (Un script injecté « à la création du document » arrivait parfois trop tard, ou avant que la page
        // existe : la barre restait aux couleurs par défaut.) Le routeur ignore la requête : c'est toujours l'accueil.
        WebView.StartPath = $"/?theme={theme.ToString().ToLowerInvariant()}";
    }

    /// <summary>
    /// Version affichée sur l'accueil : InformationalVersion du projet (ex. « 0.3-apercu.1 »), sans le suffixe
    /// « +commit » que le SDK peut y ajouter.
    /// </summary>
    private static string? AppVersion() =>
        typeof(MainPage).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];

    /// <summary>WebView2 transparente avant son premier rendu (blanche par défaut) : le fond de la page reste visible.</summary>
    private void OnWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e) =>
        e.WebView.DefaultBackgroundColor = Microsoft.UI.Colors.Transparent;
}
