using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Wh40kSim.UI.Components;
using Wh40kSim.UI.Services;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>Full sim : écran de saisie, passage aux résultats, résultat séparé de celui du Quick sim.</summary>
public class FullSimTests() : ComponentTestContext(renderPopoverProvider: false)
{
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private ScenarioSession Session => Services.GetRequiredService<ScenarioSession>();

    private IRenderedComponent<AppShell> Open(string route)
    {
        var shell = Render<AppShell>();
        Navigation.NavigateTo(route);
        return shell;
    }

    [Fact]
    public void Home_screen_offers_the_full_sim()
    {
        var shell = Render<AppShell>();

        Assert.Equal(AppRoutes.FullSim, shell.Find("[data-test=tile-full-sim]").GetAttribute("href"));
        Assert.Contains("Full sim", shell.Find("[data-test=tile-full-sim]").TextContent);
    }

    [Fact]
    public void Home_screen_shows_the_version_given_by_the_host()
    {
        var shell = Render<AppShell>(p => p.Add(c => c.Version, "0.3-apercu.1"));

        Assert.Equal("Version 0.3-apercu.1", shell.Find("[data-test=app-version]").TextContent);
    }

    [Fact]
    public void Home_screen_shows_no_version_when_the_host_gives_none()
    {
        Assert.Empty(Render<AppShell>().FindAll("[data-test=app-version]"));
    }

    [Fact]
    public void Entry_screen_has_the_banner_and_the_three_panels_but_no_results()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));

        var banner = shell.Find("[data-test=banner]");
        Assert.NotNull(banner.QuerySelector("[data-test=open]"));
        Assert.NotNull(banner.QuerySelector("[data-test=run]"));
        Assert.NotNull(shell.FindComponent<AttackerPanel>());
        Assert.NotNull(shell.FindComponent<DefenderPanel>());
        Assert.NotNull(shell.FindComponent<ContextPanel>());
        Assert.Empty(shell.FindComponents<ResultsView>());
        Assert.Equal("Full sim", shell.Find("[data-test=screen-name]").TextContent);
    }

    [Fact]
    public void Invalid_scenario_disables_run()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));

        shell.Find("[data-test=remove-group]").Click();

        Assert.True(shell.Find("[data-test=run]").HasAttribute("disabled"));
    }

    [Fact]
    public void A_finished_simulation_opens_the_results_with_a_histogram()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));

        shell.Find("[data-test=run]").Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        Assert.EndsWith("/" + AppRoutes.FullSimResults, Navigation.Uri);
        Assert.Equal(2, shell.FindAll("[data-test=histogram]").Count);
        Assert.NotNull(shell.Find("[data-test=tile-kills]"));
        Assert.NotNull(shell.Find("[data-test=tile-damage]"));
        Assert.NotNull(shell.Find("[data-test=wipe]"));
        Assert.Contains("Attaquant contre Défenseur", shell.Find("[data-test=results-header]").TextContent);
    }

    [Fact]
    public void A_compact_back_button_waits_in_the_sticky_tab_bar()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));

        // Bouton texte en haut, hors de la barre ; flèche ronde dans la barre (montrée par le CSS une fois collée)
        Assert.Empty(shell.FindAll(".mud-tabs-tabbar [data-test=back-to-entry]"));
        Assert.NotNull(shell.Find(".wh-results-tabs > .mud-tabs-tabbar .wh-tabs-back [data-test=back-to-entry-compact]"));
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "watchSticky");

        shell.Find("[data-test=back-to-entry-compact]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
    }

    [Fact]
    public void The_results_reopen_on_the_last_tab_until_a_new_simulation()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        shell.FindAll(".mud-tab").Single(tab => tab.TextContent.Contains("Groupes")).Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.Find("[data-test=groups-report]")));

        // Retour à la saisie puis aux résultats : toujours l'onglet Groupes
        shell.Find("[data-test=back-to-entry]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=show-results]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.Find("[data-test=groups-report]")));
        Assert.Equal(1, Session.FullSim.ResultsTab);

        // Nouvelle simulation : retour à l'onglet Distributions
        shell.Find("[data-test=back-to-entry]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        shell.WaitForAssertion(() => Assert.Empty(shell.FindAll("[data-test=groups-report]")));
        Assert.Equal(0, Session.FullSim.ResultsTab);
    }

    [Fact]
    public void On_a_phone_the_tab_list_opens_from_a_button_named_after_the_current_tab()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));

        // Bouton (montré par le CSS sur téléphone seulement) au nom de l'onglet affiché, qui ouvre la liste des onglets
        Assert.Contains("Distributions", shell.Find(".wh-tabs-phone-menu button").TextContent);
        shell.ClickUntil(".wh-tabs-phone-menu button", () => shell.FindAll("[data-test=tabs-phone-2]").Count > 0);
        shell.Find("[data-test=tabs-phone-2]").Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.Find("[data-test=weapons-report]")));
        Assert.Equal(2, Session.FullSim.ResultsTab);
        Assert.Contains("Armes", shell.Find(".wh-tabs-phone-menu button").TextContent);
    }

    [Fact]
    public void Results_use_the_theme_colour_rather_than_a_side_colour()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));

        Assert.NotNull(shell.Find(".wh-report [data-test=histogram]"));
        Assert.Empty(shell.FindAll(".wh-report .wh-attacker, .wh-report .wh-defender"));
    }

    [Fact]
    public void Results_have_a_groups_tab()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));

        shell.FindAll(".mud-tab").Single(tab => tab.TextContent.Contains("Groupes")).Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.Find("[data-test=groups-report]")));
        Assert.NotEmpty(shell.FindAll("[data-test^=group-losses-]"));
    }

    [Fact]
    public void Results_have_a_weapons_tab()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));

        shell.FindAll(".mud-tab").Single(tab => tab.TextContent.Contains("Armes")).Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.Find("[data-test=weapons-report]")));
        Assert.Single(shell.FindAll("[data-test^=weapon-pool-]"));   // une arme dans le scénario par défaut
    }

    [Fact]
    public void Full_sim_result_and_settings_are_separate_from_the_quick_sim()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=preset-10000]").Click();

        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));

        Assert.NotNull(Session.FullSim.Result);
        Assert.Null(Session.QuickSim.Result);
        Assert.Equal(10_000, Session.FullSim.Iterations);
        Assert.Equal(1_000_000, Session.QuickSim.Iterations);
        Assert.Equal(10_000, Simulations.LastIterations);

        Navigation.NavigateTo(AppRoutes.QuickSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<SimulatorPage>()));
        Assert.Empty(shell.FindAll("[data-test=results]"));
    }

    [Fact]
    public void A_cancelled_simulation_stays_on_the_entry_screen()
    {
        Simulations.Gate = new TaskCompletionSource();
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));

        shell.Find("[data-test=run]").Click();
        shell.WaitForElement("[data-test=cancel]");
        shell.ClickUntil("[data-test=cancel]", () => shell.FindAll("[data-test=message]").Count > 0);
        Assert.NotNull(shell.FindComponent<FullSimPage>());
        Assert.Null(Session.FullSim.Result);
    }

    [Fact]
    public void Results_screen_without_a_result_points_back_to_the_entry()
    {
        var shell = Open(AppRoutes.FullSimResults);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));

        Assert.NotNull(shell.Find("[data-test=no-result]"));
        Assert.Empty(shell.FindAll("[data-test=histogram]"));

        shell.Find("[data-test=back-to-entry]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
    }

    /// <summary>Lance une simulation puis revient à la saisie (le bouton « Voir les résultats » est alors affiché).</summary>
    private IRenderedComponent<AppShell> SimulateAndComeBack()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        Navigation.NavigateTo(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        return shell;
    }

    [Fact]
    public void Unchanged_scenario_shows_the_results_without_warning()
    {
        var shell = SimulateAndComeBack();

        shell.Find("[data-test=show-results]").Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        Assert.Empty(shell.FindAll("[data-test=stale-show-old]"));
    }

    [Fact]
    public void Changed_scenario_warns_and_can_show_the_old_results()
    {
        var shell = SimulateAndComeBack();
        var old = Session.FullSim.Result;
        shell.Find("[data-test=add-weapon]").Click();

        shell.Find("[data-test=show-results]").Click();
        shell.WaitForElement("[data-test=stale-show-old]").Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        Assert.Same(old, Session.FullSim.Result);
        Assert.Equal(1, Simulations.Calls);
        Assert.NotNull(shell.Find("[data-test=stale-results-banner]"));   // rappel sur l'écran des résultats
    }

    [Fact]
    public void Cancel_closes_the_warning_and_stays_on_the_entry_screen()
    {
        var shell = SimulateAndComeBack();
        shell.Find("[data-test=add-weapon]").Click();

        shell.Find("[data-test=show-results]").Click();
        shell.WaitForElement("[data-test=stale-cancel]").Click();

        shell.WaitForAssertion(() => Assert.Empty(shell.FindAll("[data-test=stale-cancel]")));
        Assert.NotNull(shell.FindComponent<FullSimPage>());
        Assert.Equal(1, Simulations.Calls);
        Assert.Null(Session.RememberedStaleResultsChoice);
        Assert.True(StaleResultsDialog.Options.CloseOnEscapeKey);   // Échap fait comme Annuler
    }

    [Fact]
    public void Changed_scenario_warns_and_can_rerun_the_simulation()
    {
        var shell = SimulateAndComeBack();
        shell.Find("[data-test=add-weapon]").Click();

        shell.Find("[data-test=show-results]").Click();
        shell.WaitForElement("[data-test=stale-rerun]").Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        Assert.Equal(2, Simulations.Calls);
        Assert.Equal(2, Session.FullSim.Result!.Scenario.Attacker.Weapons.Count);
        Assert.Empty(shell.FindAll("[data-test=stale-results-banner]"));
    }

    [Fact]
    public void Rerun_is_disabled_when_the_changed_scenario_is_invalid()
    {
        var shell = SimulateAndComeBack();
        shell.Find("[data-test=remove-group]").Click();

        shell.Find("[data-test=show-results]").Click();

        Assert.True(shell.WaitForElement("[data-test=stale-rerun]").HasAttribute("disabled"));
        Assert.NotNull(shell.Find("[data-test=stale-cannot-rerun]"));
    }

    /// <summary>Modifie le scénario, demande les résultats et répond à l'avertissement en retenant le choix.</summary>
    private void AnswerAndRemember(IRenderedComponent<AppShell> shell, string button)
    {
        shell.Find("[data-test=add-weapon]").Click();
        shell.Find("[data-test=show-results]").Click();
        shell.WaitForElement("input[data-test=stale-remember]").Change(true);
        shell.Find($"[data-test={button}]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        Navigation.NavigateTo(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
    }

    [Fact]
    public void A_remembered_show_old_choice_is_applied_without_asking()
    {
        var shell = SimulateAndComeBack();
        AnswerAndRemember(shell, "stale-show-old");
        Assert.Equal(StaleResultsChoice.ShowOld, Session.RememberedStaleResultsChoice);

        shell.Find("[data-test=add-weapon]").Click();
        shell.Find("[data-test=show-results]").Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        Assert.Empty(shell.FindAll("[data-test=stale-show-old]"));
        Assert.Equal(1, Simulations.Calls);
    }

    [Fact]
    public void A_remembered_rerun_choice_is_applied_without_asking()
    {
        var shell = SimulateAndComeBack();
        AnswerAndRemember(shell, "stale-rerun");
        Assert.Equal(StaleResultsChoice.Rerun, Session.RememberedStaleResultsChoice);

        shell.Find("[data-test=add-weapon]").Click();
        shell.Find("[data-test=show-results]").Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        Assert.Empty(shell.FindAll("[data-test=stale-rerun]"));
        Assert.Equal(3, Simulations.Calls);
        Assert.Equal(3, Session.FullSim.Result!.Scenario.Attacker.Weapons.Count);
    }

    [Fact]
    public void A_remembered_rerun_asks_again_when_the_scenario_is_invalid()
    {
        var shell = SimulateAndComeBack();
        AnswerAndRemember(shell, "stale-rerun");

        shell.Find("[data-test=remove-group]").Click();
        shell.Find("[data-test=show-results]").Click();

        Assert.True(shell.WaitForElement("[data-test=stale-rerun]").HasAttribute("disabled"));
    }

    [Fact]
    public void Loading_another_scenario_clears_both_results()
    {
        var shell = Open(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));
        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimResultsPage>()));
        Navigation.NavigateTo(AppRoutes.FullSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<FullSimPage>()));

        shell.Find("[data-test=new]").Click();

        Assert.Null(Session.FullSim.Result);
        Assert.Empty(shell.FindAll("[data-test=show-results]"));
    }
}
