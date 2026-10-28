using Microsoft.Extensions.Localization;
using Wh40kSim.UI.Resources;
using Wh40kSim.UI.Theme;

namespace Wh40kSim.Desktop;

/// <summary>
/// Application MAUI : une seule fenêtre, centrée, qui contient le BlazorWebView (<see cref="MainPage"/>) ; rouverte
/// agrandie si elle l'était à la dernière fermeture.
/// </summary>
public partial class App : Application
{
    private const double PreferredWidth = 1400;
    private const double PreferredHeight = 900;
    private const double MinimumWidth = 900;
    private const double MinimumHeight = 600;

    private readonly string _title;
    private readonly DesktopPreferenceService _preferences;

    public App(IStringLocalizer<Strings> strings, DesktopPreferenceService preferences)
    {
        InitializeComponent();
        _title = strings["App_Title"];
        _preferences = preferences;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Taille en unités indépendantes de la résolution, réduite si l'écran est plus petit
        var display = DeviceDisplay.Current.MainDisplayInfo;
        double screenWidth = display.Width / display.Density;
        double screenHeight = display.Height / display.Density;
        double width = Math.Min(PreferredWidth, screenWidth);
        double height = Math.Min(PreferredHeight, screenHeight);

        // Thème connu avant le premier affichage : choix mémorisé, sinon celui qui correspond au mode de Windows
        var theme = _preferences.GetTheme() ?? Wh40kTheme.Default(RequestedTheme == AppTheme.Dark);

        var window = new Window(new MainPage(theme))
        {
            Title = _title,
            Width = width,
            Height = height,
            X = (screenWidth - width) / 2,
            Y = (screenHeight - height) / 2,
            MinimumWidth = MinimumWidth,
            MinimumHeight = MinimumHeight,
        };

        // Agrandie ou non : mémorisé à la fermeture, rétabli à l'ouverture (la taille normale reste celle ci-dessus)
        window.Created += (_, _) =>
        {
            if (_preferences.WindowMaximized)
            {
                SetMaximized(window);
            }
        };
        window.Destroying += (_, _) => _preferences.WindowMaximized = IsMaximized(window);
        return window;
    }

#if WINDOWS
    private static Microsoft.UI.Windowing.OverlappedPresenter? Presenter(Window window) =>
        (window.Handler?.PlatformView as Microsoft.UI.Xaml.Window)?.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;

    private static void SetMaximized(Window window) => Presenter(window)?.Maximize();

    private static bool IsMaximized(Window window) =>
        Presenter(window)?.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
#else
    private static void SetMaximized(Window window)
    {
    }

    private static bool IsMaximized(Window window) => false;
#endif
}
