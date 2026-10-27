using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Tests;

/// <summary>Règle 05.03 : les PERSONNAGES sont alloués après tous les gardes du corps.</summary>
public class AllocationOrderTests
{
    [Theory]
    [InlineData(new bool[0])]
    [InlineData(new[] { false, false })]
    [InlineData(new[] { false, true, true })]
    [InlineData(new[] { true })]
    public void Characters_after_bodyguards_is_valid(bool[] isCharacter)
    {
        Assert.True(AllocationOrder.IsValid(isCharacter));
        Assert.Null(AllocationOrder.FindViolation(isCharacter));
    }

    [Fact]
    public void A_character_before_a_bodyguard_is_reported()
    {
        bool[] order = [false, true, false, true];

        Assert.False(AllocationOrder.IsValid(order));
        Assert.Equal((1, 2), AllocationOrder.FindViolation(order));
    }

    [Fact]
    public void Arrange_moves_characters_last_and_keeps_each_order()
    {
        string[] groups = ["Leader A", "Gardes", "Leader B", "Élite"];

        var arranged = AllocationOrder.Arrange(groups, g => g.StartsWith("Leader"));

        Assert.Equal(["Gardes", "Élite", "Leader A", "Leader B"], arranged);
    }
}
