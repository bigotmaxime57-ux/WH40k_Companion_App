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
