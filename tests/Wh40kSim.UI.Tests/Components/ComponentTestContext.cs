using System.Text.Json;
using Bunit;
using Bunit.Extensions.WaitForHelpers;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.UI.Services;
using Wh40kSim.UI.Theme;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>
/// Contexte bUnit commun : services de l'interface (MudBlazor, textes), culture de l'appli, faux services de simulation,
/// de fichiers et de préférences. Le fournisseur de popovers est rendu d'abord, comme dans la mise en page d'un hôte :
/// les listes déroulantes MudBlazor en ont besoin (sauf pour AppShell, qui contient déjà le sien : un seul est permis).
/// <para>Certains services de MudBlazor ne se libèrent qu'en asynchrone : <see cref="IAsyncLifetime"/> fait libérer le
/// contexte par xUnit avec <see cref="BunitContext.DisposeAsync"/> (xUnit 2 n'appelle sinon que Dispose).</para>
/// </summary>
public abstract class ComponentTestContext : BunitContext, IAsyncLifetime
{
    protected ComponentTestContext(bool renderPopoverProvider = true)
    {
        UiCulture.Apply();
        Services.AddWh40kSimUI();
        Services.AddSingleton<ISimulationService>(Simulations);
        Services.AddSingleton<IScenarioFileService>(Files);
        Services.AddSingleton<IPreferenceService>(Preferences);
        // Les appels JavaScript de MudBlazor (mesures, focus…) reçoivent une réponse par défaut
        JSInterop.Mode = JSRuntimeMode.Loose;
        if (renderPopoverProvider)
        {
            Render<MudPopoverProvider>();
            Dialogs = Render<MudDialogProvider>();
        }
    }

    /// <summary>Fournisseur des boîtes de dialogue (confirmations) : c'est là qu'elles s'affichent.</summary>
    protected IRenderedComponent<MudDialogProvider>? Dialogs { get; }

    protected FakeSimulationService Simulations { get; } = new();

    protected FakeScenarioFileService Files { get; } = new();

    protected FakePreferenceService Preferences { get; } = new();

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}

public static class StableClickExtensions
{
    /// <summary>
    /// Clique jusqu'à ce que l'effet attendu soit là. MudBlazor finit le rendu de certains composants (infobulles…) de
    /// façon asynchrone : rarement, un clic part vers un bouton qui vient d'être remplacé et se perd. On recherche alors
    /// le bouton et on reclique, seulement si l'effet n'est pas encore là (sans risque, même pour une bascule).
    /// </summary>
    public static void ClickUntil<TComponent>(this IRenderedComponent<TComponent> cut, string selector, Func<bool> done, int attempts = 3)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        for (int attempt = 1; ; attempt++)
        {
            cut.Find(selector).Click();
            try
            {
                cut.WaitForState(done, TimeSpan.FromMilliseconds(500));
                return;
            }
            catch (WaitForFailedException) when (attempt < attempts)
            {
                // Clic perdu : on recommence avec le bouton tel qu'il est maintenant rendu
            }
        }
    }
}

/// <summary>Simulation rapide et observable : nombre d'itérations réduit, porte optionnelle pour la garder « en cours ».</summary>
public sealed class FakeSimulationService : ISimulationService
{
    private const int TestIterations = 2_000;

    public int Calls { get; private set; }

    public int? LastIterations { get; private set; }

    /// <summary>Si définie, la simulation attend qu'elle soit ouverte (ou annulée).</summary>
    public TaskCompletionSource? Gate { get; set; }

    public async Task<SimulationResult> RunAsync(
        Scenario scenario,
        int iterations,
        int? seed = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        LastIterations = iterations;
        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }
        return Simulator.Run(scenario, TestIterations, seed ?? 1, cancellationToken: cancellationToken);
    }
}

public sealed class FakeScenarioFileService : IScenarioFileService
{
    /// <summary>Résultat de la prochaine ouverture (null = l'utilisateur annule).</summary>
    public OpenedScenario? ToOpen { get; set; }

    /// <summary>Erreur levée par la prochaine ouverture, par exemple un JSON invalide.</summary>
    public Exception? OpenError { get; set; }

    public List<(Scenario Scenario, string SuggestedFileName)> Saved { get; } = [];

    public Task<OpenedScenario?> OpenAsync(CancellationToken cancellationToken = default) =>
        OpenError is not null ? Task.FromException<OpenedScenario?>(OpenError) : Task.FromResult(ToOpen);

    public Task<string?> SaveAsync(Scenario scenario, string suggestedFileName, CancellationToken cancellationToken = default)
    {
        Saved.Add((scenario, suggestedFileName));
        return Task.FromResult<string?>(suggestedFileName);
    }

    public static JsonException InvalidJson() => new("Propriété inconnue 'toughness'.");
}

/// <summary>Préférences en mémoire : thème mémorisé lisible et modifiable par les tests.</summary>
public sealed class FakePreferenceService : IPreferenceService
{
    public ThemeChoice? Theme { get; set; }

    public Task<ThemeChoice?> GetThemeAsync() => Task.FromResult(Theme);

    public Task SetThemeAsync(ThemeChoice theme)
    {
        Theme = theme;
        return Task.CompletedTask;
    }
}
