using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.Simulation.Comparisons;
using Wh40kSim.UI.Components;
using Wh40kSim.UI.Forms;
using Wh40kSim.UI.Services;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>
/// Comparateur : tuile de l'accueil, saisie (comparaison reprise du scénario courant, ajouts, duplication, fichiers),
/// lancement, résultats (podium, égalité du 4e, éliminés, tri, menu d'une ligne).
/// </summary>
public class ComparisonPageTests() : ComponentTestContext(renderPopoverProvider: false)
{
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private ScenarioSession Session => Services.GetRequiredService<ScenarioSession>();

    private static DefenderUnit Squad => new()
    {
        Name = "Gardes",
        Groups = [new ModelGroup { Name = "Gardes", Count = 10, Wounds = 1, Toughness = 4, Save = 4 }],
    };

    private static AttackerUnit Attacker(string name, int attacks) => new()
    {
        Name = name,
        Weapons = [new WeaponGroup { ModelCount = 1, Weapon = new WeaponProfile { Name = "Bolter", Attacks = attacks, Skill = 3, Strength = 4 } }],
    };

    /// <summary>Ouvre le comparateur, le scénario courant ayant l'attaquant « Rubrics » contre « Gardes ».</summary>
    private IRenderedComponent<AppShell> OpenComparison()
    {
        Session.Form = new ScenarioForm(new Scenario { Attacker = Attacker("Rubrics", 10), Defender = Squad });
        var shell = Render<AppShell>();
        Navigation.NavigateTo(AppRoutes.Comparison);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<ComparisonPage>()));
        return shell;
    }

    private IRenderedComponent<AppShell> OpenResults(ComparisonResult result)
    {
        Session.Comparison.Form = new ComparisonForm(result.Comparison);
        Session.Comparison.Result = result;
        var shell = Render<AppShell>();
        Navigation.NavigateTo(AppRoutes.ComparisonResults);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<ComparisonResultsPage>()));
        return shell;
    }

    private static string[] AttackerNames(IRenderedComponent<AppShell> shell) =>
        shell.FindComponent<ComparisonPage>().Instance.Form.Attackers.Select(a => a.Name).ToArray();

    // Accueil

    [Fact]
    public void Home_screen_offers_the_comparator_when_the_host_can_compare()
    {
        var shell = Render<AppShell>();

        Assert.Equal(AppRoutes.Comparison, shell.Find("[data-test=tile-comparison]").GetAttribute("href"));
    }

    [Fact]
    public void Home_screen_hides_the_comparator_when_the_host_cannot_compare()
    {
        Simulations.SupportsComparison = false;

        Assert.Empty(Render<AppShell>().FindAll("[data-test=tile-comparison]"));
    }

    // Saisie

    [Fact]
    public void The_first_opening_starts_from_the_current_scenario()
    {
        var shell = OpenComparison();

        Assert.Equal(["Rubrics"], AttackerNames(shell));
        Assert.Equal("Gardes", Session.Comparison.Form!.Defender.Name);
        Assert.Equal("Comparateur", shell.Find("[data-test=screen-name]").TextContent);
        // Un seul attaquant : il en faut au moins deux
        Assert.True(shell.Find("[data-test=run]").HasAttribute("disabled"));
        Assert.Contains("au moins 2 attaquants", shell.Markup);
    }

    [Fact]
    public void Editing_the_comparison_leaves_the_current_scenario_untouched()
    {
        var shell = OpenComparison();

        shell.FindComponent<ComparisonPage>().Instance.Form.Defender.Name = "Autre";

        Assert.Equal("Gardes", Session.Form.Defender.Name);
    }

    [Fact]
    public void Adding_the_current_attacker_again_gets_a_suffix_and_enables_run()
    {
        var shell = OpenComparison();

        shell.Find("[data-test=add-from-scenario]").Click();

        Assert.Equal(["Rubrics", "Rubrics (2)"], AttackerNames(shell));
        Assert.Equal(2, shell.FindAll("[data-test=comparison-attacker]").Count);
        Assert.False(shell.Find("[data-test=run]").HasAttribute("disabled"));
    }

    [Fact]
    public void Duplicate_and_remove_work_on_each_card()
    {
        var shell = OpenComparison();

        shell.Find("[data-test=duplicate-attacker]").Click();
        Assert.Equal(["Rubrics", "Rubrics (2)"], AttackerNames(shell));

        shell.FindAll("[data-test=remove-attacker]")[0].Click();
        Assert.Equal(["Rubrics (2)"], AttackerNames(shell));
    }

    [Fact]
    public void Several_files_can_be_added_at_once()
    {
        var shell = OpenComparison();
        Files.ToOpenMany.Add(FakeScenarioFileService.File("bolters.json", Attacker("Bolters", 8)));
        Files.ToOpenMany.Add(FakeScenarioFileService.File("defenseur.json", Squad));
        Files.ToOpenMany.Add(FakeScenarioFileService.File("scenario.json", new Scenario { Attacker = Attacker("Flammes", 6), Defender = Squad }));

        shell.Find("[data-test=add-from-files]").Click();

        Assert.Equal(["Rubrics", "Bolters", "Flammes"], AttackerNames(shell));
        Assert.Contains("defenseur.json", shell.Find("[data-test=message]").TextContent);
    }

    [Fact]
    public void A_new_attacker_is_added_with_a_weapon()
    {
        var shell = OpenComparison();

        shell.Find("[data-test=add-new-attacker]").Click();

        var added = shell.FindComponent<ComparisonPage>().Instance.Form.Entries[^1].Attacker;
        Assert.Equal("Attaquant", added.Name);
        Assert.Single(added.Weapons);
    }

    [Fact]
    public void The_first_card_is_expanded_and_attackers_added_from_the_scenario_are_collapsed()
    {
        var shell = OpenComparison();

        shell.Find("[data-test=add-from-scenario]").Click();

        var contents = shell.FindAll("[data-test=comparison-attacker] [data-test=attacker-content]");
        Assert.False(contents[0].HasAttribute("hidden"));
        Assert.True(contents[1].HasAttribute("hidden"));
    }

    [Fact]
    public void Attackers_can_be_moved_up_and_down()
    {
        var shell = OpenComparison();
        shell.Find("[data-test=add-from-scenario]").Click();

        Assert.True(shell.FindAll("[data-test=move-attacker-up]")[0].HasAttribute("disabled"));
        Assert.True(shell.FindAll("[data-test=move-attacker-down]")[1].HasAttribute("disabled"));
        shell.FindAll("[data-test=move-attacker-down]")[0].Click();

        Assert.Equal(["Rubrics (2)", "Rubrics"], AttackerNames(shell));
        Assert.Contains("*", shell.Find("[data-test=file-name]").TextContent);
    }

    [Fact]
    public void The_name_is_the_card_title_and_is_edited_with_the_pen()
    {
        var shell = OpenComparison();
        var card = () => shell.Find("[data-test=comparison-attacker]");

        // Pas de champ « Nom » dans la carte : le nom est le titre
        Assert.Equal("Rubrics", card().QuerySelector("[data-test=attacker-name]")!.TextContent);
        Assert.DoesNotContain(card().QuerySelectorAll("label"), l => l.TextContent == "Nom de l'attaquant");

        shell.Find("[data-test=rename-attacker]").Click();
        shell.Find("input[data-test=attacker-name-input]").Input("Rubrics flammes");
        shell.Find("input[data-test=attacker-name-input]").KeyDown("Enter");

        Assert.Equal(["Rubrics flammes"], AttackerNames(shell));
        Assert.Equal("Rubrics flammes", card().QuerySelector("[data-test=attacker-name]")!.TextContent);
    }

    [Fact]
    public void Escape_cancels_a_rename()
    {
        var shell = OpenComparison();

        shell.Find("[data-test=rename-attacker]").Click();
        shell.Find("input[data-test=attacker-name-input]").Input("Autre");
        shell.Find("input[data-test=attacker-name-input]").KeyDown("Escape");

        Assert.Equal(["Rubrics"], AttackerNames(shell));
        Assert.Empty(shell.FindAll("input[data-test=attacker-name-input]"));
    }

    [Fact]
    public void The_scenario_screens_keep_the_name_field_in_the_card()
    {
        var shell = Render<AppShell>();
        Navigation.NavigateTo(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));

        Assert.Empty(shell.FindAll("[data-test=rename-attacker]"));
        Assert.Contains(shell.Find("[data-test=attacker-panel]").QuerySelectorAll("label"), l => l.TextContent == "Nom de l'attaquant");
    }

    private static AttackerUnit Lancer(string name) => new()
    {
        Name = name,
        Weapons = [new WeaponGroup { Weapon = new WeaponProfile { Name = "Lance", Type = WeaponType.Melee, Attacks = 3, Skill = 3, Strength = 6, Abilities = new WeaponAbilities { Lance = true } } }],
    };

    [Fact]
    public void Overwatch_is_a_common_switch_of_the_banner()
    {
        var shell = OpenComparison();

        Assert.Empty(shell.FindAll("[data-test=context]"));
        shell.Find("[data-test=banner] input[data-test=snap-shooting]").Change(true);

        Assert.True(Session.Comparison.Form!.SnapShooting);
    }

    [Fact]
    public void Each_card_shows_only_the_conditions_its_weapons_need()
    {
        Session.Comparison.Form = new ComparisonForm(Comparison.Of(Squad, [Attacker("Bolters", 10), Lancer("Cavalerie")]));
        var shell = OpenComparison();
        var cards = shell.FindAll("[data-test=comparison-attacker]");

        Assert.Null(cards[0].QuerySelector("[data-test=attacker-situation]"));
        Assert.NotNull(cards[1].QuerySelector("[data-test=charged]"));
        Assert.Null(cards[1].QuerySelector("[data-test=half-range]"));

        shell.FindAll("[data-test=comparison-attacker]")[1].QuerySelector("input[data-test=charged]")!.Change(true);

        Assert.Equal([false, true], Session.Comparison.Form.Entries.Select(e => e.Context.Charged));
    }

    [Fact]
    public void The_ranking_recalls_each_attacker_situation_and_the_overwatch()
    {
        var comparison = Comparison.Of(Squad, [Attacker("Bolters", 10), Lancer("Cavalerie")]);
        comparison = comparison with { Entries = [comparison.Entries[0], comparison.Entries[1] with { Context = new AttackContext { Charged = true } }] };
        var shell = OpenResults(Comparator.Run(comparison, 2_000, seed: 1));

        Assert.Equal("A chargé ce tour", shell.Find("[data-test=ranking-row-1] [data-test=situation]").TextContent);
        Assert.Empty(shell.FindAll("[data-test=ranking-row-0] [data-test=situation]"));
        Assert.Empty(shell.FindAll("[data-test=overwatch-chip]"));
    }

    [Fact]
    public void An_overwatch_comparison_is_marked_on_the_results()
    {
        var shell = OpenResults(Comparator.Run(Comparison.Of(Squad, [Attacker("A", 10), Attacker("B", 5)], snapShooting: true), 2_000, seed: 1));

        Assert.NotEmpty(shell.FindAll("[data-test=overwatch-chip]"));
    }

    // Armes à plusieurs profils

    [Fact]
    public void Only_the_comparator_offers_alternative_profiles()
    {
        var shell = OpenComparison();
        Assert.Single(shell.FindAll("[data-test=comparison-attacker] [data-test=add-profile]"));

        Navigation.NavigateTo(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        Assert.Empty(shell.FindAll("[data-test=add-profile]"));
    }

    [Fact]
    public void Adding_a_profile_makes_variants_and_a_single_attacker_can_run()
    {
        var shell = OpenComparison();

        shell.Find("[data-test=add-profile]").Click();

        var entry = Session.Comparison.Form!.Entries[0];
        var weapon = entry.Attacker.Weapons[0];
        var profile = Assert.Single(entry.ProfilesOf(weapon));
        Assert.Equal("Bolter (2)", profile.Name);
        Assert.Equal(2, shell.FindAll("[data-test=profile-tab]").Count);
        Assert.Contains("2 variantes", shell.Find("[data-test=attacker-title-note]").TextContent);
        Assert.StartsWith("2 variantes", shell.Find("[data-test=round-plan]").TextContent);
        Assert.False(shell.Find("[data-test=run]").HasAttribute("disabled"));
    }

    [Fact]
    public void The_card_edits_the_profile_chosen_in_its_bar()
    {
        var shell = OpenComparison();
        shell.Find("[data-test=add-profile]").Click();   // le nouveau profil est affiché

        shell.Find("input[data-test=attacks]").Change("20");

        var entry = Session.Comparison.Form!.Entries[0];
        var weapon = entry.Attacker.Weapons[0];
        Assert.Equal("20", entry.ProfilesOf(weapon)[0].Attacks);
        Assert.Equal("10", weapon.Attacks);

        shell.FindAll("[data-test=profile-tab]")[0].Click();
        Assert.Equal("10", shell.Find("input[data-test=attacks]").GetAttribute("value"));
    }

    [Fact]
    public void A_comparison_keeps_its_profiles_but_an_attacker_file_keeps_the_first_one()
    {
        var shell = OpenComparison();
        shell.Find("[data-test=add-profile]").Click();

        shell.Find("[data-test=save]").Click();
        shell.Find("[data-test=comparison-attacker] [data-test=save-attacker]").Click();

        var comparison = ComparisonJson.Deserialize(Files.Saved[0].Content);
        Assert.Equal("Bolter (2)", Assert.Single(Assert.Single(comparison.Entries[0].Alternatives).Profiles).Name);
        var attacker = ScenarioJson.ReadFile(Files.Saved[1].Content).Attacker!;
        Assert.Equal(["Bolter"], attacker.Weapons.Select(w => w.Weapon.Name));
    }

    [Fact]
    public void The_results_give_the_best_profile_of_each_weapon_and_save_the_concrete_variant()
    {
        var entry = new ComparisonEntry
        {
            Attacker = Attacker("Escouade", 3),
            Alternatives = [new WeaponAlternatives { Weapon = 0, Profiles = [new WeaponProfile { Name = "Bolter (rafale)", Attacks = 15, Skill = 3, Strength = 4 }] }],
        };
        var shell = OpenResults(Comparator.Run(new Comparison { Defender = Squad, Entries = [entry, new ComparisonEntry { Attacker = Attacker("Autre", 1) }] }, 2_000, seed: 1));

        Assert.Contains("Bolter → Bolter (rafale)", shell.Find("[data-test=best-profiles-0]").TextContent);
        Assert.Equal("Escouade · Bolter (rafale)", shell.Find("[data-test=ranking-row-1] [data-test=name]").TextContent);

        OpenRowMenu(shell, 1);
        ClickMenuItem(shell, "save-attacker");
        shell.WaitForAssertion(() => Assert.Single(Files.Saved));
        var saved = ScenarioJson.ReadFile(Files.Saved[0].Content).Attacker!;
        Assert.Equal(["Bolter (rafale)"], saved.Weapons.Select(w => w.Weapon.Name));
    }

    [Fact]
    public void The_round_plan_follows_the_number_of_attackers()
    {
        var shell = OpenComparison();
        for (int i = 0; i < 9; i++)
        {
            shell.Find("[data-test=add-from-scenario]").Click();
        }

        Assert.Contains("3 ronde(s)", shell.Find("[data-test=round-plan]").TextContent);
    }

    [Fact]
    public void The_comparison_is_saved_and_reopened()
    {
        var shell = OpenComparison();
        shell.Find("[data-test=add-from-scenario]").Click();

        shell.Find("[data-test=save]").Click();

        var (content, name) = Assert.Single(Files.Saved);
        Assert.Equal("comparaison.json", name);
        var saved = ComparisonJson.Deserialize(content);
        Assert.Equal(["Rubrics", "Rubrics (2)"], saved.Attackers.Select(a => a.Name));
        Assert.DoesNotContain("*", shell.Find("[data-test=file-name]").TextContent);

        Files.ToOpen = new OpenedFile("autre.json", ComparisonJson.Serialize(Comparison.Of(saved.Defender, [Attacker("A", 3), Attacker("B", 5)])));
        shell.Find("[data-test=open]").Click();

        Assert.Equal(["A", "B"], AttackerNames(shell));
        Assert.Contains("autre.json", shell.Find("[data-test=file-name]").TextContent);
    }

    [Fact]
    public void Opening_a_scenario_brings_its_defender_and_situation_and_adds_its_attacker()
    {
        var shell = OpenComparison();
        var other = new DefenderUnit { Name = "Terminators", Groups = [new ModelGroup { Name = "Terminators", Count = 5, Wounds = 3, Toughness = 5, Save = 2 }] };
        Files.ToOpen = FakeScenarioFileService.File("full sim.json",
            new Scenario { Attacker = Attacker("Plasma", 4), Defender = other, Context = new AttackContext { HalfRange = true } });

        shell.Find("[data-test=open]").Click();

        Assert.Equal(["Rubrics", "Plasma"], AttackerNames(shell));
        Assert.Equal("Terminators", Session.Comparison.Form!.Defender.Name);
        Assert.True(Session.Comparison.Form.Entries[1].Context.HalfRange);
        Assert.Contains("full sim.json", shell.Find("[data-test=message]").TextContent);
        Assert.False(shell.Find("[data-test=run]").HasAttribute("disabled"));
        // Fichier de comparaison inchangé, mais la comparaison est modifiée
        Assert.Contains("Nouvelle comparaison *", shell.Find("[data-test=file-name]").TextContent);
    }

    [Fact]
    public void Opening_an_attacker_file_adds_it_and_a_defender_file_replaces_the_defender()
    {
        var shell = OpenComparison();

        Files.ToOpen = FakeScenarioFileService.File("rubrics.json", Attacker("Rubrics", 5));
        shell.Find("[data-test=open]").Click();
        Assert.Equal(["Rubrics", "Rubrics (2)"], AttackerNames(shell));

        Files.ToOpen = FakeScenarioFileService.File("horde.json", Squad with { Name = "Horde" });
        shell.Find("[data-test=open]").Click();
        Assert.Equal("Horde", Session.Comparison.Form!.Defender.Name);
        Assert.Equal(["Rubrics", "Rubrics (2)"], AttackerNames(shell));
    }

    [Fact]
    public void Each_attacker_can_be_saved_from_its_card()
    {
        var shell = OpenComparison();

        shell.Find("[data-test=comparison-attacker] [data-test=save-attacker]").Click();

        var (content, name) = Assert.Single(Files.Saved);
        Assert.Equal("Rubrics.json", name);
        Assert.Equal("Rubrics", ScenarioJson.ReadFile(content).Attacker!.Name);
    }

    [Fact]
    public void A_finished_comparison_opens_the_results()
    {
        var shell = OpenComparison();
        shell.Find("[data-test=add-from-scenario]").Click();

        shell.Find("[data-test=run]").Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<ComparisonResultsPage>()));
        Assert.Equal(1, Simulations.ComparisonCalls);
        Assert.Equal(2, shell.FindAll("[data-test^=ranking-row-]").Count);
        Assert.Equal(2, shell.FindAll("[data-test^=podium-]").Count(e => e.GetAttribute("data-test") is "podium-1" or "podium-2"));
    }

    // Résultats

    [Fact]
    public void Without_result_the_results_screen_sends_back_to_the_entry()
    {
        var shell = Render<AppShell>();
        Navigation.NavigateTo(AppRoutes.ComparisonResults);

        shell.WaitForAssertion(() => Assert.NotNull(shell.Find("[data-test=no-result]")));
    }

    [Fact]
    public void The_podium_holds_three_winners_and_reports_a_fourth_tied_with_the_third()
    {
        var comparison = Comparison.Of(Squad, [Attacker("A", 20), Attacker("B", 12), Attacker("C", 6), Attacker("C bis", 6)]);
        var shell = OpenResults(Comparator.Run(comparison, 2_000, seed: 1));

        Assert.Contains("A", shell.Find("[data-test=podium-1]").TextContent);
        Assert.Contains("B", shell.Find("[data-test=podium-2]").TextContent);
        Assert.Contains("C", shell.Find("[data-test=podium-3]").TextContent);
        Assert.Contains("C bis", shell.Find("[data-test=podium-boundary-tied]").TextContent);
        Assert.Single(shell.FindAll("[data-test=ranking-row-3] [data-test=tied]"));
    }

    [Fact]
    public void The_distributions_show_the_three_winners_between_podium_and_ranking()
    {
        var comparison = Comparison.Of(Squad, [Attacker("A", 20), Attacker("B", 12), Attacker("C", 6), Attacker("D", 3)]);
        var shell = OpenResults(Comparator.Run(comparison, 2_000, seed: 1));

        var section = shell.Find("[data-test=comparison-distributions]");
        Assert.Equal(2, section.QuerySelectorAll("[data-test=comparison-chart]").Length);
        // Côte à côte sur un écran large : chacun dans sa demi-colonne de la grille
        Assert.NotNull(section.QuerySelector("[data-test=chart-kills] [data-test=comparison-chart]"));
        Assert.NotNull(section.QuerySelector("[data-test=chart-damage] [data-test=comparison-chart]"));
        Assert.Contains("mud-grid-item-lg-6", section.QuerySelector("[data-test=chart-kills]")!.ClassName);
        var legend = section.QuerySelectorAll("[data-test^=cmp-legend-]").Select(e => e.TextContent).ToList();
        Assert.Equal(6, legend.Count);   // 3 gagnants × 2 graphiques
        Assert.DoesNotContain(legend, l => l.Contains("D"));
        Assert.Equal(3, shell.FindAll("[data-test=podium-swatch]").Count);
        // Statistiques principales sur chaque tuile : figurines, PV (part de l'unité), unité détruite
        var stats = shell.Find("[data-test=podium-1] [data-test=tile-stats]").TextContent;
        Assert.Contains("Figurines tuées", stats);
        Assert.Contains("PV retirés", stats);
        Assert.Contains("de l'unité", stats);
        Assert.Contains("Unité détruite", stats);
        // Ordre de la page : podium, distributions, classement
        string markup = shell.Markup;
        Assert.True(markup.IndexOf("data-test=\"podium\"") < markup.IndexOf("comparison-distributions"));
        Assert.True(markup.IndexOf("comparison-distributions") < markup.IndexOf("ranking-table"));
    }

    [Fact]
    public void With_two_finalists_the_charts_have_two_series()
    {
        var shell = OpenResults(Comparator.Run(Comparison.Of(Squad, [Attacker("A", 20), Attacker("B", 5)]), 2_000, seed: 1));

        Assert.Equal(2, shell.FindAll("[data-test=comparison-distributions] [data-test=comparison-chart]")[0].QuerySelectorAll("[data-test^=cmp-box-]").Length);
    }

    [Fact]
    public void Eliminated_attackers_are_greyed_with_their_round()
    {
        var comparison = Comparison.Of(Squad, [Attacker("A", 20), Attacker("B", 16), Attacker("C", 12), Attacker("Faible", 1)]);
        var shell = OpenResults(Comparator.Run(comparison, 100_000, seed: 1));

        var row = shell.Find("[data-test=ranking-row-3]");
        Assert.Contains("wh-eliminated", row.ClassName);
        // Séparateur des milliers fr-FR : espace insécable étroite, ramenée à une espace ordinaire
        Assert.Contains("Éliminé, ronde 1 (10 000 simulations)", Regex.Replace(row.QuerySelector("[data-test=status]")!.TextContent, @"\s", " "));
        Assert.Equal("Finaliste", shell.Find("[data-test=ranking-row-0] [data-test=status]").TextContent);
        Assert.Contains("wh-top", shell.Find("[data-test=ranking-row-0] [data-test=score]").ClassName);
    }

    [Fact]
    public void The_ranking_can_be_sorted_by_column()
    {
        var comparison = Comparison.Of(Squad, [Attacker("Moyen", 8), Attacker("Fort", 20), Attacker("Faible", 3)]);
        var shell = OpenResults(Comparator.Run(comparison, 2_000, seed: 1));
        string[] Names() => shell.FindAll("[data-test^=ranking-row-] [data-test=name]").Select(e => e.TextContent).ToArray();

        Assert.Equal(["Fort", "Moyen", "Faible"], Names());

        shell.Find("[data-test=sort-name]").Click();
        Assert.Equal(["Faible", "Fort", "Moyen"], Names());

        shell.Find("[data-test=sort-name]").Click();
        Assert.Equal(["Moyen", "Fort", "Faible"], Names());

        shell.Find("[data-test=sort-score]").Click();
        Assert.Equal(["Fort", "Moyen", "Faible"], Names());
    }

    [Fact]
    public void A_row_can_save_its_attacker_or_its_scenario()
    {
        var comparison = Comparison.Of(Squad, [Attacker("Fort", 20), Attacker("Faible", 3)]);
        var shell = OpenResults(Comparator.Run(comparison, 2_000, seed: 1));

        OpenRowMenu(shell, 1);
        ClickMenuItem(shell, "save-attacker");
        shell.WaitForAssertion(() => Assert.Single(Files.Saved));
        OpenRowMenu(shell, 1);
        ClickMenuItem(shell, "save-scenario");
        shell.WaitForAssertion(() => Assert.Equal(2, Files.Saved.Count));

        Assert.Equal(2, Files.Saved.Count);
        Assert.Equal("Faible.json", Files.Saved[0].SuggestedFileName);
        Assert.Equal("Faible", ScenarioJson.ReadFile(Files.Saved[0].Content).Attacker!.Name);
        Assert.Equal("Faible contre Gardes.json", Files.Saved[1].SuggestedFileName);
        Assert.Equal(ScenarioFileKind.Scenario, ScenarioJson.ReadFile(Files.Saved[1].Content).Kind);
    }

    [Fact]
    public void A_row_can_be_analysed_in_the_full_sim()
    {
        var comparison = Comparison.Of(Squad, [Attacker("Fort", 20), Attacker("Faible", 3)]);
        var shell = OpenResults(Comparator.Run(comparison, 2_000, seed: 1));

        OpenRowMenu(shell, 1);
        ClickMenuItem(shell, "analyse");

        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        Assert.Equal("Faible", Session.Form.Attacker.Name);
        Assert.Equal("Gardes", Session.Form.Defender.Name);
        Assert.Null(Session.FileName);
    }

    /// <summary>Ouvre le menu d'une ligne (bouton recherché à chaque essai : la page se redessine après une action).</summary>
    private static void OpenRowMenu(IRenderedComponent<AppShell> shell, int index) =>
        shell.ClickUntil($"[data-test=ranking-row-{index}] [data-test=row-actions] button", () => shell.FindAll("[data-test=analyse]").Count > 0);

    /// <summary>
    /// Clique un élément du menu ouvert : recherché et cliqué d'un seul tenant (le menu, rendu de façon asynchrone par
    /// MudBlazor, peut se redessiner entre la recherche et le clic).
    /// </summary>
    private static void ClickMenuItem(IRenderedComponent<AppShell> shell, string item)
    {
        shell.WaitForElement($"[data-test={item}]");
        shell.InvokeAsync(() => shell.Find($"[data-test={item}]").Click());
    }
}
