using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Core.Tests;

/// <summary>
/// Quelles questions de situation ont un sens pour un scénario : la console et l'interface n'affichent que celles-là,
/// et le moteur s'appuie sur les mêmes propriétés.
/// </summary>
public class SituationTests
{
    private static WeaponProfile Weapon(WeaponAbilities abilities, WeaponType type = WeaponType.Ranged) => new() { Type = type, Abilities = abilities };

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(0, 2, true)]
    [InlineData(0, 0, false)]
    public void Half_range_matters_for_rapid_fire_and_melta(int rapidFire, int melta, bool expected)
    {
        Assert.Equal(expected, Weapon(new WeaponAbilities { RapidFire = rapidFire, Melta = melta }).UsesHalfRange);
    }

    [Theory]
    [InlineData(WeaponType.Ranged, true)]
    [InlineData(WeaponType.Melee, false)]
    public void Remaining_stationary_matters_for_heavy_ranged_weapons_only(WeaponType type, bool expected)
    {
        Assert.Equal(expected, Weapon(new WeaponAbilities { Heavy = true }, type).UsesRemainedStationary);
        Assert.False(Weapon(new WeaponAbilities(), type).UsesRemainedStationary);
    }

    [Fact]
    public void Charging_matters_for_lance()
    {
        Assert.True(Weapon(new WeaponAbilities { Lance = true }).UsesCharged);
        Assert.False(Weapon(new WeaponAbilities()).UsesCharged);
    }

    [Fact]
    public void Attacker_needs_a_situation_when_any_weapon_does()
    {
        var attacker = new AttackerUnit
        {
            Weapons =
            [
                new WeaponGroup { Weapon = Weapon(new WeaponAbilities()) },
                new WeaponGroup { Weapon = Weapon(new WeaponAbilities { Melta = 2, Lance = true }) },
            ],
        };

        Assert.True(attacker.UsesHalfRange);
        Assert.True(attacker.UsesCharged);
        Assert.False(attacker.UsesRemainedStationary);
    }

    [Theory]
    [InlineData(true, 2, true)]
    [InlineData(true, 1, false)]  // un seul PERSONNAGE : c'est forcément lui
    [InlineData(false, 2, false)] // aucune arme Precision
    public void Precision_target_matters_with_a_precision_weapon_and_several_characters(bool precision, int characters, bool expected)
    {
        var groups = new List<ModelGroup> { new() { Name = "Gardes", Count = 5 } };
        groups.AddRange(Enumerable.Range(1, characters).Select(i => new ModelGroup { Name = $"Leader {i}", IsCharacter = true }));
        var scenario = new Scenario
        {
            Attacker = new AttackerUnit { Weapons = [new WeaponGroup { Weapon = Weapon(new WeaponAbilities { Precision = precision }) }] },
            Defender = new DefenderUnit { Groups = groups },
        };

        Assert.Equal(expected, scenario.PrecisionTargetMatters);
    }

    [Fact]
    public void Situation_helpers_are_not_written_to_json()
    {
        var scenario = new Scenario
        {
            Attacker = new AttackerUnit { Weapons = [new WeaponGroup { Weapon = Weapon(new WeaponAbilities { Lance = true }) }] },
        };

        string json = ScenarioJson.Serialize(scenario);

        Assert.DoesNotContain("uses", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("precisionTargetMatters", json);
    }
}
