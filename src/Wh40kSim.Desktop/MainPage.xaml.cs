using Microsoft.AspNetCore.Components.WebView;
using Wh40kSim.UI.Theme;

namespace Wh40kSim.Desktop;

public partial class MainPage : ContentPage
{
    private readonly ThemeChoice _theme;

    /// <param name="theme">Thème au démarrage : AppShell l'affiche dès le premier rendu, sans flash d'un autre thème.</param>
    public MainPage(ThemeChoice theme)
    {
        InitializeComponent();
        _theme = theme;
        var background = Wh40kTheme.Background(theme);
        BackgroundColor = Color.FromRgb(background.R, background.G, background.B);
        Shell.Parameters = new Dictionary<string, object?>
        {
            ["InitialTheme"] = theme,
        };
    }

    /// <summary>
    /// WebView2 transparente avant son premier rendu (blanche par défaut) : le fond de la page reste visible. Le thème est
    /// posé sur la page de chargement (attribut data-theme, lu par wwwroot/app.css) pour colorer la barre de chargement.
    /// </summary>
    private void OnWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        e.WebView.DefaultBackgroundColor = Microsoft.UI.Colors.Transparent;
        _ = e.WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
            $"document.documentElement.dataset.theme = '{_theme.ToString().ToLowerInvariant()}';");
    }
}
