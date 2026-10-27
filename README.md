# Wh40kSim : simulateur de dégâts Warhammer 40.000 (11e édition)

Un moteur Monte Carlo qui simule un grand nombre de fois une séquence d'attaque (profils d'armes contre une unité
cible) et en sort des statistiques, avec une application desktop Windows pour saisir les scénarios et lire les
résultats, et une console de debug.

## Roadmap

1. Moteur de simulation (11e édition) et console : **terminé**.
2. Interface graphique : application desktop Windows (MAUI Blazor Hybrid + MudBlazor) : **terminée** (`v0.2a`).
3. Graphiques et comparaison : distributions, probabilités cumulées P(≥ k), comparaison de profils côte à côte.
4. Traduction : anglais, choix de la langue, messages du Core.
5. Mots-clés du défenseur (vrai Anti-X…), règles d'armée et détachements, stockage des règles.
6. Gestion et sauvegarde des unités : bibliothèque, choix du loadout et des leaders.
7. Intégration web : API serveur + client web.

Détail des étapes et décisions : `CLAUDE.md`. Idées non planifiées : tickets GitHub du dépôt.

## Structure

```
src/Wh40kSim.Core            Règles du jeu, bibliothèque pure : modèles + résolution d'une séquence d'attaque
  Dice/                      Dés et expressions "D6+1"
  Model/                     Arme, unités, situation, scénario
  Engine/                    Séquence d'attaque (pools, touche, blessure, allocation, dégâts)
  Serialization/             Lecture / écriture des scénarios en JSON
src/Wh40kSim.Simulation      Monte Carlo : simulateur parallèle et déterministe, résultats agrégés en histogrammes
  Statistics/                Distributions : moyenne, écart-type, centiles, P(= k), P(≥ k), lignes de tableau
src/Wh40kSim.Console         Appli console de debug (saisie interactive ou fichier JSON) : affichage uniquement
src/Wh40kSim.Desktop         Application desktop Windows (MAUI Blazor Hybrid) : hôte de Wh40kSim.UI
src/Wh40kSim.UI              Interface Blazor + MudBlazor partagée par les hôtes (aucune règle de jeu, aucun calcul)
  Components/                AppShell (barre de titre, menu des thèmes) et page « Simulateur » : attaquant,
                             défenseur, situation, simulation, résultats
  Forms/                     Modèles de formulaire modifiables, convertis vers / depuis les records du Core
  Resources/                 Textes de l'interface (Strings.resx, français)
  Services/                  Simulation hors du thread de l'interface, ouverture / enregistrement des scénarios,
                             préférences (thème mémorisé)
tests/Wh40kSim.Core.Tests    Tests xUnit du Core (règles)
tests/Wh40kSim.Simulation.Tests  Tests xUnit de la simulation (moyennes analytiques, statistiques, non-régression)
tests/Wh40kSim.UI.Tests      Tests xUnit de l'interface (formulaires, services, composants avec bUnit)
scenarios/                   Scénarios d'exemple et scénarios enregistrés depuis la saisie interactive
```

## Prérequis et commandes

**Windows uniquement** (l'application desktop cible Windows 10 1809 ou plus récent).

- SDK .NET 10 (Rider le propose s'il manque).
- Charge de travail MAUI, nécessaire pour compiler la solution :
  `dotnet workload install maui-windows` (dans un terminal administrateur si .NET est installé dans
  `C:\Program Files\dotnet`). Elle s'installe pour chaque installation de .NET : si `dotnet --list-sdks` et Rider
  n'utilisent pas la même, l'installer dans les deux.
- Runtime WebView2 (fourni avec Windows 11 et Microsoft Edge, sinon : https://developer.microsoft.com/microsoft-edge/webview2/).

```bash
dotnet build
dotnet test
dotnet run --project src/Wh40kSim.Desktop                                            # application desktop
dotnet run --project src/Wh40kSim.Console                                            # console : saisie interactive
dotnet run --project src/Wh40kSim.Console -- scenarios/exemple-escouade-vs-leader.json -n 1000000 --seed 42
```

**Depuis Rider** : choisir la configuration d'exécution `Wh40kSim.Desktop` (profil « Windows Machine ») puis Run.
L'application n'est pas empaquetée (pas de Microsoft Store) : le `.exe` est dans
`src/Wh40kSim.Desktop/bin/<Configuration>/net10.0-windows10.0.19041.0/win-x64/`. Le lancer directement demande que
.NET soit trouvable par Windows (installé dans `C:\Program Files\dotnet`, sinon variable `DOTNET_ROOT`).

## Séquence d'attaque implémentée (règles 05 et 02.02.01, 11e édition)

**Boosts** (auras, stratagèmes, aptitudes) : l'attaquant et le défenseur ont chacun les leurs, que le moteur cumule.
Valeurs du point de vue de l'attaquant (+1 = mieux pour lui). Deux sortes, à ne pas confondre :
- **modificateurs aux jets** (touche, blessure) : ajoutés au résultat du dé, cumulés entre attaquant et défenseur,
  **plafonnés à ±1**, appliqués après les relances ;
- **modifications de caractéristiques** : CT/CC, A, F, PA, D pour l'attaquant ; couvert, CT/CC des attaquants, PA et
  dégâts des attaques reçues pour le défenseur. Elles changent le profil avant les jets, **sans plafond**, chaque
  caractéristique gardant ses bornes (CT/CC entre 2+ et 6+, F, A et D au moins 1, PA jamais au-dessus de 0). Les dégâts
  du défenseur s'appliquent avec la réduction de dégâts des groupes (après Melta, minimum 1, pas sur les mortelles).

0. **Pools** : les armes aux attaques identiques (même type, CT/CC, F, PA, D et règles, hors nom, A, Blast et Rapid Fire)
   forment un seul pool, comme bolters + pistolets bolter dans l'exemple des règles. Les pools sont résolus dans l'ordre de saisie.
1. **Attaques** : expression de dés par figurine, modifiée (minimum 1), + Blast (1 par tranche de 5 fig. ciblées), + Rapid Fire à demi-portée.
2. **Touche** : 1 naturel = échec, 6 naturel = critique, sinon résultat modifié ≥ CT/CC.
   Modificateurs de jet plafonnés à ±1, appliqués après les relances (Heavy inclus).
   CT/CC modifiée par l'attaquant et le défenseur ; **couvert : CT dégradée de 1** de plus (tir uniquement, sauf
   Ignores Cover) ; résultat borné entre 2+ et 6+. Torrent = touche auto.
   Critique : Sustained Hits X (touches en plus), Lethal Hits (blessure auto).
3. **Blessure** : table F/E (Force modifiée), modificateurs plafonnés à ±1 (Lance incluse), Twin-linked = relance des échecs.
   **Endurance (05.02.01)** recalculée avant chaque pool : la plus haute des gardes du corps encore en jeu,
   sinon la plus haute des figurines restantes.
4. **Sauvegardes (05.03 / 05.04)** : groupes d'allocation = un par PERSONNAGE, un par combinaison PV/Sv/InSv pour les autres
   (PERSONNAGES toujours en dernier). Tous les dés du pool sont jetés ensemble puis résolus du plus petit au plus grand
   sur le groupe courant, jusqu'à sa destruction. 1 naturel = dégâts ; résultat ≥ InSv ou résultat + PA (modifiée) ≥ Sv = attaque ratée.
5. **Dégâts** : expression de dés, modifiée (minimum 1), + Melta à demi-portée, − réduction de dégâts (min 1), puis FNP point par point.
   Dégâts excédentaires perdus ; une fois l'unité détruite, les attaques restantes sont perdues.
6. **Devastating Wounds (24.10)** : une blessure critique met fin à l'attaque et inflige D blessures mortelles,
   après les dégâts normaux du pool, sur une seule figurine (excédent perdu). Feel No Pain s'applique, pas la réduction de dégâts.
7. **Precision (24.28)** : le PERSONNAGE visé (premier en jeu, ou celui nommé dans `precisionTarget`) devient le groupe
   courant jusqu'à la fin du pool ou sa destruction ; les mortelles de Devastating Wounds vont d'abord sur lui.

Relances disponibles (touche et blessure) : aucune, les 1, les échecs, tout sauf critique (« pêche aux 6 »).

## Excédent (au-delà de la destruction de l'unité)

Les figurines tuées et les PV retirés suivent les règles : ils s'arrêtent à l'effectif et aux PV de l'unité, et la
dernière valeur du tableau vaut la probabilité de la détruire. Pour mesurer la marge, le simulateur calcule aussi des
distributions **prolongées** : une fois l'unité détruite, les attaques restantes continuent contre des figurines
virtuelles identiques au premier groupe non-PERSONNAGE (à défaut, le premier groupe), en nombre illimité. Ce n'est pas
le dernier tué, souvent le leader, unique et plus résistant. L'excédent de dégâts sur une figurine reste perdu.
Ces dés sont tirés à part : les résultats réels sont les mêmes que sans ce calcul. La console affiche la section
« Au-delà de l'unité » dès que l'unité peut être détruite.

La **marge** résume ces distributions : figurines virtuelles tuées et PV retirés en plus, en moyenne sur les
simulations où l'unité est détruite. Dans l'interface, les tuiles du haut restent les pertes réelles ; « Afficher la
marge » ajoute la marge sous leur moyenne, « +1,33 (Marge) », et prolonge les tableaux, dont les lignes au-delà de l'unité sont notées
« 5 + 2 » (les 5 figurines de l'unité, plus 2 virtuelles).

## Hypothèses restantes

- **Réduction de dégâts** : non appliquée aux blessures mortelles (elles ne sont pas une attaque allouée).
- **Precision** : le PERSONNAGE visé est supposé visible.
- **Ordre des pools** : celui de la saisie ; l'attaquant pourrait choisir un ordre plus favorable.
- **Anti-X** : en attendant les mots-clés du défenseur (phase 5), « Anti-Infantry 4+ » suppose que la cible est concernée.

## Format JSON d'un scénario

Voir `scenarios/exemple-escouade-vs-leader.json`. Les expressions de dés s'écrivent `2` ou `"D6+1"`,
la PA `-1`, une sauvegarde `3` pour 3+ (`7` = aucune), `"isCharacter": true` pour un leader,
`"toughness"` (Endurance) et `"wounds"` (PV) sur chaque groupe de figurines. Les commentaires `//` sont autorisés ;
une propriété inconnue ou mal placée est refusée au chargement.

Le fichier suit l'interface :
- `"attacker"` : `"name"`, `"boosts"` (`skill`, `hitRoll`, `hitReroll`, `strength`, `woundRoll`, `woundReroll`,
  `attacks`, `ap`, `damage`) et `"weapons"` ;
- `"defender"` : `"name"`, `"boosts"` (`inCover`, `skill`, `hitRoll`, `woundRoll`, `ap`, `damage`) et `"groups"` ;
- `"context"` : la situation seulement (`halfRange`, `remainedStationary`, `charged`, `precisionTarget`).

Un boost absent vaut 0 (ou `"None"` pour une relance, `false` pour le couvert).
