using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;
using Wh40kSim.UI.Forms;

namespace Wh40kSim.UI.Tests;

/// <summary>Aller-retour Scenario → formulaire → Scenario : rien ne doit se perdre ni changer en route.</summary>
public class ScenarioFormTests
{
    /// <summary>
    /// Compare via le JSON : les records contenant des listes ne sont pas égaux par valeur,
    /// et le JSON couvre tous les champs d'un coup.
    /// </summary>
    private static void AssertSameScenario(Scenario expected, Scenario actual) =>
        Assert.Equal(ScenarioJson.Serialize(expected), ScenarioJson.Serialize(actual));

    private static Scenario RoundTrip(Scenario scenario) => new ScenarioForm(scenario).ToScenario();

    private static Scenario WithAbilities(WeaponAbilities abilities) => new()
    {
        Attacker = new AttackerUnit { Weapons = [new WeaponGroup { Weapon = new WeaponProfile { Abilities = abilities } }] },
        Defender = new DefenderUnit { Groups = [new ModelGroup()] },
    };

    private static Scenario FullScenario() => new()
    {
        Attacker = new AttackerUnit
        {
            Name = "Escouade",
            Weapons =
            [
                new WeaponGroup
                {
                    ModelCount = 4,
                    Weapon = new WeaponProfile
                    {
                        Name = "Fusil",
                        Attacks = 2,
                        Skill = 3,
                        Strength = 4,
                        Ap = -1,
                        Damage = DiceExpression.Parse("D6+1"),
                        Abilities = new WeaponAbilities { RapidFire = 1, SustainedHits = DiceExpression.Parse("D3"), CriticalHitOn = 5 },
                    },
                },
                new WeaponGroup
                {
                    Weapon = new WeaponProfile
                    {
                        Name = "Épée",
                        Type = WeaponType.Melee,
                        Attacks = DiceExpression.Parse("2D3"),
                        Abilities = new WeaponAbilities { Precision = true, Lance = true },
                    },
                },
            ],
        },
        Defender = new DefenderUnit
        {
            Name = "Escouade + Leader",
            InCover = true,
            HitModifier = -1,
            WoundModifier = 1,
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 5, Toughness = 4, Wounds = 2, Save = 3 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Toughness = 5, Wounds = 5, Save = 2, InvulnerableSave = 4, FeelNoPain = 5, DamageReduction = 1 },
            ],
        },
        Context = new AttackContext
        {
            HalfRange = true,
            RemainedStationary = true,
            Charged = true,
            HitModifier = 1,
            WoundModifier = -1,
            SkillModifier = 1,
            AttacksModifier = 2,
            StrengthModifier = 3,
            ApModifier = 1,
            DamageModifier = 1,
            HitReroll = RerollPolicy.Ones,
            WoundReroll = RerollPolicy.NonCritical,
            PrecisionTarget = "Leader",
        },
    };

    public static TheoryData<string, WeaponAbilities> EachAbility => new()
    {
        { "Torrent", new WeaponAbilities { Torrent = true } },
        { "Lethal Hits", new WeaponAbilities { LethalHits = true } },
        { "Sustained Hits 1", new WeaponAbilities { SustainedHits = 1 } },
        { "Sustained Hits D3", new WeaponAbilities { SustainedHits = DiceExpression.Parse("D3") } },
        { "Devastating Wounds", new WeaponAbilities { DevastatingWounds = true } },
        { "Twin-linked", new WeaponAbilities { TwinLinked = true } },
        { "Blast", new WeaponAbilities { Blast = true } },
        { "Rapid Fire 2", new WeaponAbilities { RapidFire = 2 } },
        { "Melta 4", new WeaponAbilities { Melta = 4 } },
        { "Heavy", new WeaponAbilities { Heavy = true } },
        { "Lance", new WeaponAbilities { Lance = true } },
        { "Ignores Cover", new WeaponAbilities { IgnoresCover = true } },
        { "Precision", new WeaponAbilities { Precision = true } },
        { "Touche critique 5+", new WeaponAbilities { CriticalHitOn = 5 } },
        { "Blessure critique 4+", new WeaponAbilities { CriticalWoundOn = 4 } },
    };

    [Fact]
    public void Full_scenario_round_trips()
    {
        AssertSameScenario(FullScenario(), RoundTrip(FullScenario()));
    }

    [Fact]
    public void Empty_form_matches_the_core_defaults()
    {
        AssertSameScenario(new Scenario(), new ScenarioForm().ToScenario());
        AssertSameScenario(new Scenario(), RoundTrip(new Scenario()));
    }

    [Fact]
    public void New_list_items_match_the_core_defaults()
    {
        Assert.Equal(new WeaponGroup().Weapon, new WeaponForm().ToWeaponGroup().Weapon);
        Assert.Equal(new WeaponGroup().ModelCount, new WeaponForm().ToWeaponGroup().ModelCount);
        Assert.Equal(new ModelGroup(), new ModelGroupForm().ToModelGroup());
    }

    [Theory]
    [MemberData(nameof(EachAbility))]
    public void Each_weapon_ability_round_trips(string label, WeaponAbilities abilities)
    {
        Assert.NotEmpty(label);
        AssertSameScenario(WithAbilities(abilities), RoundTrip(WithAbilities(abilities)));
    }

    [Fact]
    public void Optional_defensive_values_round_trip_when_absent()
    {
        var scenario = new Scenario
        {
            Defender = new DefenderUnit { Groups = [new ModelGroup { InvulnerableSave = null, FeelNoPain = null }] },
            Context = new AttackContext { PrecisionTarget = null },
        };

        var copy = RoundTrip(scenario);

        Assert.Null(copy.Defender.Groups[0].InvulnerableSave);
        Assert.Null(copy.Defender.Groups[0].FeelNoPain);
        Assert.Null(copy.Context.PrecisionTarget);
    }

    [Fact]
    public void Group_order_is_kept()
    {
        var form = new ScenarioForm(FullScenario());

        Assert.Equal(["Gardes", "Leader"], form.Defender.Groups.Select(g => g.Name));
        Assert.Equal(["Gardes", "Leader"], form.ToScenario().Defender.Groups.Select(g => g.Name));
    }

    [Fact]
    public void Every_scenario_file_round_trips()
    {
        string[] files = Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "scenarios"), "*.json");
        Assert.NotEmpty(files);

        foreach (string file in files)
        {
            var scenario = ScenarioJson.Load(file);
            AssertSameScenario(scenario, RoundTrip(scenario));
        }
    }

    [Fact]
    public void Dice_are_shown_as_text()
    {
        var weapon = new ScenarioForm(FullScenario()).Attacker.Weapons[0];

        Assert.Equal("2", weapon.Attacks);
        Assert.Equal("D6+1", weapon.Damage);
        Assert.Equal("D3", weapon.Abilities.SustainedHits);
    }

    [Fact]
    public void Edits_in_the_form_reach_the_scenario()
    {
        var form = new ScenarioForm(FullScenario());
        form.Attacker.Weapons[0].Attacks = "D6+2";
        form.Attacker.Weapons.RemoveAt(1);
        form.Defender.Groups[0].Toughness = 6;
        form.Context.HitReroll = RerollPolicy.Failed;

        var scenario = form.ToScenario();

        Assert.Equal(DiceExpression.Parse("D6+2"), scenario.Attacker.Weapons[0].Weapon.Attacks);
        Assert.Single(scenario.Attacker.Weapons);
        Assert.Equal(6, scenario.Defender.Groups[0].Toughness);
        Assert.Equal(RerollPolicy.Failed, scenario.Context.HitReroll);
    }

    [Fact]
    public void Unchecked_valued_abilities_are_absent_but_keep_their_value_in_the_form()
    {
        var form = new WeaponAbilitiesForm(new WeaponAbilities { RapidFire = 2, Melta = 3, SustainedHits = DiceExpression.Parse("D3") });
        form.HasRapidFire = false;
        form.HasMelta = false;
        form.HasSustainedHits = false;

        var abilities = form.ToWeaponAbilities();

        Assert.Equal(0, abilities.RapidFire);
        Assert.Equal(0, abilities.Melta);
        Assert.True(abilities.SustainedHits.IsZero);
        Assert.Equal(2, form.RapidFire);
        Assert.Equal(3, form.Melta);
        Assert.Equal("D3", form.SustainedHits);
    }

    [Fact]
    public void Unchecked_sustained_hits_ignores_an_invalid_value()
    {
        var form = new WeaponAbilitiesForm { HasSustainedHits = false, SustainedHits = "abc" };

        Assert.True(form.ToWeaponAbilities().SustainedHits.IsZero);
    }

    [Theory]
    [InlineData("D", "1", "1")]
    [InlineData("1", "D6 2", "1")]
    [InlineData("1", "1", "abc")]
    public void Invalid_dice_text_is_rejected(string attacks, string damage, string sustainedHits)
    {
        var form = new ScenarioForm(WithAbilities(new WeaponAbilities()));
        var weapon = form.Attacker.Weapons[0];
        weapon.Attacks = attacks;
        weapon.Damage = damage;
        weapon.Abilities.HasSustainedHits = true;
        weapon.Abilities.SustainedHits = sustainedHits;

        Assert.Throws<FormatException>(() => form.ToScenario());
    }

    [Fact]
    public void Weapon_clone_is_independent_and_keeps_invalid_text()
    {
        var weapon = new ScenarioForm(FullScenario()).Attacker.Weapons[0];
        weapon.Attacks = "D";

        var copy = weapon.Clone();
        copy.Name = "Copie";
        copy.Abilities.HasRapidFire = false;

        Assert.Equal("D", copy.Attacks);
        Assert.Equal("Fusil", weapon.Name);
        Assert.True(weapon.Abilities.HasRapidFire);
        Assert.NotSame(weapon.Abilities, copy.Abilities);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wh40kSim.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Racine de la solution introuvable.");
    }
}
