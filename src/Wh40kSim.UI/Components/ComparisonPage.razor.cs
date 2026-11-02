using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.Simulation;
using Wh40kSim.Simulation.Comparisons;
using Wh40kSim.UI.Forms;
using Wh40kSim.UI.Services;

namespace Wh40kSim.UI.Components;

/// <summary>
/// Logique de la saisie du comparateur : comparaison reconstruite et revalidée par le Core après chaque modification,
/// fichiers (comparaison, défenseur, attaquants), ajout des attaquants, lancement en rondes et raccourcis clavier.
/// </summary>
public partial class ComparisonPage
{
    private const string DefaultFileName = "comparaison.json";

    private Comparison? _comparison;      // comparaison saisie, null tant qu'une expression de dés est invalide
    private CancellationTokenSource? _cancellation;
    private readonly HashSet<AttackerForm> _expandOnAdd = [];   // attaquants ajoutés à la main : carte dépliée
    private readonly KeyboardShortcuts _shortcuts;

    public ComparisonPage() => _shortcuts = new KeyboardShortcuts(OnShortcut);

    [Inject]
    private ScenarioSession Session { get; set; } = null!;

    [Inject]
    private ISimulationService SimulationService { get; set; } = null!;

    [Inject]
    private IScenarioFileService FileService { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private ComparisonState State => Session.Comparison;

    /// <summary>Comparaison en cours d'édition (créée depuis le scénario courant à la première ouverture).</summary>
    public ComparisonForm Form => State.Form!;

    /// <summary>
    /// Scénario de référence pour les options de situation et la cible de Precision : le défenseur et la situation, avec
    /// toutes les armes de tous les attaquants (une option qui compte pour l'un d'eux est proposée).
    /// </summary>
    private Scenario Reference { get; set; } = new();

    private IReadOnlyList<string> Errors { get; set; } = [];

    private IReadOnlyList<string> Warnings { get; set; } = [];

    private bool HasDiceErrors { get; set; }

    private WeaponForm? KeywordWeapon { get; set; }

    private ProgressState Progress { get; } = new();

    private string? Message { get; set; }

    private Severity MessageSeverity { get; set; }

    private bool Running => Session.IsRunning;

    private bool CanRun => !Running && _comparison is not null && Errors.Count == 0;

    private bool CanSave => !Running && _comparison is not null;

    private string FileLabel => (State.FileName ?? L["Comparison_Untitled"]) + (State.IsDirty ? " *" : "");

    /// <summary>« 12 attaquants : 3 rondes (10 000 · 100 000 · 1 000 000) ».</summary>
    private string RoundPlanLabel
    {
        get
        {
            var plan = RoundPlan.For(Math.Max(1, Form.Attackers.Count), Math.Max(1, State.Iterations));
            return L["Comparison_RoundPlan", plan.Count, string.Join(" · ", plan.Select(r => r.ToString("N0")))];
        }
    }

    protected override void OnInitialized()
    {
        State.Form ??= FromCurrentScenario();
        Refresh();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _shortcuts.RegisterAsync(JS);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation?.Cancel();
        await _shortcuts.DisposeAsync();
    }

    /// <summary>Raccourci clavier : « enter » lancer, « s » enregistrer, « o » ouvrir.</summary>
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

    /// <summary>
    /// Comparaison de départ : défenseur, situation et attaquant du scénario courant (copiés : les modifier ici ne touche
    /// pas au Quick sim). Une saisie de dés illisible dans le scénario courant donne une comparaison vierge.
    /// </summary>
    private ComparisonForm FromCurrentScenario()
    {
        try
        {
            var scenario = Session.Form.ToScenario();
            return new ComparisonForm(new Comparison { Defender = scenario.Defender, Context = scenario.Context, Attackers = [scenario.Attacker] });
        }
        catch (FormatException)
        {
            return new ComparisonForm();
        }
    }

    private void Refresh()
    {
        try
        {
            _comparison = Form.ToComparison();
            Reference = new Scenario
            {
                Attacker = new AttackerUnit { Weapons = _comparison.Attackers.SelectMany(a => a.Weapons).ToList() },
                Defender = _comparison.Defender,
                Context = _comparison.Context,
            };
            Errors = _comparison.Validate();
            Warnings = _comparison.Warnings();
            HasDiceErrors = false;
        }
        catch (FormatException)
        {
            _comparison = null;
            Errors = [];
            Warnings = [];
            HasDiceErrors = true;
        }

        if (KeywordWeapon is not null && !Form.Attackers.Any(a => a.Weapons.Contains(KeywordWeapon)))
        {
            KeywordWeapon = null;
        }
    }

    // Modifications

    private void OnChanged()
    {
        State.IsDirty = true;
        Refresh();
    }

    private void OnAttackerChanged(AttackerForm attacker)
    {
        State.DirtyAttackers.Add(attacker);
        OnChanged();
    }

    private void OnKeywordsChanged()
    {
        if (Form.Attackers.FirstOrDefault(a => KeywordWeapon is not null && a.Weapons.Contains(KeywordWeapon)) is { } owner)
        {
            OnAttackerChanged(owner);
        }
        else
        {
            OnChanged();
        }
    }

    /// <summary>Défenseur modifié ; une cible de Precision qui n'est plus un PERSONNAGE revient au choix par défaut.</summary>
    private void OnDefenderChanged()
    {
        State.DefenderDirty = true;
        if (Form.Context.PrecisionTarget is string target && !Form.Defender.Groups.Any(g => g.IsCharacter && g.Name == target))
        {
            Form.Context.PrecisionTarget = null;
        }
        OnChanged();
    }

    private void OnPrecisionTargetChanged(string? target)
    {
        Form.Context.PrecisionTarget = target;
        OnChanged();
    }

    private void EditKeywords(WeaponForm weapon) => KeywordWeapon = weapon;

    private void CloseKeywords() => KeywordWeapon = null;

    // Attaquants

    private void Add(AttackerForm attacker, bool expand)
    {
        attacker.Name = Form.UniqueName(string.IsNullOrWhiteSpace(attacker.Name) ? L["Attacker_Title"] : attacker.Name);
        Form.Attackers.Add(attacker);
        if (expand)
        {
            _expandOnAdd.Add(attacker);
        }
        OnChanged();
    }

    /// <summary>Copie de l'attaquant du scénario courant (Quick sim / Full sim).</summary>
    private void AddFromScenario()
    {
        try
        {
            Add(new AttackerForm(Session.Form.Attacker.ToAttackerUnit()), expand: false);
            ShowMessage(L["Comparison_AddedFromScenario", Form.Attackers[^1].Name], Severity.Success);
        }
        catch (FormatException)
        {
            ShowMessage(L["Comparison_ScenarioUnreadable"], Severity.Warning);
        }
    }

    /// <summary>Un attaquant par fichier choisi (attaquant seul ou scénario complet) ; les fichiers sans attaquant sont signalés.</summary>
    private async Task AddFromFilesAsync()
    {
        IReadOnlyList<OpenedFile> files;
        try
        {
            files = await FileService.OpenManyAsync();
        }
        catch (IOException ex)
        {
            ShowMessage(L["File_OpenError", ex.Message], Severity.Error);
            return;
        }

        var added = new List<string>();
        var skipped = new List<string>();
        foreach (var file in files)
        {
            try
            {
                if (ScenarioJson.ReadFile(file.Content).Attacker is { } attacker)
                {
                    Add(new AttackerForm(attacker), expand: false);
                    State.DirtyAttackers.Remove(Form.Attackers[^1]);
                    added.Add(Form.Attackers[^1].Name);
                    continue;
                }
            }
            catch (JsonException)
            {
                // Illisible ou pas un fichier de scénario : signalé avec les fichiers sans attaquant
            }
            skipped.Add(file.FileName);
        }

        if (skipped.Count > 0)
        {
            ShowMessage(L["Comparison_FilesSkipped", added.Count, string.Join(", ", skipped)], Severity.Warning);
        }
        else if (added.Count > 0)
        {
            ShowMessage(L["Comparison_FilesAdded", added.Count], Severity.Success);
        }
    }

    private void AddNew()
    {
        var attacker = new AttackerForm();
        attacker.Weapons.Add(new WeaponForm());
        Add(attacker, expand: true);
    }

    private void Duplicate(AttackerForm attacker)
    {
        var copy = new AttackerForm(attacker.ToAttackerUnit());
        copy.Name = Form.UniqueName(attacker.Name);
        Form.Attackers.Insert(Form.Attackers.IndexOf(attacker) + 1, copy);
        OnChanged();
    }

    /// <summary>Change l'ordre des attaquants (ordre de saisie : départage des égalités exactes et ordre du fichier).</summary>
    private void Move(int index, int offset)
    {
        int target = index + offset;
        if (target < 0 || target >= Form.Attackers.Count)
        {
            return;
        }
        (Form.Attackers[index], Form.Attackers[target]) = (Form.Attackers[target], Form.Attackers[index]);
        OnChanged();
    }

    private void Remove(AttackerForm attacker)
    {
        Form.Attackers.Remove(attacker);
        State.DirtyAttackers.Remove(attacker);
        OnChanged();
    }

    /// <summary>Remplace un attaquant par celui d'un fichier (attaquant seul ou scénario complet), à la même place.</summary>
    private async Task OpenAttackerAsync(AttackerForm attacker)
    {
        if (Running || !await ConfirmDiscardAsync(State.DirtyAttackers.Contains(attacker)) || await PickFileAsync() is not var (fileName, file))
        {
            return;
        }
        if (file.Attacker is null)
        {
            ShowMessage(L["File_NoAttacker", fileName], Severity.Warning);
            return;
        }

        var loaded = new AttackerForm(file.Attacker);
        loaded.Name = Form.UniqueName(loaded.Name, except: attacker);
        Form.Attackers[Form.Attackers.IndexOf(attacker)] = loaded;
        State.DirtyAttackers.Remove(attacker);
        ShowMessage(L["File_AttackerLoaded", fileName], Severity.Success);
        OnChanged();
    }

    private async Task SaveAttackerAsync(AttackerForm attacker)
    {
        AttackerUnit unit;
        try
        {
            unit = attacker.ToAttackerUnit();
        }
        catch (FormatException)
        {
            ShowMessage(L["Comparison_AttackerUnreadable", attacker.Name], Severity.Warning);
            return;
        }
        if (await WriteAsync(ScenarioJson.Serialize(unit), FileNames.ForUnit(unit.Name, L["File_DefaultAttacker"]), name => L["File_AttackerSaved", name]) is not null)
        {
            State.DirtyAttackers.Remove(attacker);
        }
    }

    // Défenseur

    private async Task OpenDefenderAsync()
    {
        if (Running || !await ConfirmDiscardAsync(State.DefenderDirty) || await PickFileAsync() is not var (fileName, file))
        {
            return;
        }
        if (file.Defender is null)
        {
            ShowMessage(L["File_NoDefender", fileName], Severity.Warning);
            return;
        }
        Form.Defender = new DefenderForm(file.Defender);
        State.DefenderDirty = false;
        ShowMessage(L["File_DefenderLoaded", fileName], Severity.Success);
        OnChanged();
    }

    private async Task SaveDefenderAsync()
    {
        var defender = Form.Defender.ToDefenderUnit();
        if (await WriteAsync(ScenarioJson.Serialize(defender), FileNames.ForUnit(defender.Name, L["File_DefaultDefender"]), name => L["File_DefenderSaved", name]) is not null)
        {
            State.DefenderDirty = false;
        }
    }

    /// <summary>Reprend le défenseur et la situation du scénario courant (copiés).</summary>
    private async Task TakeDefenderFromScenarioAsync()
    {
        if (Running || !await ConfirmDiscardAsync(State.DefenderDirty))
        {
            return;
        }
        Form.Defender = new DefenderForm(Session.Form.Defender.ToDefenderUnit());
        Form.Context = new ContextForm(Session.Form.Context.ToAttackContext());
        State.DefenderDirty = false;
        ShowMessage(L["Comparison_DefenderTaken"], Severity.Success);
        OnChanged();
    }

    // Fichier de comparaison

    private async Task NewAsync()
    {
        if (Running || !await ConfirmDiscardAsync(State.IsDirty))
        {
            return;
        }
        Load(FromCurrentScenario(), null);
    }

    /// <summary>
    /// Bouton « Ouvrir » de la barre : une comparaison remplace tout ; un scénario complet (Quick sim, Full sim) apporte
    /// son défenseur et sa situation, qui remplacent ceux de la comparaison, et son attaquant, qui s'ajoute aux autres ;
    /// un fichier attaquant s'ajoute ; un fichier défenseur remplace le défenseur. Confirmation si ce qui est remplacé a
    /// été modifié.
    /// </summary>
    private async Task OpenAsync()
    {
        if (Running)
        {
            return;
        }
        OpenedFile? opened;
        try
        {
            opened = await FileService.OpenAsync();
        }
        catch (IOException ex)
        {
            ShowMessage(L["File_OpenError", ex.Message], Severity.Error);
            return;
        }
        if (opened is null)
        {
            return;
        }

        Comparison comparison;
        try
        {
            comparison = ComparisonJson.Deserialize(opened.Content);
        }
        catch (JsonException ex)
        {
            if (ReadScenarioFile(opened.Content) is { } file)
            {
                await ImportAsync(file, opened.FileName);
            }
            else
            {
                ShowMessage(L["File_OpenError", ex.Message], Severity.Error);
            }
            return;
        }

        if (await ConfirmDiscardAsync(State.IsDirty))
        {
            Load(new ComparisonForm(comparison), opened.FileName);
        }
    }

    private static ScenarioFile? ReadScenarioFile(string content)
    {
        try
        {
            return ScenarioJson.ReadFile(content);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Scénario complet ou partiel ouvert dans la comparaison en cours (qui garde son nom de fichier).</summary>
    private async Task ImportAsync(ScenarioFile file, string fileName)
    {
        if (file.Defender is not null && !await ConfirmDiscardAsync(State.DefenderDirty))
        {
            return;
        }

        if (file.Defender is not null)
        {
            Form.Defender = new DefenderForm(file.Defender);
            State.DefenderDirty = false;
            if (file.Kind == ScenarioFileKind.Scenario)
            {
                Form.Context = new ContextForm(file.Context ?? new AttackContext());
            }
        }
        if (file.Attacker is not null)
        {
            Add(new AttackerForm(file.Attacker), expand: false);
        }
        else
        {
            OnChanged();
        }

        ShowMessage(file.Kind switch
        {
            ScenarioFileKind.Scenario => L["Comparison_ScenarioImported", fileName, Form.Attackers[^1].Name],
            ScenarioFileKind.Attacker => L["Comparison_AttackerImported", fileName, Form.Attackers[^1].Name],
            _ => L["File_DefenderLoaded", fileName],
        }, Severity.Success);
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }
        string? written = await WriteAsync(ComparisonJson.Serialize(_comparison!), State.FileName ?? DefaultFileName, name => L["Comparison_Saved", name]);
        if (written is not null)
        {
            State.FileName = written;
            MarkAllSaved();
        }
    }

    private void Load(ComparisonForm form, string? fileName)
    {
        State.Form = form;
        State.FileName = fileName;
        State.Result = null;
        MarkAllSaved();
        KeywordWeapon = null;
        Message = null;
        Refresh();
    }

    private void MarkAllSaved()
    {
        State.IsDirty = false;
        State.DefenderDirty = false;
        State.DirtyAttackers.Clear();
    }

    // Lancement

    private async Task RunAsync()
    {
        if (!CanRun)
        {
            return;
        }

        Session.IsRunning = true;
        Message = null;
        Progress.Report(0);
        _cancellation = new CancellationTokenSource();
        // Une valeur par ronde et par point de pourcentage : seule la barre se redessine (ProgressState)
        var shown = (Round: 0, Value: 0.0);
        var progress = new Progress<RoundProgress>(p =>
        {
            if (p.Round != shown.Round || p.Fraction - shown.Value >= 0.01 || p.Fraction >= 1)
            {
                shown = (p.Round, p.Fraction);
                Progress.Report(p.Fraction, L["Comparison_Round", p.Round, p.RoundCount]);
            }
        });

        bool completed = false;
        try
        {
            State.Result = await SimulationService.RunComparisonAsync(_comparison!, State.Iterations, State.Seed, progress, _cancellation.Token);
            completed = true;
        }
        catch (OperationCanceledException)
        {
            ShowMessage(L["Simulation_Cancelled"], Severity.Info);
        }
        catch (Exception ex) when (ex is SimulationServiceException or NotSupportedException)
        {
            ShowMessage(ex.Message, Severity.Error);
        }
        finally
        {
            Session.IsRunning = false;
            _cancellation.Dispose();
            _cancellation = null;
        }

        if (completed)
        {
            ShowResults();
        }
    }

    private void Cancel() => _cancellation?.Cancel();

    private void ShowResults() => Navigation.NavigateTo(AppRoutes.ComparisonResults);

    // Outils

    private async Task<bool> ConfirmDiscardAsync(bool modified)
    {
        if (!modified)
        {
            return true;
        }
        bool? discard = await DialogService.ShowMessageBoxAsync(
            L["Confirm_Title"], L["Comparison_ConfirmMessage"], yesText: L["Confirm_Discard"], cancelText: L["Confirm_Cancel"]);
        return discard == true;
    }

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

    private void ClearMessage() => Message = null;

    private void ShowMessage(string message, Severity severity)
    {
        Message = message;
        MessageSeverity = severity;
    }
}
