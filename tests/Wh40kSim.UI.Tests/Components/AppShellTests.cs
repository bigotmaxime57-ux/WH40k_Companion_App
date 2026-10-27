using Bunit;
using Wh40kSim.UI.Components;
using Wh40kSim.UI.Theme;

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

        shell.ClickUntil("[data-test=toggle-theme]", () => !shell.Instance.IsDarkMode);

        Assert.False(shell.Instance.IsDarkMode);
    }

    [Theory]
    [InlineData(DarkStyle.Grimdark, true)]
    [InlineData(DarkStyle.Steel, false)]
    public void Grimdark_effects_apply_only_to_the_grimdark_dark_theme(DarkStyle style, bool grimdark)
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode").SetResult(true);
        var shell = Render<AppShell>(p => p.Add(c => c.DarkStyle, style));
        shell.WaitForAssertion(() => Assert.True(shell.Instance.IsDarkMode));

        Assert.Equal(grimdark, shell.FindAll(".wh-grimdark").Count > 0);
        shell.ClickUntil("[data-test=toggle-theme]", () => !shell.Instance.IsDarkMode);
        Assert.Empty(shell.FindAll(".wh-grimdark")); // thème clair : jamais
    }

    [Fact]
    public void Each_dark_style_has_its_own_theme_and_the_light_palette_is_shared()
    {
        Assert.NotSame(Wh40kTheme.Grimdark, Wh40kTheme.Steel);
        Assert.Same(Wh40kTheme.Steel, Wh40kTheme.For(DarkStyle.Steel));
        Assert.Equal(Wh40kTheme.Grimdark.PaletteLight.Background, Wh40kTheme.Steel.PaletteLight.Background);
        Assert.NotEqual(Wh40kTheme.Grimdark.PaletteDark.Background, Wh40kTheme.Steel.PaletteDark.Background);
    }
}
