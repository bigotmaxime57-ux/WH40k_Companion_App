using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Simulation.Tests;

/// <summary>Pertes par groupe, gardes du corps et part des pertes grâce à Precision.</summary>
public class GroupLossesTests
{
    /// <summary>Gardes, leader et soutien ; une arme Precision et une arme normale.</summary>
    private static Scenario LeaderAndSupport(string? precisionTarget = null, bool precision = true, int bolters = 5) => new()
    {
        Attacker = new AttackerUnit
        {
            Weapons =
            [
                new WeaponGroup
                {
                    ModelCount = 2,
                    Weapon = new WeaponProfile
                    {
                        Name = "Précise", Attacks = 2, Skill = 3, Strength = 5, Ap = -1, Damage = DiceExpression.Parse("D3"),
                        Abilities = new WeaponAbilities { Precision = precision },
                    },
                },
                new WeaponGroup { ModelCount = bolters, Weapon = new WeaponProfile { Name = "Bolter", Attacks = 2, Skill = 3, Strength = 4, Damage = 1 } },
            ],
        },
        Defender = new DefenderUnit
        {
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 5, Toughness = 4, Wounds = 2, Save = 4 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Toughness = 4, Wounds = 4, Save = 3 },
                new ModelGroup { Name = "Soutien", IsCharacter = true, Toughness = 4, Wounds = 3, Save = 4 },
            ],
        },
        Context = new AttackContext { PrecisionTarget = precisionTarget },
    };

    [Fact]
    public void Group_distributions_are_consistent_with_the_unit()
    {
        var result = Simulator.Run(LeaderAndSupport(), 100_000, seed: 11);

        Assert.Equal(3, result.Groups.Count);
        for (int g = 0; g < result.Groups.Count; g++)
        {
            var losses = result.Groups[g];
            Assert.Equal(g, losses.Index);
            Assert.Equal(losses.Group.Count, losses.Kills.MaxValue);
            Assert.Equal(losses.Group.Count * losses.Group.Wounds, losses.Damage.MaxValue);
            Assert.Equal(result.Iterations, losses.Kills.Total);
            Assert.Equal(result.MeanKilledInGroup(g), losses.Kills.Mean, 9);
            Assert.Equal(result.MeanDamageInGroup(g), losses.Damage.Mean, 9);
        }
        Assert.Equal(result.Kills.Mean, result.Groups.Sum(l => l.Kills.Mean), 9);
        Assert.Equal(result.Damage.Mean, result.Groups.Sum(l => l.Damage.Mean), 9);
    }

    [Fact]
    public void The_last_group_is_wiped_exactly_when_the_unit_is()
    {
        // Sans Precision : la dernière figurine à tomber est toujours celle du dernier groupe
        var result = Simulator.Run(LeaderAndSupport(precision: false, bolters: 60), 100_000, seed: 12);

        Assert.InRange(result.ProbabilityWipe, 0.05, 0.95);
        Assert.Equal(result.ProbabilityWipe, result.Groups[^1].ProbabilityWiped, 12);
        Assert.All(result.Groups, l => Assert.True(l.ProbabilityWiped >= result.ProbabilityWipe));
    }

    [Fact]
    public void Bodyguards_are_counted_together()
    {
        var result = Simulator.Run(LeaderAndSupport(), 100_000, seed: 13);

        Assert.NotNull(result.BodyguardKills);
        Assert.Equal(5, result.BodyguardKills!.MaxValue);
        // Un seul groupe de gardes : sa distribution est celle des gardes
        Assert.Equal(result.Groups[0].Kills.Counts, result.BodyguardKills.Counts);
        Assert.Equal(result.Groups[0].ProbabilityWiped, result.ProbabilityBodyguardsWiped);
        // PV retirés aux gardes : de 0 à la somme de leurs PV, et la distribution du seul groupe de gardes
        Assert.NotNull(result.BodyguardDamage);
        Assert.Equal(result.Groups[0].Group.Count * result.Groups[0].Group.Wounds, result.BodyguardDamage!.MaxValue);
        Assert.Equal(result.Groups[0].Damage.Counts, result.BodyguardDamage.Counts);
    }

    [Fact]
    public void Bodyguard_damage_adds_up_every_non_character_group()
    {
        var scenario = LeaderAndSupport();
        scenario = scenario with
        {
            Defender = scenario.Defender with
            {
                Groups = [new ModelGroup { Name = "Gardes lourds", Count = 2, Wounds = 3, Save = 2 }, .. scenario.Defender.Groups],
            },
        };

        var result = Simulator.Run(scenario, 50_000, seed: 15);

        var bodyguards = result.Groups.Where(l => !l.Group.IsCharacter).ToList();
        Assert.Equal(2, bodyguards.Count);
        Assert.Equal(bodyguards.Sum(l => l.Group.Count * l.Group.Wounds), result.BodyguardDamage!.MaxValue);
        Assert.Equal(bodyguards.Sum(l => l.Damage.Mean), result.BodyguardDamage.Mean, 10);
    }

    [Fact]
    public void Precision_weapon_work_is_consistent_with_the_group_losses()
    {
        var result = Simulator.Run(LeaderAndSupport(precisionTarget: "Leader", bolters: 30), 100_000, seed: 16);

        var leader = result.Groups[1];
        var work = leader.PrecisionWeapons!;
        // Les deux parties de la barre « tué » font P(tué), celles des PV la moyenne des PV retirés
        Assert.Equal(leader.ProbabilityWiped, work.ProbabilityKilledByPrecisionAlone + work.ProbabilityKilledWithHelp, 12);
        Assert.Equal(leader.Damage.Mean, work.MeanPrecisionDamage + work.MeanOtherDamage, 10);
        Assert.InRange(work.ProbabilityKilledByPrecisionAlone, 0.01, 0.99);
        Assert.InRange(work.ProbabilityKilledWithHelp, 0.001, 0.99);   // 30 bolters : les gardes tombent, puis le leader
        Assert.True(work.ProbabilityKilledWhileBodyguardsAlive <= leader.ProbabilityWiped);
        Assert.Equal(1 - work.ProbabilityKilledByPrecisionAlone, work.ProbabilityPrecisionFallsShort, 12);
        Assert.InRange(work.MeanWoundsLeftWhenShort, 0.0, 4.0);
        Assert.All(work.PrecisionShareByDamage, share => Assert.InRange(share, 0.0, 1.0));
        Assert.Equal(leader.Damage.MaxValue + 1, work.PrecisionShareByDamage.Count);
    }

    [Fact]
    public void With_precision_weapons_only_every_lost_wound_comes_from_them()
    {
        var scenario = LeaderAndSupport(precisionTarget: "Leader");
        scenario = scenario with { Attacker = scenario.Attacker with { Weapons = [scenario.Attacker.Weapons[0]] } };   // l'arme Precision seule

        var result = Simulator.Run(scenario, 50_000, seed: 17);

        var leader = result.Groups[1];
        var work = leader.PrecisionWeapons!;
        Assert.Equal(0, work.ProbabilityKilledWithHelp);
        Assert.Equal(0, work.MeanOtherDamage, 12);
        Assert.Equal(leader.ProbabilityWiped, work.ProbabilityKilledByPrecisionAlone, 12);
        for (int k = 1; k <= leader.Damage.MaxValue; k++)
        {
            if (leader.Damage.ProbabilityExactly(k) > 0)
            {
                Assert.Equal(1.0, work.PrecisionShareByDamage[k], 12);
            }
        }
    }

    [Fact]
    public void Without_a_precision_weapon_there_is_no_precision_weapon_report()
    {
        var result = Simulator.Run(LeaderAndSupport(precision: false), 10_000, seed: 18);

        Assert.All(result.Groups, l => Assert.Null(l.PrecisionWeapons));
    }

    [Fact]
    public void Without_a_character_there_is_no_bodyguard_report()
    {
        var result = Simulator.Run(Build.Scenario(new WeaponProfile { Attacks = 3 }, Build.Horde(models: 10), models: 5), 10_000, seed: 1);

        Assert.Null(result.BodyguardKills);
        Assert.Null(result.BodyguardDamage);
        Assert.Null(result.ProbabilityBodyguardsWiped);
        Assert.Null(result.PrecisionTarget);
    }

    [Fact]
    public void Precision_losses_go_to_the_chosen_target_only()
    {
        var result = Simulator.Run(LeaderAndSupport(precisionTarget: "Soutien"), 100_000, seed: 14);

        Assert.Equal(2, result.PrecisionTarget!.Index);
        var support = result.Groups[2];
        Assert.InRange(support.PrecisionKillShare, 0.01, 1.0);
        Assert.InRange(support.PrecisionDamageShare, 0.01, 1.0);
        Assert.True(support.MeanPrecisionKills <= support.Kills.Mean);
        Assert.Equal(0, result.Groups[0].MeanPrecisionKills);
        Assert.Equal(0, result.Groups[1].MeanPrecisionKills);
        Assert.Equal(0, result.Groups[1].MeanPrecisionDamage);
    }

    [Fact]
    public void Without_precision_nothing_is_due_to_it()
    {
        var result = Simulator.Run(LeaderAndSupport(precision: false), 50_000, seed: 15);

        Assert.Null(result.PrecisionTarget);
        Assert.All(result.Groups, l =>
        {
            Assert.Equal(0, l.MeanPrecisionKills);
            Assert.Equal(0, l.PrecisionKillShare);
            Assert.Equal(0, l.PrecisionDamageShare);
        });
    }
}
