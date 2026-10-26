using System.Text.Json;
using System.Text.Json.Serialization;
using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Serialization;

/// <summary>Lecture / écriture des scénarios au format JSON (réutilisable plus tard par l'API et l'UI).</summary>
public static class ScenarioJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static Scenario Deserialize(string json) =>
        JsonSerializer.Deserialize<Scenario>(json, Options) ?? throw new JsonException("Le fichier de scénario est vide.");

    public static string Serialize(Scenario scenario) => JsonSerializer.Serialize(scenario, Options);

    public static Scenario Load(string path) => Deserialize(File.ReadAllText(path));

    public static void Save(Scenario scenario, string path) => File.WriteAllText(path, Serialize(scenario));

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            // Une propriété mal placée ou mal orthographiée (ex. "toughness" sur le défenseur) ne doit pas être ignorée en silence
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DiceExpressionJsonConverter());
        return options;
    }
}

/// <summary>Une expression de dés s'écrit en JSON soit comme un nombre (2), soit comme un texte ("D6+1").</summary>
public sealed class DiceExpressionJsonConverter : JsonConverter<DiceExpression>
{
    public override DiceExpression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => DiceExpression.Constant(reader.GetInt32()),
                JsonTokenType.String => DiceExpression.Parse(reader.GetString() ?? ""),
                _ => throw new JsonException("Expression de dés attendue (nombre ou texte comme \"D6+1\")."),
            };
        }
        catch (FormatException ex)
        {
            throw new JsonException(ex.Message, ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, DiceExpression value, JsonSerializerOptions options)
    {
        if (value.IsConstant)
        {
            writer.WriteNumberValue(value.Modifier);
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
