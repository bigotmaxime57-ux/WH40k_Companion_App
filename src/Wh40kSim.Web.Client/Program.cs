using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Wh40kSim.UI;
using Wh40kSim.UI.Services;
using Wh40kSim.Web.Client.Services;

// Composant racine (Routes) placé par la page du serveur : rien à déclarer ici, seulement les services du navigateur
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddWh40kSimUI();
builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<ApiSimulationService>();
builder.Services.AddSingleton<ISimulationService>(services => services.GetRequiredService<ApiSimulationService>());
builder.Services.AddSingleton<BrowserScripts>();
builder.Services.AddSingleton<IScenarioFileService, BrowserScenarioFileService>();
builder.Services.AddSingleton<IPreferenceService, BrowserPreferenceService>();
UiCulture.Apply();

var host = builder.Build();
// Plafond du serveur, connu avant le premier affichage (choix rapides, saisie bornée)
await host.Services.GetRequiredService<ApiSimulationService>().LoadLimitsAsync();
await host.RunAsync();
