using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Wh40kSim.UI.Components;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>Accueil, passage d'un écran à l'autre et scénario courant conservé entre les écrans.</summary>
public class NavigationTests() : ComponentTestContext(renderPopoverProvider: false)
{
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private IRenderedComponent<AppShell> OpenQuickSim()
    {
        var shell = Render<AppShell>();
        Navigation.NavigateTo(AppRoutes.QuickSim);
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<SimulatorPage>()));
        return shell;
    }

    [Theory]
    [InlineData(typeof(HomePage), AppRoutes.Home)]
    [InlineData(typeof(SimulatorPage), AppRoutes.QuickSim)]
    public void Routes_match_the_page_directives(Type page, string route)
    {
        var template = Assert.Single(page.GetCustomAttributes<RouteAttribute>()).Template;

        Assert.Equal("/" + route, template);
    }

    [Fact]
    public void Home_screen_offers_the_quick_sim_and_no_way_back()
    {
        var shell = Render<AppShell>();

        Assert.Equal(AppRoutes.QuickSim, shell.Find("[data-test=tile-quick-sim]").GetAttribute("href"));
        Assert.Contains("Quick sim", shell.Find("[data-test=tile-quick-sim]").TextContent);
        Assert.Empty(shell.FindAll("[data-test=home]"));
        Assert.Empty(shell.FindAll("[data-test=screen-name]"));
    }

    [Fact]
    public void Quick_sim_shows_its_name_and_a_home_button()
    {
        var shell = OpenQuickSim();

        Assert.Equal("Quick sim", shell.Find("[data-test=screen-name]").TextContent);
        Assert.False(shell.Find("[data-test=home]").HasAttribute("disabled"));
    }

    [Fact]
    public void Home_button_returns_to_the_home_screen()
    {
        var shell = OpenQuickSim();

        shell.Find("[data-test=home]").Click();

        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<HomePage>()));
        Assert.Empty(shell.FindComponents<SimulatorPage>());
    }

    [Fact]
    public void Entry_and_result_are_kept_after_a_trip_home()
    {
        var shell = OpenQuickSim();
        shell.Find("input[data-test=attacks]").Input("D6+1");
        shell.Find("[data-test=run]").Click();
        shell.WaitForElement("[data-test=results]");

        shell.Find("[data-test=home]").Click();
        shell.WaitForAssertion(() => Assert.NotNull(shell.FindComponent<HomePage>()));
        Navigation.NavigateTo(AppRoutes.QuickSim);

        shell.WaitForElement("[data-test=results]");
        Assert.Equal("D6+1", shell.Find("input[data-test=attacks]").GetAttribute("value"));
        Assert.EndsWith("*", shell.Find("[data-test=file-name]").TextContent);
        Assert.Equal(1, Simulations.Calls);
    }

    [Fact]
    public void Home_button_is_disabled_while_a_simulation_runs()
    {
        Simulations.Gate = new TaskCompletionSource();
        var shell = OpenQuickSim();

        shell.Find("[data-test=run]").Click();
        shell.WaitForAssertion(() => Assert.True(shell.Find("[data-test=home]").HasAttribute("disabled")));

        Simulations.Gate.SetResult();
        shell.WaitForElement("[data-test=results]");
        shell.WaitForAssertion(() => Assert.False(shell.Find("[data-test=home]").HasAttribute("disabled")));
    }
}
