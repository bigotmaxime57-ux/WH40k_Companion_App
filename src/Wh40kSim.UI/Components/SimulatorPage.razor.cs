using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.UI.Forms;
using Wh40kSim.UI.Services;

namespace Wh40kSim.UI.Components;

/// <summary>
/// État de la page : le formulaire édité, sa validation (par le Core), la simulation en cours et son résultat.
/// Le scénario est reconstruit et revalidé après chaque modification signalée par un panneau.
/// </summary>
public partial class SimulatorPage : IDisposable
{
    private const string DefaultFileName = "scenario.json";

    private Scenario? _scenario;          // scénario saisi, null tant qu'une expression de dés est invalide
    private Scenario _reference = new();  // dernier scénario lisible : décide des options de situation à afficher
    private IReadOnlyList<string> _errors = [];
    private bool _hasDiceErrors;
    private string? _fileName;

    private int _iterations = 1_000_000;
    private int? _seed;
    private bool _running;
    private double _progress;
    private CancellationTokenSource? _cancellation;
    private SimulationResult? _result;

    private string? _message;
    private Severity _messageSeverity;

    [Inject]
    private ISimulationService SimulationService { get; set; } = null!;

    [Inject]
    private IScenarioFileService FileService { get; set; } = null!;

    /// <summary>Formulaire en cours d'édition.</summary>
    public ScenarioForm Form { get; private set; } = NewForm();

    private bool CanRun => !_running && _scenario is not null && _errors.Count == 0;

    /// <summary>On peut enregistrer un scénario incohérent (à corriger plus tard), pas une saisie de dés illisible.</summary>
    private bool CanSave => !_running && _scenario is not null;

    protected override void OnInitialized() => Refresh();

    public void Dispose() => _cancellation?.Cancel();

    /// <summary>Nouveau scénario : valeurs par défaut du Core, avec une arme et un groupe pour commencer.</summary>
    private static ScenarioForm NewForm()
    {
        var form = new ScenarioForm();
        form.Attacker.Weapons.Add(new WeaponForm());
        form.Defender.Groups.Add(new ModelGroupForm());
        return form;
    }

    private void Refresh()
    {
        try
        {
            _scenario = Form.ToScenario();
            _reference = _scenario;
            _errors = _scenario.Validate();
            _hasDiceErrors = false;
        }
        catch (FormatException)
        {
            _scenario = null;
            _errors = [];
            _hasDiceErrors = true;
        }
    }

    private void Load(ScenarioForm form, string? fileName)
    {
        Form = form;
        _fileName = fileName;
        _result = null;
        _message = null;
        Refresh();
    }

    private void NewScenario() => Load(NewForm(), null);

    private async Task OpenAsync()
    {
        try
        {
            var opened = await FileService.OpenAsync();
            if (opened is not null)
            {
                Load(new ScenarioForm(opened.Scenario), opened.FileName);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            ShowMessage(L["File_OpenError", ex.Message], Severity.Error);
        }
    }

    private async Task SaveAsync()
    {
        if (_scenario is null)
        {
            return;
        }

        try
        {
            string? written = await FileService.SaveAsync(_scenario, _fileName ?? DefaultFileName);
            if (written is not null)
            {
                _fileName = written;
                ShowMessage(L["File_Saved", written], Severity.Success);
            }
        }
        catch (IOException ex)
        {
            ShowMessage(L["File_SaveError", ex.Message], Severity.Error);
        }
    }

    private async Task RunAsync()
    {
        if (!CanRun)
        {
            return;
        }

        _running = true;
        _progress = 0;
        _message = null;
        _cancellation = new CancellationTokenSource();
        // Les rapports arrivent des threads de calcul : on redessine via le contexte de l'interface
        var progress = new Progress<double>(value =>
        {
            _progress = value;
            _ = InvokeAsync(StateHasChanged);
        });

        try
        {
            _result = await SimulationService.RunAsync(_scenario!, _iterations, _seed, progress, _cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            ShowMessage(L["Simulation_Cancelled"], Severity.Info);
        }
        finally
        {
            _running = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    private void Cancel() => _cancellation?.Cancel();

    private void ShowMessage(string message, Severity severity)
    {
        _message = message;
        _messageSeverity = severity;
    }
}
