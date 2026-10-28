using Bunit;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.UI.Components;
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
