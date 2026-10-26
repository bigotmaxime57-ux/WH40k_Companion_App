# Wh40kSim : simulateur de dégâts Warhammer 40.000 (11e édition)

Phase 1 de la roadmap : un moteur Monte Carlo qui simule un grand nombre de fois
une séquence d'attaque (profils d'armes contre une unité cible) et en sort des statistiques.

## Structure

```
src/Wh40kSim.Core            Bibliothèque pure : modèles + moteur (réutilisée plus tard par l'API et l'UI)
  Dice/                      Dés et expressions "D6+1"
  Model/                     Arme, unités, situation, scénario
  Engine/                    Séquence d'attaque, simulateur parallèle, statistiques
  Serialization/             Lecture / écriture des scénarios en JSON
src/Wh40kSim.Console         Appli console de test (saisie interactive ou fichier JSON)
tests/Wh40kSim.Core.Tests    Tests xUnit
scenarios/                   Scénarios d'exemple
```

## Prérequis et commandes

SDK .NET 10 (Rider le propose s'il manque).

```bash
dotnet build
dotnet test
dotnet run --project src/Wh40kSim.Console                                            # saisie interactive
dotnet run --project src/Wh40kSim.Console -- scenarios/exemple-escouade-vs-leader.json -n 1000000 --seed 42
```

## Séquence d'attaque implémentée (règles 05 et 02.02.01, 11e édition)

0. **Pools** : les armes aux attaques identiques (même type, CT/CC, F, PA, D et règles, hors nom, A, Blast et Rapid Fire)
   forment un seul pool, comme bolters + pistolets bolter dans l'exemple des règles. Les pools sont résolus dans l'ordre de saisie.
1. **Attaques** : expression de dés par figurine (minimum 1), + Blast (1 par tranche de 5 fig. ciblées), + Rapid Fire à demi-portée.
2. **Touche** : 1 naturel = échec, 6 naturel = critique, sinon résultat modifié ≥ CT/CC.
   Modificateurs de jet plafonnés à ±1, appliqués après les relances (Heavy inclus).
   **Couvert : CT dégradée de 1** (tir uniquement, sauf Ignores Cover), CT bornée entre 2+ et 6+. Torrent = touche auto.
   Critique : Sustained Hits X (touches en plus), Lethal Hits (blessure auto).
3. **Blessure** : table F/E, modificateurs plafonnés à ±1 (Lance incluse), Twin-linked = relance des échecs.
   **Endurance (05.02.01)** recalculée avant chaque pool : la plus haute des gardes du corps encore en jeu,
   sinon la plus haute des figurines restantes.
4. **Sauvegardes (05.03 / 05.04)** : groupes d'allocation = un par PERSONNAGE, un par combinaison PV/Sv/InSv pour les autres
   (PERSONNAGES toujours en dernier). Tous les dés du pool sont jetés ensemble puis résolus du plus petit au plus grand
   sur le groupe courant, jusqu'à sa destruction. 1 naturel = dégâts ; résultat ≥ InSv ou résultat + PA ≥ Sv = attaque ratée.
5. **Dégâts** : expression de dés (minimum 1), + Melta à demi-portée, − réduction de dégâts (min 1), puis FNP point par point.
   Dégâts excédentaires perdus ; une fois l'unité détruite, les attaques restantes sont perdues.
6. **Devastating Wounds (24.10)** : une blessure critique met fin à l'attaque et inflige D blessures mortelles,
   après les dégâts normaux du pool, sur une seule figurine (excédent perdu). Feel No Pain s'applique, pas la réduction de dégâts.
7. **Precision (24.28)** : le PERSONNAGE visé (premier en jeu, ou celui nommé dans `precisionTarget`) devient le groupe
   courant jusqu'à la fin du pool ou sa destruction ; les mortelles de Devastating Wounds vont d'abord sur lui.

Relances disponibles (touche et blessure) : aucune, les 1, les échecs, tout sauf critique (« pêche aux 6 »).

## Hypothèses restantes

- **Réduction de dégâts** : non appliquée aux blessures mortelles (elles ne sont pas une attaque allouée).
- **Precision** : le PERSONNAGE visé est supposé visible.
- **Ordre des pools** : celui de la saisie ; l'attaquant pourrait choisir un ordre plus favorable.
- **Anti-X** : en attendant les mots-clés du défenseur (phase 3), « Anti-Infantry 4+ » suppose que la cible est concernée.

## Format JSON d'un scénario

Voir `scenarios/exemple-escouade-vs-leader.json`. Les expressions de dés s'écrivent `2` ou `"D6+1"`,
la PA `-1`, une sauvegarde `3` pour 3+ (`7` = aucune), `"isCharacter": true` pour un leader,
`"toughness"` (Endurance) et `"wounds"` (PV) sur chaque groupe de figurines. Les commentaires `//` sont autorisés ;
une propriété inconnue ou mal placée est refusée au chargement.
