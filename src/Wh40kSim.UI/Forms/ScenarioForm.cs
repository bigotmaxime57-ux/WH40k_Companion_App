using Wh40kSim.Core.Model;

namespace Wh40kSim.UI.Forms;

/// <summary>
/// Version modifiable d'un <see cref="Scenario"/>, liée aux champs de l'interface.
/// Les records du Core sont immuables (propriétés en init) : on édite ce formulaire, puis
/// <see cref="ToScenario"/> produit le scénario à valider et à simuler.
/// </summary>
public sealed class ScenarioForm
{
    /// <summary>Formulaire vierge, avec les valeurs par défaut du Core.</summary>
    public ScenarioForm()
        : this(new Scenario())
    {
    }

    public ScenarioForm(Scenario scenario)
    {
        Attacker = new AttackerForm(scenario.Attacker);
        Defender = new DefenderForm(scenario.Defender);
        Context = new ContextForm(scenario.Context);
    }

    public AttackerForm Attacker { get; set; }

    public DefenderForm Defender { get; set; }

    public ContextForm Context { get; set; }

    /// <exception cref="FormatException">Si une expression de dés saisie est invalide.</exception>
    public Scenario ToScenario() => new()
    {
        Attacker = Attacker.ToAttackerUnit(),
        Defender = Defender.ToDefenderUnit(),
        Context = Context.ToAttackContext(),
    };
}
