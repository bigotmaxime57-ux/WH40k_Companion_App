# Wh40kSim : simulateur de dégâts Warhammer 40.000 (11e édition)

Un moteur Monte Carlo qui simule un grand nombre de fois une séquence d'attaque (profils d'armes contre une unité
cible) et en sort des statistiques, avec une application desktop Windows pour saisir les scénarios et lire les
résultats, et une console de debug.

## Roadmap

1. Moteur de simulation (11e édition) et console : **terminé**.
2. Interface graphique : application desktop Windows (MAUI Blazor Hybrid + MudBlazor) : **terminée** (`v0.2a`).
3. Graphiques et Full sim : écran d'accueil, Quick sim, Full sim (écran de saisie, puis résultats en onglets :
   histogrammes, puis rapports par groupe et par arme), overwatch. En cours.
4. Intégration web : API serveur + client web. Les phases suivantes livrent aussi leur partie web.
5. Comparaison de plusieurs attaquants ou de plusieurs cibles.
6. Optimiseur de l'ordre des armes.
7. Traduction : anglais, choix de la langue, messages du Core.
8. Mots-clés du défenseur (vrai Anti-X…), règles d'armée et détachements, stockage des règles.
9. Gestion et sauvegarde des unités : bibliothèque, choix du loadout et des leaders, rapport en points.

Détail des étapes et décisions : `CLAUDE.md`. Idées non planifiées : tickets GitHub du dépôt.

## Structure

```
src/Wh40kSim.Core            Règles du jeu, bibliothèque pure : modèles + résolution d'une séquence d'attaque
  Dice/                      Dés (générateur xoshiro256**, sans biais) et expressions "D6+1"
  Model/                     Arme, unités, situation, scénario
  Engine/                    Séquence d'attaque (pools, touche, blessure, allocation, dégâts)
  Serialization/             Lecture / écriture des scénarios en JSON
src/Wh40kSim.Simulation      Monte Carlo : simulateur parallèle et déterministe, résultats agrégés en histogrammes
  Statistics/                Distributions : moyenne, écart-type, centiles, P(= k), P(≥ k), lignes de tableau
src/Wh40kSim.Console         Appli console de debug (saisie interactive ou fichier JSON) : affichage uniquement
src/Wh40kSim.Desktop         Application desktop Windows (MAUI Blazor Hybrid) : hôte de Wh40kSim.UI
src/Wh40kSim.Web             Serveur web : page du client, API de simulation (POST /api/simulations, progression en
                             Server-Sent Events, file d'attente, limites réglables dans appsettings.json)
src/Wh40kSim.Web.Client      Client web (Blazor WebAssembly) : l'interface de Wh40kSim.UI dans le navigateur
                             (simulation par l'API, fichiers ouverts / téléchargés, préférences du navigateur)
src/Wh40kSim.UI              Interface Blazor + MudBlazor partagée par les hôtes (aucune règle de jeu, aucun calcul)
  Components/                AppShell (barre de titre, menu des thèmes, routeur), accueil, écrans « Quick sim » et
                             « Full sim » (saisie, résultats en onglets) : attaquant, défenseur, situation,
                             simulation, résultats, histogrammes SVG
  Forms/                     Modèles de formulaire modifiables, convertis vers / depuis les records du Core
  Resources/                 Textes de l'interface (Strings.resx, français)
  Services/                  Simulation hors du thread de l'interface, ouverture / enregistrement des scénarios,
                             préférences (thème mémorisé), scénario courant partagé par les écrans
tests/Wh40kSim.Core.Tests    Tests xUnit du Core (règles)
tests/Wh40kSim.Simulation.Tests  Tests xUnit de la simulation (moyennes analytiques, statistiques, non-régression)
tests/Wh40kSim.UI.Tests      Tests xUnit de l'interface (formulaires, services, composants avec bUnit)
tests/Wh40kSim.Web.Tests     Tests d'intégration du serveur (API et page, WebApplicationFactory)
tests/Wh40kSim.Web.Client.Tests  Tests du client web (API face à un faux serveur, fichiers, préférences)
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
dotnet run --project src/Wh40kSim.Web                                                # version web sur http://localhost:5080
```

**Version web** : lancer `Wh40kSim.Web` (profil « http » depuis Rider, ou la commande ci-dessus) ouvre le site dans le
navigateur. Le calcul se fait sur le serveur : 1 000 000 de simulations au plus par lancement, un calcul à la fois
(une fenêtre indique la place dans la file), 10 lancements par minute et par adresse IP (réglables dans
`src/Wh40kSim.Web/appsettings.json`). Les scénarios s'ouvrent avec le sélecteur de fichier du navigateur et s'enregistrent
par un téléchargement ; le thème est mémorisé par le navigateur. L'API seule s'essaie depuis Rider avec
`src/Wh40kSim.Web/Wh40kSim.Web.http`. Hors de l'environnement Development, le site se lance depuis une version publiée
(`dotnet publish`) : c'est là que les fichiers du client et de MudBlazor sont rassemblés.

**Depuis Rider** : choisir la configuration d'exécution `Wh40kSim.Desktop` (profil « Windows Machine ») puis Run.
L'application n'est pas empaquetée (pas de Microsoft Store) : le `.exe` est dans
`src/Wh40kSim.Desktop/bin/<Configuration>/net10.0-windows10.0.19041.0/win-x64/`. Le lancer directement demande que
.NET soit trouvable par Windows (installé dans `C:\Program Files\dotnet`, sinon variable `DOTNET_ROOT`).

## Distribution (versions de test)

Un installateur (`Setup.exe`, Inno Setup) : l'application est autonome (.NET et Windows App SDK inclus), seul
WebView2 est requis chez l'utilisateur. Installation pour l'utilisateur seul, sans droits administrateur
(`%LOCALAPPDATA%\Programs\Wh40kSim`), raccourcis dans le menu Démarrer (et sur le bureau si coché), scénarios d'exemple
copiés dans `Documents\Wh40kSim\scenarios` (jamais écrasés ni supprimés), désinstallation depuis les paramètres de
Windows. Une nouvelle version remplace la précédente.

```powershell
winget install --id JRSoftware.InnoSetup -e --scope user                       # une fois par machine
powershell -ExecutionPolicy Bypass -File tools\publier.ps1                   # scénarios versionnés seulement
powershell -ExecutionPolicy Bypass -File tools\publier.ps1 -LocalScenarios   # tout le dossier scenarios local
```

Le script publie avec le profil `win-x64` (`src/Wh40kSim.Desktop/Properties/PublishProfiles/`), ne garde que les
traductions française et anglaise des bibliothèques, puis compile `tools/installateur.iss` (avec `tools/LISEZMOI.txt`
et les scénarios) en `dist/Wh40kSim-<version>-setup.exe` (environ 75 Mo).
La version vient de `<InformationalVersion>` dans `Wh40kSim.Desktop.csproj` et s'affiche en bas de l'accueil.
L'installateur n'est pas signé : au premier lancement, Windows SmartScreen demande « Exécuter quand même ».
Les versions de test sont publiées en préversion dans les Releases GitHub du dépôt.

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
3. **Blessure** : table F/E (Force modifiée), modificateurs plafonnés à ±1 (Lance incluse), Twin-linked = relance complète du jet de blessure.
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

Relances disponibles (touche et blessure) : aucune, les 1, relance complète (`Failed` dans le JSON), qui relance les
échecs. **Pêche aux critiques**, arme par arme (`fishForCriticalHits`, `fishForCriticalWounds` sur l'arme) : avec la relance
complète, l'arme relance aussi ses réussites ordinaires pour chercher un critique. Elle n'agit que si elle a un sens :
à la touche, une arme Lethal Hits ou Sustained Hits (ni Torrent, ni en overwatch) ; à la blessure, une arme
Devastating Wounds, la relance complète venant des boosts ou de Twin-linked. Deux armes identiques qui font un choix
différent forment deux pools. L'ancienne relance globale « tout sauf critique » (`NonCritical`) est refusée.

**Tir en overwatch** (interrupteur de la Situation, `snapShooting` dans le JSON) : l'Overwatch de la 11e édition (15.08)
tire en snap shooting (15.09). Seules les armes de tir tirent (les armes de mêlée restent dans le scénario, un
avertissement le signale) ; chaque attaque ne touche que sur un 6 non modifié, quels que soient la CT et tous les
modificateurs (couvert et boosts de touche compris), sans relance, et ces touches ne sont pas critiques (ni Lethal Hits,
ni Sustained Hits, « touche critique sur X+ » sans effet). Torrent touche toujours ; la suite de la séquence est
inchangée (Devastating Wounds compris). « Immobile » et « a chargé » sont sans effet. Le simulateur ne vérifie pas si le
tir est permis (cible visible à 24" ou moins, unité non engagée et non TITANIC) : c'est au joueur de s'en assurer.

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

## Dégâts bruts et gaspillage

Les **dégâts bruts** comptent en entier les dégâts de chaque attaque non sauvegardée (et de chaque salve de
blessures mortelles), après réduction de dégâts et Feel No Pain, sans plafond par figurine ni arrêt à la destruction
de l'unité (les figurines virtuelles de l'excédent comptent aussi). Le **gaspillage** est la différence avec les PV
retirés, excédent compris : un D3 qui fait 3 sur une figurine à 1 PV gaspille 2. Ce qui dépasse la destruction de
l'unité n'en fait pas partie : c'est la marge. Aucun dé supplémentaire n'est tiré. Affichés par la console (section
« Dégâts bruts ») ; dans l'interface au Full sim.

## Hypothèses restantes

- **Réduction de dégâts** : non appliquée aux blessures mortelles (elles ne sont pas une attaque allouée).
- **Precision** : le PERSONNAGE visé est supposé visible.
- **Ordre des pools** : celui de la saisie ; l'attaquant pourrait choisir un ordre plus favorable.
- **Anti-X** : en attendant les mots-clés du défenseur (phase 8), « Anti-Infantry 4+ » suppose que la cible est concernée.

## Format JSON d'un scénario

Voir `scenarios/exemple-escouade-vs-leader.json`. Les expressions de dés s'écrivent `2` ou `"D6+1"`,
la PA `-1`, une sauvegarde `3` pour 3+ (`7` = aucune), `"isCharacter": true` pour un leader,
`"toughness"` (Endurance) et `"wounds"` (PV) sur chaque groupe de figurines. Les commentaires `//` sont autorisés ;
une propriété inconnue ou mal placée est refusée au chargement.

Le fichier suit l'interface :
- `"attacker"` : `"name"`, `"boosts"` (`skill`, `hitRoll`, `hitReroll`, `strength`, `woundRoll`, `woundReroll`,
  `attacks`, `ap`, `damage`) et `"weapons"` ;
- `"defender"` : `"name"`, `"boosts"` (`inCover`, `skill`, `hitRoll`, `woundRoll`, `ap`, `damage`) et `"groups"` ;
- `"context"` : la situation seulement (`halfRange`, `remainedStationary`, `charged`, `snapShooting`, `precisionTarget`).

Un boost absent vaut 0 (ou `"None"` pour une relance, `false` pour le couvert).

Points (facultatifs) : `"points"` sur l'attaquant (coût de l'unité) et sur chaque groupe du défenseur (coût du groupe
entier : un leader a son propre coût). Les points détruits se comptent au prorata des PV retirés (la moitié des PV d'un
char vaut la moitié de ses points), seulement si chaque groupe a ses points ; le rendement les rapporte aux points de
l'attaquant. Saisie et affichage dans l'interface prévus avec la gestion des unités (phase 9) ; en attendant, à écrire
dans le fichier (l'interface les garde à l'enregistrement), résultats affichés par la console.

Un scénario complet contient `"attacker"` et `"defender"` (`"context"` facultatif). Un fichier peut aussi ne contenir
qu'une partie, pour la réutiliser dans d'autres scénarios : `{ "attacker": { … } }` (fichier attaquant) ou
`{ "defender": { … } }` (fichier défenseur), boosts compris. Dans le Quick sim, les boutons des cartes Attaquant et
Défenseur ouvrent et enregistrent une partie ; l'attaquant ou le défenseur d'un scénario complet peut aussi être repris.
Sont refusés : un fichier sans attaquant ni défenseur, une situation (`"context"`) hors d'un scénario complet. La
console n'accepte que les scénarios complets.
