using Bunit;
using Wh40kSim.Core.Model;
using Wh40kSim.Simulation;
using Wh40kSim.UI.Components;

namespace Wh40kSim.UI.Tests.Components;

/// <summary>Tuiles de synthèse : médiane, écart-type et fourchette en colonne, chacun expliqué par une bulle « ? ».</summary>
public class SummaryTilesTests : ComponentTestContext
{
    private static readonly SimulationResult Result = Simulator.Run(new Scenario
    {
        Attacker = new AttackerUnit { Weapons = [new WeaponGroup { ModelCount = 10, Weapon = new WeaponProfile { Skill = 3 } }] },
        Defender = new DefenderUnit { Groups = [new ModelGroup { Name = "Cibles", Count = 10 }] },
    }, 2_000, seed: 1);

    [Fact]
    public void Kills_and_damage_tiles_list_median_spread_and_range_with_help()
    {
        var tiles = Render<SummaryTiles>(p => p.Add(c => c.Result, Result));

        foreach (string tile in new[] { "tile-kills", "tile-damage" })
        {
            var stats = tiles.Find($"[data-test={tile}] [data-test=tile-stats]");
            Assert.Equal(3, stats.QuerySelectorAll(".wh-tile-stat").Length);
            Assert.Contains("Médiane", stats.TextContent);
            Assert.Contains("Écart-type", stats.TextContent);
            Assert.Contains("80 % des cas", stats.TextContent);
            Assert.Equal(3, stats.QuerySelectorAll("[data-test=help]").Length);
        }
        Assert.Empty(tiles.FindAll("[data-test=wipe] [data-test=tile-stats]"));
    }
}
