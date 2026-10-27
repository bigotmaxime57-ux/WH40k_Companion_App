using Bunit;
using Wh40kSim.UI.Components;

namespace Wh40kSim.UI.Tests.Components;

public class AppShellTests() : ComponentTestContext(renderPopoverProvider: false)
{
    [Fact]
    public void Shell_shows_the_title_and_the_simulator()
    {
        var shell = Render<AppShell>();

        Assert.Contains("Wh40kSim – Simulateur de dégâts", shell.Markup);
        Assert.NotNull(shell.FindComponent<SimulatorPage>());
    }

    [Fact]
    public void Theme_follows_the_system_then_can_be_toggled()
    {
        // Préférence du système lue par MudBlazor via JavaScript : ici, sombre
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode").SetResult(true);
        var shell = Render<AppShell>();
        shell.WaitForAssertion(() => Assert.True(shell.Instance.IsDarkMode));

        shell.Find("[data-test=toggle-theme]").Click();

        Assert.False(shell.Instance.IsDarkMode);
    }
}
