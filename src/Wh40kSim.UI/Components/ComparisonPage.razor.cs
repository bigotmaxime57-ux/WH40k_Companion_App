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

    /// <summary>Dernier scénario lisible de chaque attaquant : décide des conditions affichées sur sa carte.</summary>
    private readonly Dictionary<ComparisonEntryForm, Scenario> _references = [];

    private IReadOnlyList<string> Errors { get; set; } = [];

    private IReadOnlyList<string> Warnings { get; set; } = [];

    private bool HasDiceErrors { get; set; }

    private WeaponForm? KeywordWeapon { get; set; }

    private ProgressState Progress { get; } = new();

    private string? Message { get; set; }

    private Severity MessageSeverity { get; set; }

    private bool Running => Session.IsRunning;

    private bool CanRun => !Running && _comparison is not null && Errors.Count == 0 && LimitMessage is null;

    /// <summary>Plafond de simulations du service (null = aucun, cas du desktop).</summary>
    private int? MaxIterations => SimulationService.MaxIterations;

    /// <summary>
    /// Limite du site dépassée (version web) : simulations de la finale ou nombre de variantes ; null si rien ne
    /// dépasse (toujours en desktop).
    /// </summary>
    private string? LimitMessage
    {
        get
        {
            if (MaxIterations is int max && State.Iterations > max)
            {
                return L["Comparison_LimitIterations", max.ToString("N0")];
            }
            int variants = Form.Entries.Sum(e => e.VariantCount);
            return SimulationService.MaxComparisonVariants is int maxVariants && variants > maxVariants
                ? L["Comparison_LimitVariants", maxVariants, variants].Value
                : null;
        }
    }

    private QueueStatus? _queue;          // attente du lancement en cours dans la file du serveur
    private IDialogReference? _queueDialog;

    private bool CanSave => !Running && _comparison is not null;

    private string FileLabel => (State.FileName ?? L["Comparison_Untitled"]) + (State.IsDirty ? " *" : "");

    /// <summary>
    /// « 3 ronde(s) : 10 000 · 100 000 · 1 000 000 simulations », précédé du nombre de variantes quand des profils de
    /// rechange en ajoutent (ce sont elles que les rondes comptent).
    /// </summary>
    private string RoundPlanLabel
    {
        get
        {
            int variants = Form.Entries.Sum(e => e.VariantCount);
            var plan = RoundPlan.For(Math.Max(1, variants), Math.Max(1, State.Iterations));
            string rounds = L["Comparison_RoundPlan", plan.Count, string.Join(" · ", plan.Select(r => r.ToString("N0")))];
            return variants > Form.Entries.Count ? L["Comparison_RoundPlanVariants", variants, rounds] : rounds;
        }
    }

    /// <summary>« 4 variantes » sur la carte d'un attaquant à profils de rechange ; null sans.</summary>
    private string? VariantNote(ComparisonEntryForm entry) =>
        entry.VariantCount > 1 ? L["Comparison_VariantCount", entry.VariantCount].Value : null;

    /// <summary>Attaquant (et entrée) auquel appartient une arme ou un profil de rechange ; null s'il n'existe plus.</summary>
    private ComparisonEntryForm? OwnerOf(WeaponForm weapon) =>
        Form.Entries.FirstOrDefault(e => e.Attacker.Weapons.Contains(weapon) || e.Alternatives.Values.Any(p => p.Contains(weapon)));

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
    /// Comparaison de départ : défenseur, overwatch et attaquant (avec sa situation) du scénario courant, copiés : les
    /// modifier ici ne touche pas au Quick sim. Une saisie de dés illisible dans le scénario courant donne une comparaison
    /// vierge.
    /// </summary>
    private ComparisonForm FromCurrentScenario()
    {
        try
        {
            var scenario = Session.Form.ToScenario();
            return new ComparisonForm(new Comparison
            {
                Defender = scenario.Defender,
                SnapShooting = scenario.Context.SnapShooting,
                Entries = [new ComparisonEntry { Attacker = scenario.Attacker, Context = scenario.Context with { SnapShooting = false } }],
            });
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
            // Scénario de chaque attaquant : décide des conditions à afficher sur sa carte (gardé pendant une saisie de dés invalide)
            var scenarios = _comparison.ToScenarios();
            _references.Clear();
            for (int i = 0; i < Form.Entries.Count; i++)
            {
                _references[Form.Entries[i]] = scenarios[i];
            }
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

        // L'arme (ou le profil de rechange) dont les mots-clés étaient ouverts a été supprimée
        if (KeywordWeapon is not null && OwnerOf(KeywordWeapon) is null)
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
        if (KeywordWeapon is not null && OwnerOf(KeywordWeapon) is { } owner)
        {
            OnAttackerChanged(owner.Attacker);
        }
        else
        {
            OnChanged();
        }
    }

    /// <summary>
    /// Défenseur modifié ; la cible de Precision d'un attaquant qui n'est plus un PERSONNAGE du défenseur revient au
    /// choix par défaut (le premier PERSONNAGE).
    /// </summary>
    private void OnDefenderChanged()
    {
        State.DefenderDirty = true;
        foreach (var entry in Form.Entries)
        {
            if (entry.Context.PrecisionTarget is string target && !Form.Defender.Groups.Any(g => g.IsCharacter && g.Name == target))
            {
                entry.Context.PrecisionTarget = null;
            }
        }
        OnChanged();
    }

    /// <summary>Scénario d'un attaquant (dernier lisible), pour les conditions de sa carte.</summary>
    private Scenario ReferenceOf(ComparisonEntryForm entry) => _references.GetValueOrDefault(entry) ?? new Scenario();

    private void EditKeywords(WeaponForm weapon) => KeywordWeapon = weapon;

    private void CloseKeywords() => KeywordWeapon = null;

    // Attaquants

    /// <summary>Ajoute un attaquant et sa situation (sans l'overwatch, commun), sous un nom libre.</summary>
    private void Add(AttackerUnit attacker, AttackContext? context, bool expand)
    {
        var entry = new ComparisonEntryForm(new AttackerForm(attacker), new ContextForm((context ?? new AttackContext()) with { SnapShooting = false }));
        entry.Attacker.Name = Form.UniqueName(string.IsNullOrWhiteSpace(attacker.Name) ? L["Attacker_Title"] : attacker.Name);
        Form.Entries.Add(entry);
        if (expand)
        {
            _expandOnAdd.Add(entry.Attacker);
        }
        OnChanged();
    }

    private string LastName => Form.Entries[^1].Attacker.Name;

    /// <summary>Copie de l'attaquant du scénario courant (Quick sim / Full sim), avec sa situation.</summary>
    private void AddFromScenario()
    {
        try
        {
            var scenario = Session.Form.ToScenario();
            Add(scenario.Attacker, scenario.Context, expand: false);
            ShowMessage(L["Comparison_AddedFromScenario", LastName], Severity.Success);
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
                var read = ScenarioJson.ReadFile(file.Content);
                if (read.Attacker is { } attacker)
                {
                    Add(attacker, read.Context, expand: false);
                    added.Add(LastName);
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
        var weapon = new WeaponForm().ToWeaponGroup();
        Add(new AttackerUnit { Weapons = [weapon] }, null, expand: true);
    }

    /// <summary>Copie d'un attaquant, de sa situation et de ses profils de rechange, juste après lui.</summary>
    private void Duplicate(ComparisonEntryForm entry)
    {
        ComparisonEntryForm copy;
        try
        {
            copy = entry.Clone();
        }
        catch (FormatException)
        {
            ShowMessage(L["Comparison_AttackerUnreadable", entry.Attacker.Name], Severity.Warning);
            return;
        }
        copy.Attacker.Name = Form.UniqueName(entry.Attacker.Name);
        Form.Entries.Insert(Form.Entries.IndexOf(entry) + 1, copy);
        OnChanged();
    }

    /// <summary>Change l'ordre des attaquants (ordre de saisie : départage des égalités exactes et ordre du fichier).</summary>
    private void Move(int index, int offset)
    {
        int target = index + offset;
        if (target < 0 || target >= Form.Entries.Count)
        {
            return;
        }
        (Form.Entries[index], Form.Entries[target]) = (Form.Entries[target], Form.Entries[index]);
        OnChanged();
    }

    private void Remove(ComparisonEntryForm entry)
    {
        Form.Entries.Remove(entry);
        State.DirtyAttackers.Remove(entry.Attacker);
        OnChanged();
    }

    /// <summary>
    /// Remplace un attaquant par celui d'un fichier (attaquant seul ou scénario complet, dont la situation est reprise),
    /// à la même place.
    /// </summary>
    private async Task OpenAttackerAsync(ComparisonEntryForm entry)
    {
        if (Running || !await ConfirmDiscardAsync(State.DirtyAttackers.Contains(entry.Attacker)) || await PickFileAsync() is not var (fileName, file))
        {
            return;
        }
        if (file.Attacker is null)
        {
            ShowMessage(L["File_NoAttacker", fileName], Severity.Warning);
            return;
        }

        State.DirtyAttackers.Remove(entry.Attacker);
        var loaded = new AttackerForm(file.Attacker);
        loaded.Name = Form.UniqueName(loaded.Name, except: entry.Attacker);
        entry.Attacker = loaded;
        entry.Alternatives.Clear();   // profils de rechange de l'ancien attaquant, sans objet
        if (file.Context is { } context)
        {
            entry.Context = new ContextForm(context with { SnapShooting = false });
        }
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
    /// son défenseur et son overwatch, qui remplacent ceux de la comparaison, et son attaquant avec sa situation, qui
    /// s'ajoute aux autres ; un fichier attaquant s'ajoute ; un fichier défenseur remplace le défenseur. Confirmation si
    /// ce qui est remplacé a été modifié.
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
                Form.SnapShooting = file.Context?.SnapShooting ?? false;
            }
        }
        if (file.Attacker is not null)
        {
            Add(file.Attacker, file.Context, expand: false);
        }
        else
        {
            OnChanged();
        }

        ShowMessage(file.Kind switch
        {
            ScenarioFileKind.Scenario => L["Comparison_ScenarioImported", fileName, LastName],
            ScenarioFileKind.Attacker => L["Comparison_AttackerImported", fileName, LastName],
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
            _ = InvokeAsync(CloseQueueDialog); // le calcul a commencé : plus d'attente
            if (p.Round != shown.Round || p.Fraction - shown.Value >= 0.01 || p.Fraction >= 1)
            {
                shown = (p.Round, p.Fraction);
                Progress.Report(p.Fraction, L["Comparison_Round", p.Round, p.RoundCount]);
            }
        });
        // File d'attente du serveur (web seulement) : même fenêtre que pour une simulation
        _queue = new QueueStatus();
        var queuePosition = new Progress<int>(position => _ = InvokeAsync(() => ShowQueueDialogAsync(position)));

        bool completed = false;
        try
        {
            State.Result = await SimulationService.RunComparisonAsync(
                _comparison!, State.Iterations, State.Seed, progress, _cancellation.Token, queuePosition);
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
            CloseQueueDialog();
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
