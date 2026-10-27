using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;
using Wh40kSim.UI;
using Wh40kSim.UI.Services;

namespace Wh40kSim.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        UiCulture.Apply();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddWh40kSimUI();
        // Calcul sur la machine, hors du thread de l'interface
        builder.Services.AddSingleton<ISimulationService, LocalSimulationService>();
        builder.Services.AddSingleton(FilePicker.Default);
        builder.Services.AddSingleton(FileSaver.Default);
        builder.Services.AddSingleton<IScenarioFileService, DesktopScenarioFileService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
