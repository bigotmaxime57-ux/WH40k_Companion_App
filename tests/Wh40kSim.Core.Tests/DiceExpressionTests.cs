using Wh40kSim.Core.Dice;

namespace Wh40kSim.Core.Tests;

public class DiceExpressionTests
{
    [Theory]
    [InlineData("3", 0, 0, 3)]
    [InlineData("D6", 1, 6, 0)]
    [InlineData("d3", 1, 3, 0)]
    [InlineData("2D6", 2, 6, 0)]
    [InlineData("D6+2", 1, 6, 2)]
    [InlineData(" D3 - 1 ", 1, 3, -1)]
    public void Parse_reads_expressions(string text, int count, int sides, int modifier)
    {
        Assert.Equal(new DiceExpression(count, sides, modifier), DiceExpression.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("D")]
    [InlineData("D6 2")]
    [InlineData("abc")]
    [InlineData("D1")]
    public void Parse_rejects_invalid_text(string text)
    {
        Assert.Throws<FormatException>(() => DiceExpression.Parse(text));
    }

    [Theory]
    [InlineData("D6", 3.5)]
    [InlineData("2D3+1", 5)]
    [InlineData("4", 4)]
    public void Average_is_correct(string text, double expected)
    {
        Assert.Equal(expected, DiceExpression.Parse(text).Average, 6);
    }

    [Theory]
    [InlineData("D6+1")]
    [InlineData("2D3")]
    [InlineData("D3-1")]
    [InlineData("5")]
    public void ToString_round_trips(string text)
    {
        Assert.Equal(text, DiceExpression.Parse(text).ToString());
    }

    [Fact]
    public void Roll_sums_dice_and_modifier_and_never_goes_below_zero()
    {
        Assert.Equal(9, DiceExpression.Parse("2D6+1").Roll(new ScriptedDiceRoller(3, 5)));
        Assert.Equal(0, DiceExpression.Parse("D3-2").Roll(new ScriptedDiceRoller(1)));
    }
}
