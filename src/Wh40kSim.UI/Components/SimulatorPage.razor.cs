using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
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

    /// <summary>Les dés ne se saisissent que dans les armes : une erreur de dés rend l'attaquant (seul) illisible.</summary>
    private bool CanSaveAttacker => !Running && !_hasDiceErrors;

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

    /// <summary>
    /// Bouton « Ouvrir » de la barre : un scénario complet remplace tout ; un fichier attaquant (ou défenseur) ne
    /// remplace que cette partie, comme le bouton de la carte correspondante.
    /// </summary>
    private async Task OpenAsync()
    {
        if (Running || !await ConfirmDiscardAsync() || await PickFileAsync() is not var (fileName, file))
        {
            return;
        }

        switch (file.Kind)
        {
            case ScenarioFileKind.Scenario:
                Load(new ScenarioForm(file.ToScenario()), fileName);
                break;
            case ScenarioFileKind.Attacker:
                LoadAttacker(file.Attacker!, fileName);
                break;
            default:
                LoadDefender(file.Defender!, fileName);
                break;
        }
    }

    /// <summary>Remplace l'attaquant par celui d'un fichier attaquant ou d'un scénario complet.</summary>
    private async Task OpenAttackerAsync()
    {
        if (Running || !await ConfirmDiscardAsync() || await PickFileAsync() is not var (fileName, file))
        {
            return;
        }

        if (file.Attacker is null)
        {
            ShowMessage(L["File_NoAttacker", fileName], Severity.Warning);
            return;
        }
        LoadAttacker(file.Attacker, fileName);
    }

    /// <summary>Remplace le défenseur par celui d'un fichier défenseur ou d'un scénario complet.</summary>
    private async Task OpenDefenderAsync()
    {
        if (Running || !await ConfirmDiscardAsync() || await PickFileAsync() is not var (fileName, file))
        {
            return;
        }

        if (file.Defender is null)
        {
            ShowMessage(L["File_NoDefender", fileName], Severity.Warning);
            return;
        }
        LoadDefender(file.Defender, fileName);
    }

    /// <summary>Demande un fichier et le lit (scénario complet ou partiel) ; null si annulé ou illisible (message affiché).</summary>
    private async Task<(string FileName, ScenarioFile File)?> PickFileAsync()
    {
        try
        {
            var opened = await FileService.OpenAsync();
            return opened is null ? null : (opened.FileName, ScenarioJson.ReadFile(opened.Content));
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            ShowMessage(L["File_OpenError", ex.Message], Severity.Error);
            return null;
        }
    }

    /// <summary>Une partie chargée modifie le scénario courant, qui garde son nom de fichier.</summary>
    private void LoadAttacker(AttackerUnit attacker, string fileName)
    {
        Form.Attacker = new AttackerForm(attacker);
        Session.IsDirty = true;
        ShowMessage(L["File_AttackerLoaded", fileName], Severity.Success);
        Refresh();
    }

    private void LoadDefender(DefenderUnit defender, string fileName)
    {
        Form.Defender = new DefenderForm(defender);
        Session.IsDirty = true;
        ShowMessage(L["File_DefenderLoaded", fileName], Severity.Success);
        Refresh();
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        string? written = await WriteAsync(ScenarioJson.Serialize(_scenario!), Session.FileName ?? DefaultFileName, name => L["File_Saved", name]);
        if (written is not null)
        {
            Session.FileName = written;
            Session.IsDirty = false;
        }
    }

    /// <summary>Enregistre l'attaquant seul (boosts compris) ; le scénario courant n'est pas considéré comme enregistré.</summary>
    private async Task SaveAttackerAsync()
    {
        if (CanSaveAttacker)
        {
            var attacker = Form.Attacker.ToAttackerUnit();
            await WriteAsync(ScenarioJson.Serialize(attacker), SuggestedFileName(attacker.Name, L["File_DefaultAttacker"]),
                name => L["File_AttackerSaved", name]);
        }
    }

    private async Task SaveDefenderAsync()
    {
        if (!Running)
        {
            var defender = Form.Defender.ToDefenderUnit();
            await WriteAsync(ScenarioJson.Serialize(defender), SuggestedFileName(defender.Name, L["File_DefaultDefender"]),
                name => L["File_DefenderSaved", name]);
        }
    }

    /// <summary>Écrit un fichier et affiche <paramref name="savedMessage"/> (nom écrit) ; renvoie le nom écrit, ou null.</summary>
    private async Task<string?> WriteAsync(string content, string suggestedFileName, Func<string, string> savedMessage)
    {
        try
        {
            string? written = await FileService.SaveAsync(content, suggestedFileName);
            if (written is not null)
            {
                ShowMessage(savedMessage(written), Severity.Success);
            }
            return written;
        }
        catch (IOException ex)
        {
            ShowMessage(L["File_SaveError", ex.Message], Severity.Error);
            return null;
        }
    }

    /// <summary>Nom proposé pour une partie : le nom de l'unité sans les caractères interdits, à défaut un nom générique.</summary>
    private static string SuggestedFileName(string unitName, string fallback)
    {
        string name = string.Concat(unitName.Split(Path.GetInvalidFileNameChars())).Trim();
        return (name.Length > 0 ? name : fallback) + ".json";
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
