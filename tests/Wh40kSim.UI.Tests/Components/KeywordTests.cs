using Bunit;
using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;
using Wh40kSim.UI.Components;
using Wh40kSim.UI.Forms;
using Wh40kSim.UI.Keywords;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>Sélecteur de mots-clés (panneau latéral) et étiquettes sur la carte d'une arme.</summary>
public class KeywordTests : ComponentTestContext
{
    private IRenderedComponent<KeywordPicker<WeaponAbilitiesForm>> RenderPicker(WeaponAbilitiesForm target, Action? changed = null) =>
        Render<KeywordPicker<WeaponAbilitiesForm>>(p => p
            .Add(c => c.Target, target)
            .Add(c => c.Definitions, WeaponKeywords.All)
            .Add(c => c.Changed, () => changed?.Invoke()));

    private static int Rows(IRenderedComponent<KeywordPicker<WeaponAbilitiesForm>> picker) => picker.FindAll("[data-test=keyword-row]").Count;

    [Fact]
    public void Every_weapon_keyword_is_listed_by_default()
    {
        var picker = RenderPicker(new WeaponAbilitiesForm());

        Assert.Equal(WeaponKeywords.All.Count, Rows(picker));
    }

    [Fact]
    public void Search_filters_on_name_and_rule_text()
    {
        var picker = RenderPicker(new WeaponAbilitiesForm());

        picker.Find("input[data-test=keyword-search]").Input("sus");
        Assert.Equal(1, Rows(picker));

        picker.Find("input[data-test=keyword-search]").Input("demi-portée");
        Assert.Equal(2, Rows(picker)); // Rapid Fire et Melta

        picker.Find("input[data-test=keyword-search]").Input("zzz");
        Assert.NotNull(picker.Find("[data-test=keyword-none]"));
    }

    [Fact]
    public void Enter_in_the_search_activates_the_first_result()
    {
        var target = new WeaponAbilitiesForm();
        var picker = RenderPicker(target);

        var search = picker.Find("input[data-test=keyword-search]");
        search.Input("lethal");
        picker.Find("input[data-test=keyword-search]").KeyDown("Enter");

        Assert.True(target.LethalHits);
    }

    [Fact]
    public void Category_filter_keeps_only_its_keywords()
    {
        var picker = RenderPicker(new WeaponAbilitiesForm());

        picker.Find($"[data-test=keyword-category-{WeaponKeywords.Wound}]").Click();

        Assert.Equal(WeaponKeywords.All.Count(k => k.Category == WeaponKeywords.Wound), Rows(picker));
    }

    [Fact]
    public void Checking_a_keyword_changes_the_form_and_shows_its_value()
    {
        var target = new WeaponAbilitiesForm();
        int changes = 0;
        var picker = RenderPicker(target, () => changes++);
        Assert.Empty(picker.FindAll("input[data-test=keyword-value-RapidFire]"));

        picker.Find("input[data-test=keyword-RapidFire]").Change(true);
        picker.Find("input[data-test=keyword-value-RapidFire]").Change("2");

        Assert.True(target.HasRapidFire);
        Assert.Equal(2, target.RapidFire);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Critical_thresholds_are_active_when_not_six()
    {
        var target = new WeaponAbilitiesForm();
        var critical = WeaponKeywords.All.Single(k => k.Id == "CriticalWoundOn");
        Assert.False(critical.IsActive(target));

        critical.SetActive(target, true);
        Assert.Equal(5, target.CriticalWoundOn);
        Assert.Equal("5+", critical.ValueText(target));

        critical.SetActive(target, false);
        Assert.Equal(6, target.CriticalWoundOn);
    }

    [Fact]
    public void Catalog_reaches_every_weapon_ability_of_the_core()
    {
        // Tout cocher dans le sélecteur doit donner un profil où chaque mot-clé du Core est actif
        var target = new WeaponAbilitiesForm();
        foreach (var keyword in WeaponKeywords.All)
        {
            keyword.SetActive(target, true);
        }

        var abilities = target.ToWeaponAbilities();

        Assert.Equal(new WeaponAbilities
        {
            Torrent = true, LethalHits = true, SustainedHits = DiceExpression.Constant(1), DevastatingWounds = true, TwinLinked = true,
            Blast = true, RapidFire = 1, Melta = 2, Heavy = true, Lance = true, IgnoresCover = true, Precision = true,
            CriticalHitOn = 5, CriticalWoundOn = 5,
        }, abilities);
    }

    [Fact]
    public void Weapon_card_shows_active_keywords_as_chips_that_can_be_removed()
    {
        var weapon = new WeaponForm(new WeaponGroup
        {
            Weapon = new WeaponProfile { Abilities = new WeaponAbilities { LethalHits = true, SustainedHits = DiceExpression.Parse("D3") } },
        });
        var card = Render<WeaponEditor>(p => p.Add(c => c.Weapon, weapon));

        var chips = card.FindAll("[data-test=keyword-chip]");
        Assert.Equal(["Lethal Hits", "Sustained Hits D3"], chips.Select(c => c.TextContent.Trim()));

        card.Find("[data-test=keyword-chip] .mud-chip-close-button").Click();

        Assert.False(weapon.Abilities.LethalHits);
        Assert.Single(card.FindAll("[data-test=keyword-chip]"));
    }

    [Fact]
    public void Keyword_panel_opens_for_a_weapon_and_closes_when_it_is_removed()
    {
        var page = Render<SimulatorPage>();

        page.Find("[data-test=edit-keywords]").Click();
        Assert.NotEmpty(page.FindAll("[data-test=keyword-row]"));

        page.Find("[data-test=remove-weapon]").Click();

        Assert.Empty(page.FindAll("[data-test=keyword-row]"));
    }

    [Fact]
    public void Collapsed_weapon_shows_the_core_summary()
    {
        var weapon = new WeaponForm(new WeaponGroup { ModelCount = 9, Weapon = new WeaponProfile { Name = "Fusil", Attacks = 2, Skill = 3 } });
        var card = Render<WeaponEditor>(p => p.Add(c => c.Weapon, weapon));

        card.Find("[data-test=toggle-weapon]").Click();

        Assert.Equal("9 × Fusil : A2 CT3+ F4 PA0 D1", card.Find("[data-test=weapon-summary]").TextContent);
    }

    [Fact]
    public void Weapon_card_shows_a_shooting_or_melee_pictogram_even_when_collapsed()
    {
        var ranged = Render<WeaponEditor>(p => p.Add(c => c.Weapon, new WeaponForm()));
        var melee = Render<WeaponEditor>(p => p.Add(c => c.Weapon,
            new WeaponForm(new WeaponGroup { Weapon = new WeaponProfile { Type = WeaponType.Melee } })));

        Assert.NotNull(ranged.Find("[data-test=weapon-ranged-icon]"));
        Assert.Empty(ranged.FindAll("[data-test=weapon-melee-icon]"));
        melee.Find("[data-test=toggle-weapon]").Click();
        Assert.NotNull(melee.Find("[data-test=weapon-melee-icon]"));
    }
}
