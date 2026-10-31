using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.UI.Components;
using Wh40kSim.UI.Forms;
using Wh40kSim.UI.Services;

namespace Wh40kSim.UI.Tests.Components;

public class SimulatorPageTests : ComponentTestContext
{
    private static bool IsDisabled(IRenderedComponent<SimulatorPage> page, string testId) =>
        page.Find($"[data-test={testId}]").HasAttribute("disabled");

    private static Scenario Opened() => new()
    {
        Attacker = new AttackerUnit { Name = "Escouade ouverte", Weapons = [new WeaponGroup { ModelCount = 5, Weapon = new WeaponProfile { Skill = 3 } }] },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cibles", Count = 10 }] },
    };

    [Fact]
    public void New_scenario_is_valid_and_texts_are_localized()
    {
        var page = Render<SimulatorPage>();

        Assert.False(IsDisabled(page, "run"));
        Assert.Contains("Attaquant", page.Markup);
        Assert.DoesNotContain("Attacker_Title", page.Markup);
    }

    [Fact]
    public void File_buttons_keep_their_name_when_only_their_icon_is_shown()
    {
        var page = Render<SimulatorPage>();

        // Sur téléphone, le libellé (wh-toolbar-label) est masqué : le nom reste donné par aria-label
        Assert.Equal("Nouveau", page.Find("[data-test=new]").GetAttribute("aria-label"));
        Assert.Equal("Ouvrir", page.Find("[data-test=open]").GetAttribute("aria-label"));
        Assert.Equal("Enregistrer", page.Find("[data-test=save]").GetAttribute("aria-label"));
        Assert.Equal("Nouveau", page.Find("[data-test=new] .wh-toolbar-label").TextContent);
    }

    [Fact]
    public void The_phone_run_bar_shares_the_settings_and_the_validation_of_the_panel()
    {
        var page = Render<SimulatorPage>();

        // Barre compacte (montrée par le CSS sur téléphone seulement) : même nombre de simulations que le panneau
        page.Find("input[data-test=phone-iterations]").Change("10000");
        Assert.Equal("10000", page.Find("input[data-test=iterations]").GetAttribute("value")?.Replace(" ", "").Replace(" ", ""));

        page.Find("input[data-test=attacks]").Change("D");
        Assert.True(IsDisabled(page, "phone-run"));
    }

    [Fact]
    public void Invalid_dice_disables_run_and_is_shown_under_the_field()
    {
        var page = Render<SimulatorPage>();

        page.Find("input[data-test=attacks]").Change("D");

        Assert.True(IsDisabled(page, "run"));
        Assert.NotNull(page.Find("[data-test=dice-errors]"));
        Assert.Contains("Expression de dés invalide", page.Find("input[data-test=attacks]").Closest(".mud-input-control")!.TextContent);
    }

    [Fact]
    public void Dice_are_only_checked_when_leaving_the_field()
    {
        var page = Render<SimulatorPage>();

        // Saisie en cours : le champ n'écoute pas la frappe (pas d'événement « input »), donc pas d'erreur
        Assert.Throws<MissingEventHandlerException>(() => page.Find("input[data-test=attacks]").Input(""));
        Assert.Empty(page.FindAll("[data-test=dice-errors]"));
        Assert.False(IsDisabled(page, "run"));

        // Sortie du champ : la valeur est lue, l'erreur s'affiche
        page.Find("input[data-test=attacks]").Change("");
        Assert.NotNull(page.Find("[data-test=dice-errors]"));
        Assert.True(IsDisabled(page, "run"));
    }

    [Fact]
    public void Fixing_the_dice_enables_run_again()
    {
        var page = Render<SimulatorPage>();
        var attacks = page.Find("input[data-test=attacks]");

        attacks.Change("D");
        page.Find("input[data-test=attacks]").Change("D6+1");

        Assert.False(IsDisabled(page, "run"));
        Assert.Empty(page.FindAll("[data-test=dice-errors]"));
        Assert.Equal("D6+1", page.Instance.Form.Attacker.Weapons[0].Attacks);
    }

    [Fact]
    public void Scenario_errors_from_the_core_are_listed_and_disable_run()
    {
        var page = Render<SimulatorPage>();

        page.Find("[data-test=remove-group]").Click();

        Assert.True(IsDisabled(page, "run"));
        Assert.Contains("au moins un groupe", page.Find("[data-test=scenario-errors]").TextContent);
    }

    [Fact]
    public void Situation_options_appear_only_when_a_weapon_needs_them()
    {
        var page = Render<SimulatorPage>();
        Assert.Empty(page.FindAll("[data-test=half-range]"));

        page.Find("[data-test=edit-keywords]").Click();
        page.Find("input[data-test=keyword-RapidFire]").Change(true);

        Assert.NotEmpty(page.FindAll("[data-test=half-range]"));
        Assert.Empty(page.FindAll("[data-test=charged]"));
        Assert.Empty(page.FindAll("[data-test=remained-stationary]"));
    }

    [Fact]
    public void Overwatch_warns_about_melee_weapons_without_blocking_and_blocks_without_ranged_weapons()
    {
        var page = Render<SimulatorPage>();
        page.Instance.Form.Attacker.Weapons.Add(new WeaponForm { Name = "Épée", Type = WeaponType.Melee });

        page.Find("input[data-test=snap-shooting]").Change(true);

        Assert.Contains("'Épée'", page.Find("[data-test=scenario-warnings]").TextContent);
        Assert.Empty(page.FindAll("[data-test=scenario-errors]"));
        Assert.False(IsDisabled(page, "run"));

        // Plus aucune arme de tir : erreur, Lancer désactivé
        page.Instance.Form.Attacker.Weapons[0].Type = WeaponType.Melee;
        page.Find("input[data-test=snap-shooting]").Change(false);
        page.Find("input[data-test=snap-shooting]").Change(true);

        Assert.Contains("aucune arme de tir", page.Find("[data-test=scenario-errors]").TextContent);
        Assert.True(IsDisabled(page, "run"));
    }

    [Fact]
    public void With_a_server_limit_the_larger_preset_is_hidden_and_input_is_capped()
    {
        Simulations.MaxIterations = 1_000_000;
        var page = Render<SimulatorPage>();

        Assert.Empty(page.FindAll("[data-test=preset-10000000]"));
        Assert.NotEmpty(page.FindAll("[data-test=preset-1000000]"));
        Assert.NotNull(page.Find("[data-test=max-iterations-help]"));

        page.Find("input[data-test=iterations]").Change("5000000");

        Assert.Equal(1_000_000, Services.GetRequiredService<ScenarioSession>().QuickSim.Iterations);
        Assert.False(IsDisabled(page, "run"));
    }

    [Fact]
    public void Without_a_limit_every_preset_is_offered()
    {
        var page = Render<SimulatorPage>();

        Assert.NotEmpty(page.FindAll("[data-test=preset-10000000]"));
        Assert.Empty(page.FindAll("[data-test=max-iterations-help]"));
    }

    [Fact]
    public void Waiting_in_the_server_queue_opens_a_window_with_the_position_until_the_computation_starts()
    {
        Simulations.QueuePositions = [2, 1];
        Simulations.Gate = new TaskCompletionSource();
        var page = Render<SimulatorPage>();

        var dialogs = Dialogs!;
        page.Find("[data-test=run]").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("1 calcul(s)", dialogs.Find("[data-test=queue-position]").TextContent));
        Simulations.Gate.SetResult();
        page.WaitForElement("[data-test=results]");
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-test=queue-dialog]")));
    }

    [Fact]
    public void Cancelling_from_the_queue_window_cancels_the_launch()
    {
        Simulations.QueuePositions = [3];
        Simulations.Gate = new TaskCompletionSource();
        var dialogs = Dialogs!;
        var page = Render<SimulatorPage>();
        page.Find("[data-test=run]").Click();

        dialogs.WaitForElement("[data-test=queue-cancel]").Click();

        page.WaitForAssertion(() => Assert.Contains("annulée", page.Find("[data-test=message]").TextContent));
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-test=queue-dialog]")));
        Assert.False(IsDisabled(page, "run"));
    }

    [Fact]
    public void Progress_redraws_only_the_bar_not_the_page()
    {
        Simulations.ProgressReports = 100;
        Simulations.Gate = new TaskCompletionSource();
        var page = Render<SimulatorPage>();
        page.Find("[data-test=run]").Click();
        // Le compteur de bUnit inclut les rendus des enfants : on suit un panneau voisin de la barre (la Situation), qui
        // ne se redessine que si la page entière se redessine
        var context = page.FindComponent<ContextPanel>();
        int pageRenders = context.RenderCount;
        var bar = page.FindComponent<SimulationProgressBar>();
        int barRenders = bar.RenderCount;

        Simulations.Gate.SetResult();
        page.WaitForElement("[data-test=results]", TimeSpan.FromSeconds(10));

        // La barre a suivi les 100 pourcents ; la page ne s'est redessinée qu'à la fin (résultats)
        Assert.True(bar.RenderCount - barRenders >= 50, $"Barre redessinée {bar.RenderCount - barRenders} fois.");
        Assert.True(context.RenderCount - pageRenders <= 3, $"Page redessinée {context.RenderCount - pageRenders} fois.");
    }

    [Fact]
    public void A_refusal_from_the_service_is_shown_as_a_message()
    {
        Simulations.Error = new SimulationServiceException("Trop de lancements", ["Réessayez dans 30 s."]);
        var page = Render<SimulatorPage>();

        page.Find("[data-test=run]").Click();

        page.WaitForAssertion(() => Assert.Contains("Trop de lancements : Réessayez dans 30 s.", page.Find("[data-test=message]").TextContent));
        Assert.Empty(page.FindAll("[data-test=results]"));
    }

    [Fact]
    public void Running_shows_the_results()
    {
        var page = Render<SimulatorPage>();

        page.Find("[data-test=run]").Click();

        page.WaitForElement("[data-test=results]");
        Assert.Equal(1, Simulations.Calls);
        Assert.Equal(1_000_000, Simulations.LastIterations);
        Assert.Contains("Unité détruite", page.Find("[data-test=wipe]").TextContent);
        Assert.Equal(2, page.FindAll("[data-test=distribution]").Count);
    }

    [Fact]
    public void Changing_the_scenario_after_a_run_flags_the_results_as_stale_until_rerun()
    {
        var page = Render<SimulatorPage>();
        page.Find("[data-test=run]").Click();
        page.WaitForElement("[data-test=results]");
        Assert.Empty(page.FindAll("[data-test=stale-results-banner]"));

        page.Find("[data-test=add-weapon]").Click();

        Assert.NotNull(page.Find("[data-test=stale-results-banner]"));
        Assert.NotNull(page.Find("[data-test=results]"));   // anciens résultats toujours affichés, sans grisé

        page.Find("[data-test=stale-rerun]").Click();

        page.WaitForAssertion(() => Assert.Equal(2, Simulations.Calls));
        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[data-test=stale-results-banner]")));
    }

    [Fact]
    public void Attacker_and_defender_panels_can_be_collapsed()
    {
        var page = Render<SimulatorPage>();

        page.Find("[data-test=toggle-attacker]").Click();

        Assert.True(page.Find("[data-test=attacker-content]").HasAttribute("hidden"));
        Assert.False(page.Find("[data-test=defender-content]").HasAttribute("hidden"));

        page.Find("[data-test=toggle-defender]").Click();
        Assert.True(page.Find("[data-test=defender-content]").HasAttribute("hidden"));
        Assert.Contains("wh-collapse-toggle", page.Find("[data-test=toggle-attacker]").ClassName);

        page.Find("[data-test=toggle-attacker]").Click();
        Assert.False(page.Find("[data-test=attacker-content]").HasAttribute("hidden"));
    }

    [Fact]
    public void A_collapsed_weapon_stays_collapsed_when_the_attacker_is_folded_and_unfolded()
    {
        var page = Render<SimulatorPage>();
        page.Find("[data-test=toggle-weapon]").Click();
        Assert.Equal("false", page.Find("[data-test=toggle-weapon]").GetAttribute("aria-expanded"));

        page.Find("[data-test=toggle-attacker]").Click();
        page.Find("[data-test=toggle-attacker]").Click();

        Assert.Equal("false", page.Find("[data-test=toggle-weapon]").GetAttribute("aria-expanded"));
    }

    /// <summary>Une arme Precision contre des gardes, un leader et un soutien : le choix de la cible compte.</summary>
    private IRenderedComponent<SimulatorPage> OpenTwoCharacters()
    {
        Files.ToOpen = FakeScenarioFileService.File("precision.json", new Scenario
        {
            Attacker = new AttackerUnit
            {
                Weapons = [new WeaponGroup { Weapon = new WeaponProfile { Abilities = new WeaponAbilities { Precision = true } } }],
            },
            Defender = new DefenderUnit
            {
                Groups =
                [
                    new ModelGroup { Name = "Gardes", Count = 5 },
                    new ModelGroup { Name = "Leader", IsCharacter = true, Wounds = 4 },
                    new ModelGroup { Name = "Soutien", IsCharacter = true, Wounds = 3 },
                ],
            },
        });
        var page = Render<SimulatorPage>();
        page.Find("[data-test=open]").Click();
        page.WaitForElement("[data-test=precision-target-toggle]");
        return page;
    }

    private static bool[] TargetStates(IRenderedComponent<SimulatorPage> page) =>
        page.FindAll("[data-test=precision-target-toggle]").Select(t => t.ClassList.Contains("wh-precision-target-active")).ToArray();

    [Fact]
    public void Weapons_can_be_moved_up_and_down_to_change_the_resolution_order()
    {
        var page = Render<SimulatorPage>();
        page.Find("[data-test=add-weapon]").Click();
        page.Instance.Form.Attacker.Weapons[0].Name = "Première";
        page.Instance.Form.Attacker.Weapons[1].Name = "Seconde";
        page.Render();

        var up = page.FindAll("[data-test=move-weapon-up]");
        var down = page.FindAll("[data-test=move-weapon-down]");
        Assert.True(up[0].HasAttribute("disabled"));     // la première ne monte pas
        Assert.True(down[1].HasAttribute("disabled"));   // la dernière ne descend pas

        page.FindAll("[data-test=move-weapon-down]")[0].Click();

        Assert.Equal(["Seconde", "Première"], page.Instance.Form.Attacker.Weapons.Select(w => w.Name));
        Assert.True(page.Instance.IsDirty);

        page.FindAll("[data-test=move-weapon-up]")[1].Click();
        Assert.Equal(["Première", "Seconde"], page.Instance.Form.Attacker.Weapons.Select(w => w.Name));
    }

    [Fact]
    public void Situation_comes_before_the_attacker_and_the_defender()
    {
        var page = Render<SimulatorPage>();

        string markup = page.Markup;
        Assert.True(markup.IndexOf("data-test=\"context\"") < markup.IndexOf("data-test=\"attacker-panel\""));
        Assert.True(markup.IndexOf("data-test=\"context\"") < markup.IndexOf("data-test=\"defender-panel\""));
    }

    [Fact]
    public void Each_character_card_shows_a_precision_target_the_first_one_by_default()
    {
        var page = OpenTwoCharacters();

        Assert.Equal([true, false], TargetStates(page));   // leader visé par défaut, soutien en gris ; rien sur les gardes
        Assert.Null(page.Instance.Form.Context.PrecisionTarget);
    }

    [Fact]
    public void Clicking_a_grey_target_chooses_it_and_clicking_it_again_goes_back_to_the_default()
    {
        var page = OpenTwoCharacters();

        page.FindAll("[data-test=precision-target-toggle]")[1].Click();
        Assert.Equal("Soutien", page.Instance.Form.Context.PrecisionTarget);
        Assert.Equal([false, true], TargetStates(page));

        page.FindAll("[data-test=precision-target-toggle]")[1].Click();
        Assert.Null(page.Instance.Form.Context.PrecisionTarget);
        Assert.Equal([true, false], TargetStates(page));
    }

    [Fact]
    public void A_removed_target_goes_back_to_the_default_and_a_renamed_one_is_followed()
    {
        var page = OpenTwoCharacters();
        page.FindAll("[data-test=precision-target-toggle]")[1].Click();

        page.FindAll("input[data-test=group-name]")[2].Change("Psyker");
        Assert.Equal("Psyker", page.Instance.Form.Context.PrecisionTarget);

        page.FindAll("[data-test=remove-group]")[2].Click();
        Assert.Null(page.Instance.Form.Context.PrecisionTarget);
        // Un seul PERSONNAGE : sa cible reste affichée (visé par défaut), mais plus de liste dans la Situation
        Assert.Equal([true], TargetStates(page));
        Assert.Empty(page.FindAll("[data-test=precision-target]"));
    }

    [Fact]
    public void Without_a_precision_weapon_no_character_shows_a_target()
    {
        var page = OpenTwoCharacters();

        page.Instance.Form.Attacker.Weapons[0].Abilities.Precision = false;
        page.Find("[data-test=add-weapon]").Click();   // une modification de l'attaquant fait relire le scénario

        Assert.Empty(page.FindAll("[data-test=precision-target-toggle]"));
    }

    [Fact]
    public void Running_can_be_cancelled()
    {
        Simulations.Gate = new TaskCompletionSource();
        var page = Render<SimulatorPage>();

        page.Find("[data-test=run]").Click();
        Assert.True(IsDisabled(page, "run"));
        page.ClickUntil("[data-test=cancel]", () => page.FindAll("[data-test=message]").Count > 0);

        page.WaitForAssertion(() => Assert.Contains("Simulation annulée", page.Find("[data-test=message]").TextContent));
        Assert.False(IsDisabled(page, "run"));
        Assert.Empty(page.FindAll("[data-test=results]"));
    }

    [Fact]
    public void Opening_a_file_replaces_the_form()
    {
        Files.ToOpen = FakeScenarioFileService.File("ouvert.json", Opened());
        var page = Render<SimulatorPage>();

        page.Find("[data-test=open]").Click();

        Assert.Equal("Escouade ouverte", page.Instance.Form.Attacker.Name);
        Assert.Equal("ouvert.json", page.Find("[data-test=file-name]").TextContent);
        Assert.False(IsDisabled(page, "run"));
    }

    [Fact]
    public void Cancelled_open_keeps_the_form()
    {
        var page = Render<SimulatorPage>();
        var form = page.Instance.Form;

        page.Find("[data-test=open]").Click();

        Assert.Same(form, page.Instance.Form);
    }

    [Fact]
    public void Invalid_file_shows_an_error()
    {
        Files.ToOpen = new OpenedFile("ancien.json", """{ "defender": { "toughness": 5 } }""");
        var page = Render<SimulatorPage>();
        var form = page.Instance.Form;

        page.Find("[data-test=open]").Click();

        string message = page.Find("[data-test=message]").TextContent;
        Assert.Contains("Impossible d'ouvrir le fichier", message);
        Assert.Contains("toughness", message);
        Assert.Same(form, page.Instance.Form);
    }

    [Fact]
    public void Unreadable_file_shows_an_error()
    {
        Files.OpenError = new IOException("Accès refusé.");
        var page = Render<SimulatorPage>();

        page.Find("[data-test=open]").Click();

        Assert.Contains("Accès refusé.", page.Find("[data-test=message]").TextContent);
    }

    [Fact]
    public void Saving_writes_the_current_scenario_under_the_opened_name()
    {
        Files.ToOpen = FakeScenarioFileService.File("ouvert.json", Opened());
        var page = Render<SimulatorPage>();
        page.Find("[data-test=open]").Click();

        page.Find("[data-test=save]").Click();

        var (content, fileName) = Assert.Single(Files.Saved);
        Assert.Equal("ouvert.json", fileName);
        Assert.Equal("Escouade ouverte", ScenarioJson.Deserialize(content).Attacker.Name);
        Assert.Contains("Scénario enregistré", page.Find("[data-test=message]").TextContent);
    }

    [Fact]
    public void Saving_is_impossible_while_the_dice_are_invalid()
    {
        var page = Render<SimulatorPage>();

        page.Find("input[data-test=damage]").Change("abc");

        Assert.True(IsDisabled(page, "save"));
    }

    [Fact]
    public void New_resets_the_form_and_the_results()
    {
        Files.ToOpen = FakeScenarioFileService.File("ouvert.json", Opened());
        var page = Render<SimulatorPage>();
        page.Find("[data-test=open]").Click();
        page.Find("[data-test=run]").Click();
        page.WaitForElement("[data-test=results]");

        page.Find("[data-test=new]").Click();

        Assert.Equal(new AttackerUnit().Name, page.Instance.Form.Attacker.Name);
        Assert.Empty(page.FindAll("[data-test=results]"));
        Assert.Equal("Nouveau scénario", page.Find("[data-test=file-name]").TextContent);
    }

    [Fact]
    public void Modifications_are_marked_with_a_star_until_saved()
    {
        var page = Render<SimulatorPage>();
        Assert.False(page.Instance.IsDirty);

        page.Find("input[data-test=attacks]").Change("2");

        Assert.True(page.Instance.IsDirty);
        Assert.Equal("Nouveau scénario *", page.Find("[data-test=file-name]").TextContent);
        page.Find("[data-test=save]").Click();
        Assert.Equal("scenario.json", page.Find("[data-test=file-name]").TextContent);
    }

    [Fact]
    public void Losing_modifications_asks_for_confirmation()
    {
        var page = Render<SimulatorPage>();
        page.Find("input[data-test=attacks]").Change("2");

        // « Nouveau » puis « Annuler » : la saisie est gardée
        page.Find("[data-test=new]").Click();
        DialogButton("Annuler").Click();
        Assert.Equal("2", page.Instance.Form.Attacker.Weapons[0].Attacks);

        // « Nouveau » puis « Abandonner » : formulaire vierge
        page.Find("[data-test=new]").Click();
        DialogButton("Abandonner").Click();
        page.WaitForAssertion(() => Assert.Equal("1", page.Instance.Form.Attacker.Weapons[0].Attacks));
        Assert.False(page.Instance.IsDirty);
    }

    [Fact]
    public void Shortcuts_run_save_and_open()
    {
        Files.ToOpen = FakeScenarioFileService.File("ouvert.json", Opened());
        var page = Render<SimulatorPage>();

        page.InvokeAsync(() => page.Instance.OnShortcut("enter"));
        page.WaitForElement("[data-test=results]");
        page.InvokeAsync(() => page.Instance.OnShortcut("s"));
        page.InvokeAsync(() => page.Instance.OnShortcut("o"));

        Assert.Equal(1, Simulations.Calls);
        Assert.Single(Files.Saved);
        Assert.Equal("Escouade ouverte", page.Instance.Form.Attacker.Name);
    }

    [Fact]
    public void Iteration_presets_set_the_number_of_simulations()
    {
        var page = Render<SimulatorPage>();
        // Par défaut 1 000 000 : ce choix est mis en évidence
        Assert.Contains("wh-preset-active", page.Find("[data-test=preset-1000000]").ClassName);

        page.Find("[data-test=preset-10000]").Click();
        Assert.Contains("wh-preset-active", page.Find("[data-test=preset-10000]").ClassName);
        Assert.DoesNotContain("wh-preset-active", page.Find("[data-test=preset-1000000]").ClassName);
        page.Find("[data-test=run]").Click();

        page.WaitForElement("[data-test=results]");
        Assert.Equal(10_000, Simulations.LastIterations);
    }

    [Fact]
    public void Results_start_with_summary_tiles()
    {
        var page = Render<SimulatorPage>();

        page.Find("[data-test=run]").Click();

        page.WaitForElement("[data-test=results]");
        Assert.NotNull(page.Find("[data-test=tile-kills]"));
        Assert.NotNull(page.Find("[data-test=tile-damage]"));
        Assert.Contains("%", page.Find("[data-test=wipe]").TextContent);
        Assert.NotEmpty(page.FindAll("[data-test=bar]"));
    }

    private AngleSharp.Dom.IElement DialogButton(string text)
    {
        AngleSharp.Dom.IElement? button = null;
        Dialogs!.WaitForAssertion(() => button = Assert.Single(Dialogs!.FindAll("button"), b => b.TextContent.Contains(text)));
        return button!;
    }
}
