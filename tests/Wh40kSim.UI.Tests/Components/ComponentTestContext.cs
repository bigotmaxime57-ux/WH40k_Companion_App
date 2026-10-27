using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.UI.Services;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>
/// Contexte bUnit commun : services de l'interface (MudBlazor, textes), culture de l'appli, faux services de simulation
/// et de fichiers. Le fournisseur de popovers est rendu d'abord, comme dans la mise en page d'un hôte : les listes
/// déroulantes MudBlazor en ont besoin (sauf pour AppShell, qui contient déjà le sien : un seul est permis).
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

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
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
