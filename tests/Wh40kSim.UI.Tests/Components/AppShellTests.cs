using Bunit;
using Wh40kSim.UI.Components;
using Wh40kSim.UI.Theme;

namespace Wh40kSim.UI.Tests.Components;

public class AppShellTests() : ComponentTestContext(renderPopoverProvider: false)
{
    [Fact]
    public void Shell_shows_the_title_and_opens_on_the_home_screen()
    {
        var shell = Render<AppShell>();

        Assert.Contains("Wh40kSim – Simulateur de dégâts", shell.Markup);
        Assert.NotNull(shell.FindComponent<HomePage>());
        Assert.Empty(shell.FindComponents<SimulatorPage>());
    }

    [Theory]
    [InlineData(true, ThemeChoice.Grimdark)]
    [InlineData(false, ThemeChoice.Parchment)]
    public void Without_a_saved_choice_the_theme_follows_the_system(bool systemDark, ThemeChoice expected)
    {
        // Préférence du système lue par MudBlazor via JavaScript
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode").SetResult(systemDark);
        var shell = Render<AppShell>();

        shell.WaitForAssertion(() => Assert.Equal(expected, shell.Instance.Theme));
        Assert.Null(Preferences.Theme); // rien n'est mémorisé tant que l'utilisateur n'a pas choisi
    }

    [Fact]
    public void The_saved_choice_wins_over_the_system()
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode").SetResult(false);
        Preferences.Theme = ThemeChoice.Neon;
        var shell = Render<AppShell>();

        shell.WaitForAssertion(() => Assert.Equal(ThemeChoice.Neon, shell.Instance.Theme));
        Assert.NotEmpty(shell.FindAll(".wh-neon"));
    }

    [Theory]
    [InlineData(ThemeChoice.Parchment)]
    [InlineData(ThemeChoice.Neon)]
    public void Theme_given_by_the_host_applies_from_the_first_render_without_asking_anyone(ThemeChoice theme)
    {
        // Le navigateur et les préférences diraient autre chose : ils ne doivent pas être consultés
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode").SetResult(true);
        Preferences.Theme = ThemeChoice.Steel;
        var shell = Render<AppShell>(p => p.Add(c => c.InitialTheme, theme));

        Assert.Equal(theme, shell.Instance.Theme);
        Assert.Equal(Wh40kTheme.IsDark(theme), shell.FindAll(".wh-dark").Count > 0);
        Assert.Empty(JSInterop.Invocations["mudThemeProvider.isDarkMode"]);
    }

    [Fact]
    public void Choosing_a_theme_in_the_menu_applies_and_saves_it()
    {
        var shell = Render<AppShell>(p => p.Add(c => c.InitialTheme, ThemeChoice.Grimdark));

        shell.ClickUntil("[data-test=theme-menu] button", () => shell.FindAll("[data-test=theme-Steel]").Count > 0);
        shell.ClickUntil("[data-test=theme-Steel]", () => shell.Instance.Theme == ThemeChoice.Steel);

        Assert.Equal(ThemeChoice.Steel, Preferences.Theme);
        Assert.NotEmpty(shell.FindAll(".wh-dark"));
        Assert.Empty(shell.FindAll(".wh-grimdark"));
    }

    [Theory]
    [InlineData(ThemeChoice.Parchment, "wh-shell")]
    [InlineData(ThemeChoice.Grimdark, "wh-shell wh-dark wh-grimdark")]
    [InlineData(ThemeChoice.Steel, "wh-shell wh-dark")]
    [InlineData(ThemeChoice.Neon, "wh-shell wh-dark wh-neon")]
    public void Each_theme_sets_its_own_style_classes(ThemeChoice theme, string classes)
    {
        var shell = Render<AppShell>(p => p.Add(c => c.InitialTheme, theme));

        var layout = shell.Find(".wh-shell");
        Assert.Equal(classes.Split(' ').Order(), layout.ClassList.Where(c => c.StartsWith("wh-")).Order());
    }

    [Fact]
    public void Each_dark_style_has_its_own_theme_and_the_light_palette_is_shared()
    {
        MudBlazor.MudTheme[] dark = [.. new[] { ThemeChoice.Grimdark, ThemeChoice.Steel, ThemeChoice.Neon }.Select(Wh40kTheme.For)];

        Assert.Equal(3, dark.Distinct().Count());
        Assert.Equal(3, dark.Select(t => t.PaletteDark.Background).Distinct().Count());
        Assert.Single(dark.Select(t => t.PaletteLight.Background).Distinct());
        Assert.False(Wh40kTheme.IsDark(ThemeChoice.Parchment));
    }
}
