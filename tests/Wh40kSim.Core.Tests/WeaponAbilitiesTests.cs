using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Engine;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Tests;

public class WeaponAbilitiesTests
{
    [Fact]
    public void Parse_reads_english_keywords_with_values()
    {
        var a = WeaponAbilities.Parse("Lethal Hits, Sustained Hits D3, Devastating Wounds, Twin-linked, Rapid Fire 1, Melta 2, Anti 4+, crit hit 5+");

        Assert.True(a.LethalHits);
        Assert.Equal(DiceExpression.Parse("D3"), a.SustainedHits);
        Assert.True(a.DevastatingWounds);
        Assert.True(a.TwinLinked);
        Assert.Equal(1, a.RapidFire);
        Assert.Equal(2, a.Melta);
        Assert.Equal(4, a.CriticalWoundOn);
        Assert.Equal(5, a.CriticalHitOn);
    }

    [Fact]
    public void Parse_reads_french_aliases_and_defaults()
    {
        var a = WeaponAbilities.Parse("touches fatales; touches soutenues; blessures dévastatrices; ignore le couvert");

        Assert.True(a.LethalHits);
        Assert.Equal(DiceExpression.Constant(1), a.SustainedHits);
        Assert.True(a.DevastatingWounds);
        Assert.True(a.IgnoresCover);
    }

    [Fact]
    public void Parse_treats_anti_keyword_as_critical_wound_threshold()
    {
        Assert.Equal(4, WeaponAbilities.Parse("Anti-Infantry 4+").CriticalWoundOn);
    }

    [Theory]
    [InlineData("pouet")]
    [InlineData("melta")]
    [InlineData("anti 9+")]
    public void Parse_rejects_unknown_or_incomplete_keywords(string text)
    {
        Assert.Throws<FormatException>(() => WeaponAbilities.Parse(text));
    }

    [Theory]
    [InlineData(8, 4, 2)]
    [InlineData(5, 4, 3)]
    [InlineData(4, 4, 4)]
    [InlineData(3, 4, 5)]
    [InlineData(2, 4, 6)]
    [InlineData(4, 8, 6)]
    [InlineData(5, 9, 5)]
    public void WoundTable_follows_strength_versus_toughness(int strength, int toughness, int expected)
    {
        Assert.Equal(expected, WoundTable.RequiredRoll(strength, toughness));
    }
}
