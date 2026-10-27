using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Wh40kSim.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Services communs à tous les hôtes : MudBlazor et textes de l'interface (<c>IStringLocalizer</c>).
    /// L'hôte enregistre en plus son <see cref="Services.ISimulationService"/>, son
    /// <see cref="Services.IScenarioFileService"/> et son <see cref="Services.IPreferenceService"/>, et appelle
    /// <see cref="UiCulture.Apply"/> au démarrage.
    /// </summary>
    public static IServiceCollection AddWh40kSimUI(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddLocalization();
        return services;
    }
}
