using System.Text.Json.Serialization;
using Wh40kSim.Core.Model;

namespace Wh40kSim.Core.Serialization;

/// <summary>Ce que contient un fichier JSON : un scénario complet, un attaquant seul ou un défenseur seul.</summary>
public enum ScenarioFileKind
{
    Scenario,
    Attacker,
    Defender,
}

/// <summary>
/// Contenu d'un fichier JSON, sections facultatives : même format qu'un scénario, dont on peut n'écrire qu'une partie.
/// <c>{ "attacker": … }</c> = fichier attaquant, <c>{ "defender": … }</c> = fichier défenseur, attaquant et défenseur
/// (et éventuellement la situation) = scénario complet. Lu par <see cref="ScenarioJson.ReadFile"/>, qui refuse un
/// fichier sans attaquant ni défenseur, ou une situation hors d'un scénario complet.
/// </summary>
public sealed record ScenarioFile
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AttackerUnit? Attacker { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DefenderUnit? Defender { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AttackContext? Context { get; init; }

    /// <summary>Nature du fichier, déduite des sections présentes (fichier déjà contrôlé par la lecture).</summary>
    [JsonIgnore]
    public ScenarioFileKind Kind => (Attacker, Defender) switch
    {
        (not null, not null) => ScenarioFileKind.Scenario,
        (not null, null) => ScenarioFileKind.Attacker,
        _ => ScenarioFileKind.Defender,
    };

    /// <summary>Scénario complet (situation par défaut si elle est absente).</summary>
    /// <exception cref="InvalidOperationException">Si le fichier ne contient qu'une partie.</exception>
    public Scenario ToScenario() => Kind == ScenarioFileKind.Scenario
        ? new Scenario { Attacker = Attacker!, Defender = Defender!, Context = Context ?? new() }
        : throw new InvalidOperationException("Un fichier partiel n'est pas un scénario complet.");
}
