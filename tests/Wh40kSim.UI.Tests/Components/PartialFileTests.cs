using Bunit;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.UI.Components;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>Fichiers partiels dans le Quick sim : ouvrir et enregistrer l'attaquant ou le défenseur seul.</summary>
public class PartialFileTests : ComponentTestContext
{
    private static AttackerUnit Attacker() => new()
    {
        Name = "Fusiliers",
        Boosts = new AttackerBoosts { HitRoll = 1 },
        Weapons = [new WeaponGroup { ModelCount = 7, Weapon = new WeaponProfile { Name = "Fusil", Skill = 4 } }],
    };

    private static DefenderUnit Defender() => new()
    {
        Name = "Terminators",
        Boosts = new DefenderBoosts { InCover = true },
        Groups = [new ModelGroup { Name = "Terminators", Count = 5, Toughness = 5, Wounds = 3, Save = 2, InvulnerableSave = 4 }],
    };

    private static Scenario FullScenario() => new() { Attacker = Attacker(), Defender = Defender() };

    private static bool IsDisabled(IRenderedComponent<SimulatorPage> page, string testId) =>
        page.Find($"[data-test={testId}]").HasAttribute("disabled");

    private string Message(IRenderedComponent<SimulatorPage> page) => page.Find("[data-test=message]").TextContent;

    [Fact]
    public void Opening_an_attacker_file_replaces_only_the_attacker()
    {
        var page = Render<SimulatorPage>();
        string defenderName = page.Instance.Form.Defender.Name;
        Files.ToOpen = FakeScenarioFileService.File("fusiliers.json", Attacker());

        page.Find("[data-test=open-attacker]").Click();

        Assert.Equal("Fusiliers", page.Instance.Form.Attacker.Name);
        Assert.Equal(7, page.Instance.Form.Attacker.Weapons[0].ModelCount);
        Assert.Equal(1, page.Instance.Form.Attacker.Boosts.HitRoll);
        Assert.Equal(defenderName, page.Instance.Form.Defender.Name);
        // Le scénario courant est modifié et garde son nom
        Assert.Equal("Nouveau scénario *", page.Find("[data-test=file-name]").TextContent);
        Assert.Contains("Attaquant chargé depuis fusiliers.json", Message(page));
    }

    [Fact]
    public void The_attacker_can_be_taken_from_a_full_scenario()
    {
        var page = Render<SimulatorPage>();
        string defenderName = page.Instance.Form.Defender.Name;
        Files.ToOpen = FakeScenarioFileService.File("complet.json", FullScenario());

        page.Find("[data-test=open-attacker]").Click();

        Assert.Equal("Fusiliers", page.Instance.Form.Attacker.Name);
        Assert.Equal(defenderName, page.Instance.Form.Defender.Name);
    }

    [Fact]
    public void Opening_a_defender_file_replaces_only_the_defender()
    {
        var page = Render<SimulatorPage>();
        string attackerName = page.Instance.Form.Attacker.Name;
        Files.ToOpen = FakeScenarioFileService.File("terminators.json", Defender());

        page.Find("[data-test=open-defender]").Click();

        Assert.Equal("Terminators", page.Instance.Form.Defender.Name);
        Assert.True(page.Instance.Form.Defender.Boosts.InCover);
        Assert.Equal(attackerName, page.Instance.Form.Attacker.Name);
        Assert.Contains("Défenseur chargé depuis terminators.json", Message(page));
    }

    [Fact]
    public void A_file_without_the_requested_part_is_reported_and_changes_nothing()
    {
        var page = Render<SimulatorPage>();
        var form = page.Instance.Form.Attacker;
        Files.ToOpen = FakeScenarioFileService.File("terminators.json", Defender());

        page.Find("[data-test=open-attacker]").Click();

        Assert.Same(form, page.Instance.Form.Attacker);
        Assert.False(page.Instance.IsDirty);
        Assert.Contains("terminators.json ne contient pas d'attaquant", Message(page));
    }

    [Fact]
    public void The_main_open_button_loads_a_partial_file_into_the_current_scenario()
    {
        Files.ToOpen = FakeScenarioFileService.File("complet.json", FullScenario());
        var page = Render<SimulatorPage>();
        page.Find("[data-test=open]").Click();
        Files.ToOpen = FakeScenarioFileService.File("autre-cible.json", Defender() with { Name = "Gardes" });

        page.Find("[data-test=open]").Click();

        Assert.Equal("Gardes", page.Instance.Form.Defender.Name);
        Assert.Equal("Fusiliers", page.Instance.Form.Attacker.Name);
        Assert.Equal("complet.json *", page.Find("[data-test=file-name]").TextContent);
    }

    [Fact]
    public void Replacing_a_part_asks_for_confirmation_when_the_entry_is_modified()
    {
        var page = Render<SimulatorPage>();
        page.Find("input[data-test=attacks]").Input("3");
        Files.ToOpen = FakeScenarioFileService.File("fusiliers.json", Attacker());

        page.Find("[data-test=open-attacker]").Click();
        DialogButton("Annuler").Click();

        Assert.Equal("3", page.Instance.Form.Attacker.Weapons[0].Attacks);
    }

    [Fact]
    public void Saving_the_attacker_writes_an_attacker_file_named_after_it()
    {
        Files.ToOpen = FakeScenarioFileService.File("complet.json", FullScenario() with { Attacker = Attacker() with { Name = "Fusiliers : tir/2" } });
        var page = Render<SimulatorPage>();
        page.Find("[data-test=open]").Click();

        page.Find("[data-test=save-attacker]").Click();

        var (content, fileName) = Assert.Single(Files.Saved);
        var file = ScenarioJson.ReadFile(content);
        Assert.Equal(ScenarioFileKind.Attacker, file.Kind);
        Assert.Equal(1, file.Attacker!.Boosts.HitRoll);
        Assert.Equal("Fusiliers  tir2.json", fileName); // caractères interdits dans un nom de fichier retirés
        Assert.Contains("Attaquant enregistré", Message(page));
        // Le scénario, lui, n'est pas enregistré
        Assert.Equal("complet.json", page.Find("[data-test=file-name]").TextContent);
    }

    [Fact]
    public void Saving_the_defender_writes_a_defender_file()
    {
        var page = Render<SimulatorPage>();
        page.Instance.Form.Defender.Name = "";

        page.Find("[data-test=save-defender]").Click();

        var (content, fileName) = Assert.Single(Files.Saved);
        Assert.Equal(ScenarioFileKind.Defender, ScenarioJson.ReadFile(content).Kind);
        Assert.Equal("defenseur.json", fileName);
    }

    [Fact]
    public void Invalid_dice_prevent_saving_the_attacker_but_not_the_defender()
    {
        var page = Render<SimulatorPage>();

        page.Find("input[data-test=attacks]").Input("D");

        Assert.True(IsDisabled(page, "save-attacker"));
        Assert.False(IsDisabled(page, "save-defender"));
    }

    [Fact]
    public void Part_buttons_are_disabled_while_a_simulation_runs()
    {
        Simulations.Gate = new TaskCompletionSource();
        var page = Render<SimulatorPage>();

        page.Find("[data-test=run]").Click();

        Assert.All(["open-attacker", "save-attacker", "open-defender", "save-defender"], id => Assert.True(IsDisabled(page, id)));
        Simulations.Gate.SetResult();
    }

    private AngleSharp.Dom.IElement DialogButton(string text)
    {
        AngleSharp.Dom.IElement? button = null;
        Dialogs!.WaitForAssertion(() => button = Assert.Single(Dialogs!.FindAll("button"), b => b.TextContent.Contains(text)));
        return button!;
    }
}
