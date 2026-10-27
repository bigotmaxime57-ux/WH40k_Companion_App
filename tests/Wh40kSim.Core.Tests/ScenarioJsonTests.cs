using System.Text.Json;
using System.Text.Json.Nodes;
using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;
using Wh40kSim.Core.Serialization;

namespace Wh40kSim.Core.Tests;

/// <summary>Format JSON des scénarios : aller-retour, expressions de dés, tolérances de lecture et rejets.</summary>
public class ScenarioJsonTests
{
    private static Scenario FullScenario() => new()
    {
        Attacker = new AttackerUnit
        {
            Name = "Escouade",
            Boosts = new AttackerBoosts { WoundRoll = 1, HitReroll = RerollPolicy.Ones, WoundReroll = RerollPolicy.NonCritical },
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
            Boosts = new DefenderBoosts { InCover = true, HitRoll = -1 },
            Groups =
            [
                new ModelGroup { Name = "Gardes", Count = 5, Toughness = 4, Wounds = 2, Save = 3 },
                new ModelGroup { Name = "Leader", IsCharacter = true, Toughness = 5, Wounds = 5, Save = 2, InvulnerableSave = 4, FeelNoPain = 5, DamageReduction = 1 },
            ],
        },
        Context = new AttackContext { HalfRange = true, Charged = true, PrecisionTarget = "Leader" },
    };

    private static JsonNode Node(string json) => JsonNode.Parse(json) ?? throw new InvalidOperationException("JSON vide.");

    [Fact]
    public void Round_trip_keeps_every_field()
    {
        var scenario = FullScenario();

        var copy = ScenarioJson.Deserialize(ScenarioJson.Serialize(scenario));

        // Les listes des records sont comparées par référence : on compare élément par élément
        Assert.Equal(scenario.Attacker.Name, copy.Attacker.Name);
        Assert.Equal(scenario.Attacker.Boosts, copy.Attacker.Boosts);
        Assert.Equal(scenario.Attacker.Weapons, copy.Attacker.Weapons);
        Assert.Equal(scenario.Defender with { Groups = [] }, copy.Defender with { Groups = [] });
        Assert.Equal(scenario.Defender.Groups, copy.Defender.Groups);
        Assert.Equal(scenario.Context, copy.Context);
        Assert.Equal(ScenarioJson.Serialize(scenario), ScenarioJson.Serialize(copy));
    }

    [Fact]
    public void Save_then_load_round_trips_through_a_file()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wh40ksim-{Guid.NewGuid():N}.json");
        try
        {
            ScenarioJson.Save(FullScenario(), path);
            var loaded = ScenarioJson.Load(path);

            Assert.Equal(ScenarioJson.Serialize(FullScenario()), ScenarioJson.Serialize(loaded));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Constant_dice_are_written_as_numbers_and_random_dice_as_text()
    {
        var weapon = Node(ScenarioJson.Serialize(FullScenario()))["attacker"]!["weapons"]![0]!["weapon"]!;

        Assert.Equal(JsonValueKind.Number, weapon["attacks"]!.GetValueKind());
        Assert.Equal(2, weapon["attacks"]!.GetValue<int>());
        Assert.Equal("D6+1", weapon["damage"]!.GetValue<string>());
        Assert.Equal("D3", weapon["abilities"]!["sustainedHits"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("2", 0, 0, 2)]
    [InlineData("\"3\"", 0, 0, 3)]
    [InlineData("\"D6\"", 1, 6, 0)]
    [InlineData("\"2D3\"", 2, 3, 0)]
    [InlineData("\"D6+1\"", 1, 6, 1)]
    [InlineData("\"D6-1\"", 1, 6, -1)]
    public void Dice_are_read_from_numbers_or_text(string value, int count, int sides, int modifier)
    {
        string json = $$"""{ "attacker": { "weapons": [{ "weapon": { "attacks": {{value}} } }] } }""";

        var attacks = ScenarioJson.Deserialize(json).Attacker.Weapons[0].Weapon.Attacks;

        Assert.Equal(new DiceExpression(count, sides, modifier), attacks);
    }

    [Theory]
    [InlineData("\"D\"")]
    [InlineData("\"abc\"")]
    [InlineData("\"D6 2\"")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[1]")]
    public void Invalid_dice_are_rejected(string value)
    {
        string json = $$"""{ "attacker": { "weapons": [{ "weapon": { "damage": {{value}} } }] } }""";

        Assert.Throws<JsonException>(() => ScenarioJson.Deserialize(json));
    }

    [Fact]
    public void Enums_are_written_as_names()
    {
        var root = Node(ScenarioJson.Serialize(FullScenario()));

        Assert.Equal("Melee", root["attacker"]!["weapons"]![1]!["weapon"]!["type"]!.GetValue<string>());
        Assert.Equal("Ones", root["attacker"]!["boosts"]!["hitReroll"]!.GetValue<string>());
        Assert.Equal("NonCritical", root["attacker"]!["boosts"]!["woundReroll"]!.GetValue<string>());
    }

    [Fact]
    public void Enums_are_read_regardless_of_case()
    {
        const string json = """
            {
              "attacker": { "boosts": { "hitReroll": "FAILED" }, "weapons": [{ "weapon": { "type": "melee" } }] }
            }
            """;

        var scenario = ScenarioJson.Deserialize(json);

        Assert.Equal(WeaponType.Melee, scenario.Attacker.Weapons[0].Weapon.Type);
        Assert.Equal(RerollPolicy.Failed, scenario.Attacker.Boosts.HitReroll);
    }

    [Fact]
    public void Unknown_enum_value_is_rejected()
    {
        Assert.Throws<JsonException>(() => ScenarioJson.Deserialize("""{ "attacker": { "boosts": { "hitReroll": "Toutes" } } }"""));
    }

    [Fact]
    public void Property_names_are_written_in_camel_case()
    {
        var root = Node(ScenarioJson.Serialize(FullScenario())).AsObject();

        Assert.Equal(["attacker", "defender", "context"], root.Select(p => p.Key));
        Assert.True(root["defender"]!["groups"]![1]!.AsObject().ContainsKey("invulnerableSave"));
    }

    [Fact]
    public void Property_names_are_read_regardless_of_case()
    {
        const string json = """{ "Defender": { "NAME": "Cible", "Groups": [{ "Count": 3, "TOUGHNESS": 6 }] } }""";

        var defender = ScenarioJson.Deserialize(json).Defender;

        Assert.Equal("Cible", defender.Name);
        Assert.Equal(3, defender.Groups[0].Count);
        Assert.Equal(6, defender.Groups[0].Toughness);
    }

    [Fact]
    public void Computed_totals_are_not_written()
    {
        var defender = Node(ScenarioJson.Serialize(FullScenario()))["defender"]!.AsObject();

        Assert.False(defender.ContainsKey("totalModels"));
        Assert.False(defender.ContainsKey("totalWounds"));
    }

    [Fact]
    public void Comments_and_trailing_commas_are_allowed()
    {
        const string json = """
            {
              // Commentaire de ligne
              "defender": {
                "name": "Cible", /* commentaire de bloc */
                "groups": [{ "count": 2, },],
              },
            }
            """;

        var defender = ScenarioJson.Deserialize(json).Defender;

        Assert.Equal("Cible", defender.Name);
        Assert.Equal(2, defender.Groups[0].Count);
    }

    [Fact]
    public void Missing_properties_take_their_default_values()
    {
        const string json = """{ "attacker": { "weapons": [{ "weapon": {} }] }, "defender": { "groups": [{}] } }""";

        var scenario = ScenarioJson.Deserialize(json);

        Assert.Equal(new WeaponProfile(), scenario.Attacker.Weapons[0].Weapon);
        Assert.Equal(1, scenario.Attacker.Weapons[0].ModelCount);
        Assert.Equal(new ModelGroup(), scenario.Defender.Groups[0]);
        Assert.Equal(4, scenario.Defender.Groups[0].Toughness);
        Assert.Equal(new AttackContext(), scenario.Context);
    }

    [Theory]
    [InlineData("""{ "iterations": 1000 }""")]
    [InlineData("""{ "attacker": { "nom": "Escouade" } }""")]
    [InlineData("""{ "attacker": { "weapons": [{ "models": 3 }] } }""")]
    [InlineData("""{ "attacker": { "weapons": [{ "weapon": { "ws": 3 } }] } }""")]
    [InlineData("""{ "attacker": { "weapons": [{ "weapon": { "abilities": { "lethal": true } } }] } }""")]
    [InlineData("""{ "defender": { "toughness": 5 } }""")] // l'Endurance appartient aux groupes de figurines
    [InlineData("""{ "defender": { "groups": [{ "pv": 2 }] } }""")]
    [InlineData("""{ "context": { "cover": true } }""")]
    public void Unknown_properties_are_rejected_at_every_level(string json)
    {
        Assert.Throws<JsonException>(() => ScenarioJson.Deserialize(json));
    }

    [Fact]
    public void Unknown_property_error_names_the_property()
    {
        var error = Assert.Throws<JsonException>(() => ScenarioJson.Deserialize("""{ "defender": { "toughness": 5 } }"""));

        Assert.Contains("toughness", error.Message);
    }

    [Fact]
    public void Null_document_is_rejected()
    {
        var error = Assert.Throws<JsonException>(() => ScenarioJson.Deserialize("null"));

        Assert.Equal("Le fichier de scénario est vide.", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("""{ "defender": { "groups": [{ "count": "trois" }] } }""")]
    public void Malformed_json_is_rejected(string json)
    {
        Assert.Throws<JsonException>(() => ScenarioJson.Deserialize(json));
    }

    [Fact]
    public void Missing_file_is_reported_as_an_io_error()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wh40ksim-absent-{Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(() => ScenarioJson.Load(path));
    }
}
