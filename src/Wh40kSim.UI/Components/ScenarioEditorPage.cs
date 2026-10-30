using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.UI.Forms;
using Wh40kSim.UI.Resources;
using Wh40kSim.UI.Services;

namespace Wh40kSim.UI.Components;

/// <summary>
/// Base des écrans de saisie d'un scénario (Quick sim, Full sim) : tout ce qu'ils partagent, sans la mise en page.
/// Le scénario courant (formulaire, fichier, modifications non enregistrées) est tenu par <see cref="ScenarioSession"/>
/// et survit au changement d'écran ; chaque écran a ses propres réglages et son résultat (<see cref="Run"/>). La page
/// garde ce qui s'en déduit (validation par le Core) et la simulation en cours. Le scénario est reconstruit et
/// revalidé après chaque modification signalée par un panneau. Raccourcis : Ctrl+Entrée lancer, Ctrl+S enregistrer,
/// Ctrl+O ouvrir.
/// </summary>
public abstract class ScenarioEditorPage : ComponentBase, IAsyncDisposable
{
    private const string DefaultFileName = "scenario.json";

    /// <summary>Progression minimale entre deux rafraîchissements de la barre (1 %).</summary>
    private const double ProgressStep = 0.01;
    private const string ShortcutsModule = "./_content/Wh40kSim.UI/wh40ksim.js";

    private Scenario? _scenario;          // scénario saisi, null tant qu'une expression de dés est invalide
    private CancellationTokenSource? _cancellation;
    private QueueStatus? _queue;          // attente du lancement en cours dans la file du serveur
    private IDialogReference? _queueDialog;

    private IJSObjectReference? _shortcutsModule;
    private IJSObjectReference? _shortcuts;
    private DotNetObjectReference<ScenarioEditorPage>? _self;

    [Inject]
    private ISimulationService SimulationService { get; set; } = null!;

    [Inject]
    private IScenarioFileService FileService { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    /// <summary>Textes de l'interface (privé : les écrans dérivés ont leur propre « L », injecté par _Imports.razor).</summary>
    [Inject]
    private IStringLocalizer<Strings> L { get; set; } = null!;

    [Inject]
    protected ScenarioSession Session { get; set; } = null!;

    /// <summary>Réglages et résultat du mode de cet écran.</summary>
    protected abstract SimulationRun Run { get; }

    /// <summary>Formulaire en cours d'édition.</summary>
    public ScenarioForm Form => Session.Form;

    /// <summary>Le formulaire a été modifié depuis le dernier « Nouveau », « Ouvrir » ou « Enregistrer ».</summary>
    public bool IsDirty => Session.IsDirty;

    /// <summary>Dernier scénario lisible : décide des options de situation à afficher (stable pendant une saisie de dés invalide).</summary>
    protected Scenario Reference { get; private set; } = new();

    protected IReadOnlyList<string> Errors { get; private set; } = [];

    /// <summary>Avertissements du Core (ce qui ne jouera pas), qui n'empêchent pas de lancer.</summary>
    protected IReadOnlyList<string> Warnings { get; private set; } = [];

    protected bool HasDiceErrors { get; private set; }

    /// <summary>Arme dont les mots-clés sont ouverts dans le panneau latéral.</summary>
    protected WeaponForm? KeywordWeapon { get; private set; }

    /// <summary>Progression du calcul en cours : seule la barre se redessine quand elle change, pas la page.</summary>
    protected ProgressState Progress { get; } = new();

    protected string? Message { get; private set; }

    protected Severity MessageSeverity { get; private set; }

    protected bool Running => Session.IsRunning;

    protected bool CanRun => !Running && _scenario is not null && Errors.Count == 0 && WithinMaxIterations;

    /// <summary>Plafond de simulations par lancement du service (null = aucun, cas du desktop).</summary>
    protected int? MaxIterations => SimulationService.MaxIterations;

    private bool WithinMaxIterations => MaxIterations is not int max || Run.Iterations <= max;

    /// <summary>On peut enregistrer un scénario incohérent (à corriger plus tard), pas une saisie de dés illisible.</summary>
    protected bool CanSave => !Running && _scenario is not null;

    /// <summary>Les dés ne se saisissent que dans les armes : une erreur de dés rend l'attaquant (seul) illisible.</summary>
    protected bool CanSaveAttacker => !Running && !HasDiceErrors;

    /// <summary>Le dernier résultat de ce mode correspond à un ancien scénario (voir <see cref="ScenarioSession.IsStale"/>).</summary>
    protected bool ResultIsStale => Session.IsStale(Run);

    /// <summary>Nom du fichier (ou « Nouveau scénario »), suivi de « * » s'il y a des modifications non enregistrées.</summary>
    protected string FileLabel => (Session.FileName ?? L["Toolbar_Untitled"]) + (Session.IsDirty ? " *" : "");

    /// <summary>Appelé quand une simulation se termine sans annulation (son résultat est dans <see cref="Run"/>).</summary>
    protected virtual void OnSimulationCompleted()
    {
    }

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

    /// <summary>Modification hors attaquant et défenseur (situation) : seul le scénario est modifié.</summary>
    protected void OnFormChanged()
    {
        Session.IsDirty = true;
        Refresh();
    }

    /// <summary>Modification de l'attaquant (armes, mots-clés, boosts) : le scénario et l'attaquant sont modifiés.</summary>
    protected void OnAttackerChanged()
    {
        Session.AttackerDirty = true;
        OnFormChanged();
    }

    /// <summary>
    /// Modification du défenseur (groupes, boosts) : le scénario et le défenseur sont modifiés. Une cible de Precision
    /// qui n'est plus un PERSONNAGE du défenseur (supprimé, décoché) revient au choix par défaut (le premier PERSONNAGE) ;
    /// un renommage est suivi avant (OnPrecisionTargetChanged).
    /// </summary>
    protected void OnDefenderChanged()
    {
        Session.DefenderDirty = true;
        if (Form.Context.PrecisionTarget is string target && !Form.Defender.Groups.Any(g => g.IsCharacter && g.Name == target))
        {
            Form.Context.PrecisionTarget = null;
        }
        OnFormChanged();
    }

    /// <summary>Cible de Precision choisie sur une carte du défenseur : même champ que la liste de la Situation.</summary>
    protected void OnPrecisionTargetChanged(string? target)
    {
        Form.Context.PrecisionTarget = target;
        OnFormChanged();
    }

    private void Refresh()
    {
        try
        {
            _scenario = Form.ToScenario();
            Reference = _scenario;
            Errors = _scenario.Validate();
            Warnings = _scenario.Warnings();
            HasDiceErrors = false;
        }
        catch (FormatException)
        {
            _scenario = null;
            Errors = [];
            Warnings = [];
            HasDiceErrors = true;
        }

        // L'arme dont les mots-clés étaient ouverts a été supprimée
        if (KeywordWeapon is not null && !Form.Attacker.Weapons.Contains(KeywordWeapon))
        {
            KeywordWeapon = null;
        }
    }

    private void Load(ScenarioForm form, string? fileName)
    {
        Session.Form = form;
        Session.FileName = fileName;
        MarkAllSaved();
        Session.ClearResults();
        KeywordWeapon = null;
        Message = null;
        Refresh();
    }

    private void MarkAllSaved()
    {
        Session.IsDirty = false;
        Session.AttackerDirty = false;
        Session.DefenderDirty = false;
    }

    /// <summary>
    /// Demande confirmation avant d'abandonner des modifications non enregistrées de ce qui va être remplacé :
    /// <paramref name="modified"/> = le scénario entier, ou seulement la partie concernée (attaquant, défenseur).
    /// </summary>
    private async Task<bool> ConfirmDiscardAsync(bool modified)
    {
        if (!modified)
        {
            return true;
        }
        bool? discard = await DialogService.ShowMessageBoxAsync(
            L["Confirm_Title"], L["Confirm_Message"], yesText: L["Confirm_Discard"], cancelText: L["Confirm_Cancel"]);
        return discard == true;
    }

    protected async Task NewScenarioAsync()
    {
        if (Running || !await ConfirmDiscardAsync(Session.IsDirty))
        {
            return;
        }
        Load(ScenarioSession.NewForm(), null);
    }

    /// <summary>
    /// Bouton « Ouvrir » de la barre : un scénario complet remplace tout ; un fichier attaquant (ou défenseur) ne
    /// remplace que cette partie, comme le bouton de la carte correspondante. Le fichier est choisi d'abord : la
    /// confirmation porte sur ce qu'il remplace (le scénario, ou seulement l'attaquant ou le défenseur).
    /// </summary>
    protected async Task OpenAsync()
    {
        if (Running || await PickFileAsync() is not var (fileName, file))
        {
            return;
        }

        bool modified = file.Kind switch
        {
            ScenarioFileKind.Scenario => Session.IsDirty,
            ScenarioFileKind.Attacker => Session.AttackerDirty,
            _ => Session.DefenderDirty,
        };
        if (!await ConfirmDiscardAsync(modified))
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
    protected async Task OpenAttackerAsync()
    {
        if (Running || !await ConfirmDiscardAsync(Session.AttackerDirty) || await PickFileAsync() is not var (fileName, file))
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
    protected async Task OpenDefenderAsync()
    {
        if (Running || !await ConfirmDiscardAsync(Session.DefenderDirty) || await PickFileAsync() is not var (fileName, file))
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

    /// <summary>
    /// Une partie chargée modifie le scénario courant, qui garde son nom de fichier ; la partie elle-même correspond
    /// à son fichier (rien à perdre en la remplaçant).
    /// </summary>
    private void LoadAttacker(AttackerUnit attacker, string fileName)
    {
        Form.Attacker = new AttackerForm(attacker);
        Session.IsDirty = true;
        Session.AttackerDirty = false;
        ShowMessage(L["File_AttackerLoaded", fileName], Severity.Success);
        Refresh();
    }

    private void LoadDefender(DefenderUnit defender, string fileName)
    {
        Form.Defender = new DefenderForm(defender);
        Session.IsDirty = true;
        Session.DefenderDirty = false;
        ShowMessage(L["File_DefenderLoaded", fileName], Severity.Success);
        Refresh();
    }

    protected async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        string? written = await WriteAsync(ScenarioJson.Serialize(_scenario!), Session.FileName ?? DefaultFileName, name => L["File_Saved", name]);
        if (written is not null)
        {
            Session.FileName = written;
            MarkAllSaved();
        }
    }

    /// <summary>
    /// Enregistre l'attaquant seul (boosts compris) : l'attaquant est sauvé (le remplacer ne fait rien perdre), mais le
    /// scénario courant n'est pas considéré comme enregistré.
    /// </summary>
    protected async Task SaveAttackerAsync()
    {
        if (CanSaveAttacker)
        {
            var attacker = Form.Attacker.ToAttackerUnit();
            if (await WriteAsync(ScenarioJson.Serialize(attacker), SuggestedFileName(attacker.Name, L["File_DefaultAttacker"]),
                    name => L["File_AttackerSaved", name]) is not null)
            {
                Session.AttackerDirty = false;
            }
        }
    }

    protected async Task SaveDefenderAsync()
    {
        if (!Running)
        {
            var defender = Form.Defender.ToDefenderUnit();
            if (await WriteAsync(ScenarioJson.Serialize(defender), SuggestedFileName(defender.Name, L["File_DefaultDefender"]),
                    name => L["File_DefenderSaved", name]) is not null)
            {
                Session.DefenderDirty = false;
            }
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

    /// <summary>
    /// Caractères retirés des noms de fichier proposés : ceux que Windows interdit, quel que soit le système (Linux et le
    /// navigateur en acceptent davantage, mais les fichiers doivent pouvoir être ouverts partout).
    /// </summary>
    private static readonly char[] ForbiddenFileNameChars =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*', .. Enumerable.Range(0, 32).Select(c => (char)c)];

    /// <summary>Nom proposé pour une partie : le nom de l'unité sans les caractères interdits, à défaut un nom générique.</summary>
    private static string SuggestedFileName(string unitName, string fallback)
    {
        string name = string.Concat(unitName.Split(ForbiddenFileNameChars)).Trim();
        return (name.Length > 0 ? name : fallback) + ".json";
    }

    protected async Task RunAsync()
    {
        if (!CanRun)
        {
            return;
        }

        Session.IsRunning = true;
        Progress.Report(0);
        Message = null;
        _cancellation = new CancellationTokenSource();
        // Les rapports arrivent des threads de calcul (ou du serveur) : seule la barre se redessine (ProgressState), à
        // chaque point de pourcentage gagné (un rapport par paquet de simulations : 1 000 pour 10 M, ticket #12) ; la page
        // n'est pas redessinée pendant le calcul
        double shown = 0;
        var progress = new Progress<double>(value =>
        {
            _ = InvokeAsync(CloseQueueDialog); // le calcul a commencé : plus d'attente
            if (value - shown >= ProgressStep || value >= 1)
            {
                shown = value;
                Progress.Report(value);
            }
        });
        // File d'attente du serveur (web seulement) : fenêtre avec la position, fermée au démarrage du calcul
        _queue = new QueueStatus();
        var queuePosition = new Progress<int>(position => _ = InvokeAsync(() => ShowQueueDialogAsync(position)));

        bool completed = false;
        try
        {
            Run.Result = await SimulationService.RunAsync(_scenario!, Run.Iterations, Run.Seed, progress, _cancellation.Token, queuePosition);
            completed = true;
        }
        catch (OperationCanceledException)
        {
            ShowMessage(L["Simulation_Cancelled"], Severity.Info);
        }
        catch (SimulationServiceException ex)
        {
            ShowMessage(ex.Message, Severity.Error);
        }
        finally
        {
            CloseQueueDialog();
            Session.IsRunning = false;
            _cancellation.Dispose();
            _cancellation = null;
        }

        if (completed)
        {
            OnSimulationCompleted();
        }
    }

    protected void Cancel() => _cancellation?.Cancel();

    /// <summary>Nouvelle position dans la file : ouvre la fenêtre d'attente la première fois, sinon la met à jour.</summary>
    private async Task ShowQueueDialogAsync(int position)
    {
        if (_queue is not { Started: false } queue)
        {
            return;
        }
        queue.Position = position;
        if (_queueDialog is null && !queue.Opening)
        {
            queue.Opening = true;
            var parameters = new DialogParameters<QueueDialog>
            {
                { d => d.Status, queue },
                { d => d.OnCancel, EventCallback.Factory.Create(this, Cancel) },
            };
            var dialog = await DialogService.ShowAsync<QueueDialog>(L["Queue_Title"], parameters, QueueDialog.Options);
            if (queue.Started)
            {
                dialog.Close(); // le calcul a commencé pendant l'ouverture
            }
            else
            {
                _queueDialog = dialog;
            }
        }
    }

    private void CloseQueueDialog()
    {
        if (_queue is not null)
        {
            _queue.Started = true;
        }
        _queueDialog?.Close();
        _queueDialog = null;
    }

    protected void EditKeywords(WeaponForm weapon) => KeywordWeapon = weapon;

    protected void CloseKeywords() => KeywordWeapon = null;

    protected void ClearMessage() => Message = null;

    private void ShowMessage(string message, Severity severity)
    {
        Message = message;
        MessageSeverity = severity;
    }
}
