using Microsoft.AspNetCore.Components.WebView;

namespace Wh40kSim.Desktop;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        // Thème du système connu dès le démarrage : AppShell affiche le bon thème au premier rendu
        Shell.Parameters = new Dictionary<string, object?>
        {
            ["SystemDarkMode"] = Application.Current?.RequestedTheme == AppTheme.Dark,
        };
    }

    /// <summary>WebView2 transparente avant son premier rendu (blanche par défaut) : le fond de la page reste visible.</summary>
    private void OnWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        e.WebView.DefaultBackgroundColor = Microsoft.UI.Colors.Transparent;
    }
}
