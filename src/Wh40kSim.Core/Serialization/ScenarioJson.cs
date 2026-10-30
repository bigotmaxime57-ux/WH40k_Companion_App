using System.Text.Json;
using System.Text.Json.Serialization;
using Wh40kSim.Core.Dice;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Serialization;

/// <summary>
/// Lecture / écriture au format JSON : scénarios complets, et fichiers partiels (attaquant seul, défenseur seul, voir
/// <see cref="ScenarioFile"/>). Lecture stricte : propriété inconnue, section manquante ou mal placée refusées.
/// </summary>
public static class ScenarioJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Lit un fichier qui peut ne contenir qu'une partie (attaquant seul, défenseur seul) ou un scénario complet.</summary>
    /// <exception cref="JsonException">JSON invalide, sans attaquant ni défenseur, ou situation sans scénario complet.</exception>
    public static ScenarioFile ReadFile(string json)
    {
        var file = JsonSerializer.Deserialize<ScenarioFile>(json, Options) ?? throw new JsonException("Le fichier de scénario est vide.");
        if (file.Attacker is null && file.Defender is null)
        {
            throw new JsonException("Le fichier ne contient ni attaquant ni défenseur.");
        }
        if (file.Context is not null && file.Kind != ScenarioFileKind.Scenario)
        {
            throw new JsonException("La situation (context) n'a de sens que dans un scénario complet, avec un attaquant et un défenseur.");
        }
        return file;
    }

    /// <summary>Lit un scénario complet : attaquant et défenseur obligatoires, situation facultative.</summary>
    /// <exception cref="JsonException">JSON invalide ou fichier partiel.</exception>
    public static Scenario Deserialize(string json)
    {
        var file = ReadFile(json);
        return file.Kind switch
        {
            ScenarioFileKind.Scenario => file.ToScenario(),
            ScenarioFileKind.Attacker => throw new JsonException("Le fichier ne contient qu'un attaquant : un scénario complet demande aussi un défenseur."),
            _ => throw new JsonException("Le fichier ne contient qu'un défenseur : un scénario complet demande aussi un attaquant."),
        };
    }

    public static string Serialize(Scenario scenario) => JsonSerializer.Serialize(scenario, Options);

    /// <summary>Fichier attaquant : <c>{ "attacker": … }</c>, boosts compris.</summary>
    public static string Serialize(AttackerUnit attacker) => JsonSerializer.Serialize(new ScenarioFile { Attacker = attacker }, Options);

    /// <summary>Fichier défenseur : <c>{ "defender": … }</c>, boosts compris.</summary>
    public static string Serialize(DefenderUnit defender) => JsonSerializer.Serialize(new ScenarioFile { Defender = defender }, Options);

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
        // Avant le convertisseur générique des énumérations : le premier qui accepte le type est utilisé
        options.Converters.Add(new RerollPolicyJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DiceExpressionJsonConverter());
        return options;
    }
}

/// <summary>
/// Relance écrite par son nom (<c>None</c>, <c>Ones</c>, <c>Failed</c>) ; toute autre valeur est refusée, dont l'ancienne
/// « tout sauf critique » (<c>NonCritical</c>), remplacée par la pêche aux critiques sur les armes.
/// </summary>
public sealed class RerollPolicyJsonConverter : JsonConverter<RerollPolicy>
{
    public override RerollPolicy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (Enum.TryParse(text, ignoreCase: true, out RerollPolicy policy) && Enum.IsDefined(policy) && !int.TryParse(text, out _))
        {
            return policy;
        }
        if (string.Equals(text, "NonCritical", StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException("La relance « NonCritical » (tout sauf critique) n'existe plus : choisir \"Failed\" (relance complète) "
                                    + "et cocher la pêche aux critiques (fishForCriticalHits, fishForCriticalWounds) sur les armes concernées.");
        }
        throw new JsonException("Relance attendue : \"None\", \"Ones\" ou \"Failed\" (relance complète).");
    }

    public override void Write(Utf8JsonWriter writer, RerollPolicy value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
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
