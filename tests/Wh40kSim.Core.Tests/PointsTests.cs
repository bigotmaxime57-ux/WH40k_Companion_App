using System.Text.Json.Nodes;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Core.Tests;

/// <summary>Points des unités (facultatifs) et PV retirés par groupe, base du calcul des points détruits.</summary>
public class PointsTests
{
    private static WeaponProfile Torrent(int attacks) => new() { Attacks = attacks, Strength = 8, Abilities = new WeaponAbilities { Torrent = true } };

    private static DefenderUnit SquadWithLeader(int? squadPoints = 100, int? leaderPoints = 60) => new()
    {
        Groups =
        [
            new ModelGroup { Name = "Gardes", Count = 2, Wounds = 2, Points = squadPoints },
            new ModelGroup { Name = "Leader", IsCharacter = true, Wounds = 3, Points = leaderPoints },
        ],
    };

    [Fact]
    public void Damage_in_a_group_counts_the_wounded_model()
    {
        var resolver = new AttackResolver(Build.Scenario(Torrent(3), SquadWithLeader()));

        // Blessures 2, 2, 2 (F8 contre E4 : 2+) ; sauvegardes 1, 1, 1 ratées : un garde tué (2 PV), le suivant entamé (1 PV)
        resolver.Run(new ScriptedDiceRoller(2, 2, 2, 1, 1, 1));

        Assert.Equal(1, resolver.Target.KilledInGroup(0));
        Assert.Equal(3, resolver.Target.DamageInGroup(0));
        Assert.Equal(0, resolver.Target.DamageInGroup(1));
        Assert.Equal(resolver.Target.DamageDealt, resolver.Target.DamageInGroup(0) + resolver.Target.DamageInGroup(1));
    }

    [Fact]
    public void A_wiped_group_counts_all_its_wounds_and_the_next_one_its_share()
    {
        var resolver = new AttackResolver(Build.Scenario(Torrent(5), SquadWithLeader()));

        // 5 PV retirés : les deux gardes (4 PV), puis 1 PV au leader
        resolver.Run(new ScriptedDiceRoller(2, 2, 2, 2, 2, 1, 1, 1, 1, 1));

        Assert.Equal(4, resolver.Target.DamageInGroup(0));
        Assert.Equal(1, resolver.Target.DamageInGroup(1));
        Assert.Equal(5, resolver.Target.DamageDealt);
    }

    [Fact]
    public void Total_points_exist_only_when_every_group_has_points()
    {
        Assert.Equal(160, SquadWithLeader().TotalPoints);
        Assert.Null(SquadWithLeader(leaderPoints: null).TotalPoints);
        Assert.Null(new DefenderUnit().TotalPoints);
    }

    [Fact]
    public void Negative_points_are_rejected()
    {
        var scenario = Build.Scenario(Torrent(1), SquadWithLeader(squadPoints: -5));
        scenario = scenario with { Attacker = scenario.Attacker with { Points = -1 } };

        var errors = scenario.Validate();

        Assert.Contains(errors, e => e.Contains("points de l'attaquant"));
        Assert.Contains(errors, e => e.Contains("Gardes") && e.Contains("points"));
    }

    [Fact]
    public void Points_round_trip_through_json()
    {
        var scenario = Build.Scenario(Torrent(1), SquadWithLeader());
        scenario = scenario with { Attacker = scenario.Attacker with { Points = 180 } };

        var copy = ScenarioJson.Deserialize(ScenarioJson.Serialize(scenario));

        Assert.Equal(180, copy.Attacker.Points);
        Assert.Equal([100, 60], copy.Defender.Groups.Select(g => g.Points));
    }

    [Fact]
    public void Missing_points_are_not_written()
    {
        var root = JsonNode.Parse(ScenarioJson.Serialize(Build.Scenario(Torrent(1), SquadWithLeader(null, null))))!;

        Assert.False(root["attacker"]!.AsObject().ContainsKey("points"));
        Assert.False(root["defender"]!["groups"]![0]!.AsObject().ContainsKey("points"));
    }
}
