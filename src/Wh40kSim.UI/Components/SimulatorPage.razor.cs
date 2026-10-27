using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.UI.Forms;
using Wh40kSim.UI.Services;

namespace Wh40kSim.UI.Components;

/// <summary>
/// Écran « Quick sim ». Le scénario courant (formulaire, fichier, modifications non enregistrées, dernier résultat,
/// réglages) est tenu par <see cref="ScenarioSession"/> et survit au changement d'écran ; la page garde ce qui s'en
/// déduit (validation par le Core) et la simulation en cours. Le scénario est reconstruit et revalidé après chaque
/// modification signalée par un panneau.
/// </summary>
public partial class SimulatorPage : IAsyncDisposable
{
    private const string DefaultFileName = "scenario.json";
    private const string ShortcutsModule = "./_content/Wh40kSim.UI/wh40ksim.js";

    private Scenario? _scenario;          // scénario saisi, null tant qu'une expression de dés est invalide
    private Scenario _reference = new();  // dernier scénario lisible : décide des options de situation à afficher
    private IReadOnlyList<string> _errors = [];
    private bool _hasDiceErrors;
    private WeaponForm? _keywordWeapon;   // arme dont les mots-clés sont ouverts dans le panneau latéral

    private double _progress;
    private CancellationTokenSource? _cancellation;

    private string? _message;
    private Severity _messageSeverity;

    private IJSObjectReference? _shortcutsModule;
    private IJSObjectReference? _shortcuts;
    private DotNetObjectReference<SimulatorPage>? _self;

    [Inject]
    private ISimulationService SimulationService { get; set; } = null!;

    [Inject]
    private IScenarioFileService FileService { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private ScenarioSession Session { get; set; } = null!;

    /// <summary>Formulaire en cours d'édition.</summary>
    public ScenarioForm Form => Session.Form;

    /// <summary>Le formulaire a été modifié depuis le dernier « Nouveau », « Ouvrir » ou « Enregistrer ».</summary>
    public bool IsDirty => Session.IsDirty;

    private bool Running => Session.IsRunning;

    private bool CanRun => !Running && _scenario is not null && _errors.Count == 0;

    /// <summary>On peut enregistrer un scénario incohérent (à corriger plus tard), pas une saisie de dés illisible.</summary>
    private bool CanSave => !Running && _scenario is not null;

    /// <summary>Nom du fichier (ou « Nouveau scénario »), suivi de « * » s'il y a des modifications non enregistrées.</summary>
    private string FileLabel => (Session.FileName ?? L["Toolbar_Untitled"]) + (Session.IsDirty ? " *" : "");

    protected override void OnInitialized() => Refresh();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // Raccourcis clavier à l'échelle du document (module JavaScript de Wh40kSim.UI, chargé à la demande)
        try
        {
            _self = DotNetObjectReference.Create(this);
            _shortcutsModule = await JS.InvokeAsync<IJSObjectReference>("import", ShortcutsModule);
            if (_shortcutsModule is not null)
            {
                _shortcuts = await _shortcutsModule.InvokeAsync<IJSObjectReference>("registerShortcuts", _self);
            }
        }
        catch (JSException)
        {
            // Raccourcis indisponibles : la page reste utilisable à la souris
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation?.Cancel();
        try
        {
            if (_shortcuts is not null) await _shortcuts.InvokeVoidAsync("dispose");
            if (_shortcuts is not null) await _shortcuts.DisposeAsync();
            if (_shortcutsModule is not null) await _shortcutsModule.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // L'interface est déjà fermée : rien à détacher
        }
        _self?.Dispose();
    }

    /// <summary>Appelé par le module JavaScript : « enter », « s » ou « o » avec Ctrl.</summary>
    [JSInvokable]
    public Task OnShortcut(string key) => InvokeAsync(async () =>
    {
        switch (key)
        {
            case "enter": await RunAsync(); break;
            case "s": await SaveAsync(); break;
            case "o": await OpenAsync(); break;
        }
        StateHasChanged();
    });

    private void OnFormChanged()
    {
        Session.IsDirty = true;
        Refresh();
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

        // L'arme dont les mots-clés étaient ouverts a été supprimée
        if (_keywordWeapon is not null && !Form.Attacker.Weapons.Contains(_keywordWeapon))
        {
            _keywordWeapon = null;
        }
    }

    private void Load(ScenarioForm form, string? fileName)
    {
        Session.Form = form;
        Session.FileName = fileName;
        Session.IsDirty = false;
        Session.Result = null;
        _keywordWeapon = null;
        _message = null;
        Refresh();
    }

    /// <summary>Demande confirmation avant d'abandonner des modifications non enregistrées.</summary>
    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!Session.IsDirty)
        {
            return true;
        }
        bool? discard = await DialogService.ShowMessageBoxAsync(
            L["Confirm_Title"], L["Confirm_Message"], yesText: L["Confirm_Discard"], cancelText: L["Confirm_Cancel"]);
        return discard == true;
    }

    private async Task NewScenarioAsync()
    {
        if (Running || !await ConfirmDiscardAsync())
        {
            return;
        }
        Load(ScenarioSession.NewForm(), null);
    }

    private async Task OpenAsync()
    {
        if (Running || !await ConfirmDiscardAsync())
        {
            return;
        }

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
        if (!CanSave)
        {
            return;
        }

        try
        {
            string? written = await FileService.SaveAsync(_scenario!, Session.FileName ?? DefaultFileName);
            if (written is not null)
            {
                Session.FileName = written;
                Session.IsDirty = false;
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

        Session.IsRunning = true;
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
            Session.Result = await SimulationService.RunAsync(_scenario!, Session.Iterations, Session.Seed, progress, _cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            ShowMessage(L["Simulation_Cancelled"], Severity.Info);
        }
        finally
        {
            Session.IsRunning = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    private void Cancel() => _cancellation?.Cancel();

    private void EditKeywords(WeaponForm weapon) => _keywordWeapon = weapon;

    private void CloseKeywords() => _keywordWeapon = null;

    private void OnKeywordPanelOpenChanged(bool open)
    {
        if (!open)
        {
            CloseKeywords();
        }
    }

    private void ShowMessage(string message, Severity severity)
    {
        _message = message;
        _messageSeverity = severity;
    }
}
