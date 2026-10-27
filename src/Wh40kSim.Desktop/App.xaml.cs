using Microsoft.Extensions.Localization;
using Wh40kSim.UI.Resources;

namespace Wh40kSim.Desktop;

/// <summary>Application MAUI : une seule fenêtre, centrée, qui contient le BlazorWebView (<see cref="MainPage"/>).</summary>
public partial class App : Application
{
    private const double PreferredWidth = 1400;
    private const double PreferredHeight = 900;
    private const double MinimumWidth = 900;
    private const double MinimumHeight = 600;

    private readonly string _title;

    public App(IStringLocalizer<Strings> strings)
    {
        InitializeComponent();
        _title = strings["App_Title"];
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Taille en unités indépendantes de la résolution, réduite si l'écran est plus petit
        var display = DeviceDisplay.Current.MainDisplayInfo;
        double screenWidth = display.Width / display.Density;
        double screenHeight = display.Height / display.Density;
        double width = Math.Min(PreferredWidth, screenWidth);
        double height = Math.Min(PreferredHeight, screenHeight);

        return new Window(new MainPage())
        {
            Title = _title,
            Width = width,
            Height = height,
            X = (screenWidth - width) / 2,
            Y = (screenHeight - height) / 2,
            MinimumWidth = MinimumWidth,
            MinimumHeight = MinimumHeight,
        };
    }
}
