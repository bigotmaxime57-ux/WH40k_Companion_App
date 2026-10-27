# Wh40kSim : consignes pour Claude Code

Ce fichier est lu automatiquement par Claude Code au début de chaque session. Il garde les règles, la roadmap et les
décisions du projet, pour reprendre le travail sur n'importe quelle machine. **Le tenir à jour** : l'avancement est mis
à jour à la fin de chaque étape, dans le commit de l'étape.

**Deux machines.** L'utilisateur travaille en alternance sur deux PC Windows. Ce fichier (versionné) contient
uniquement ce qui vaut pour les deux : règles, décisions, avancement. Ce qui est propre à une machine (chemin du SDK,
charges de travail installées, identité git, réglages Windows…) va dans la mémoire utilisateur de Claude Code de
cette machine (`~/.claude/projects/<projet>/memory/`), jamais dans le dépôt. En début de session : `git fetch` et
vérifier que `main` est à jour avant toute chose ; en fin de session : tout pousser, et mettre à jour
« Avancement » si l'état a changé (étape en cours, branche non fusionnée, point en suspens).

Voir aussi `README.md` (structure de la solution, règles de jeu implémentées, format JSON).

## Règles de travail

- **Core pur** : aucune règle de jeu dans l'UI, aucune dépendance UI dans le Core. L'UI ne fait qu'appeler le Core
  (`Scenario.Validate`, `DiceExpression.Parse`, `Simulator.Run`…).
- **Langue** : interface et commentaires en français, noms de code (types, méthodes, propriétés) en anglais.
- **Ne rien coder sans feu vert explicite** pour une étape. Les étapes sont décrites à l'avance (voir plus bas) ;
  signaler avant de commencer tout point qui contredit le code existant ou demande une décision.
- **Après chaque étape** : `dotnet build` et `dotnet test` sans erreur ni avertissement, puis un commit dédié.
- **En fin d'étape**, expliquer brièvement ce qui a été créé et pourquoi, en expliquant les notions C# / Blazor
  introduites (sans faire un cours).
- **Versions de paquets** : vérifier sur NuGet la dernière version stable compatible .NET 10 avant de référencer un paquet.
- **Wh40kSim.Console** est conservé comme outil de debug : ne pas le supprimer, le garder compilable.
- **Ne pas anticiper les phases suivantes** pendant une phase (ne pas la surcharger).

## Git

- Une branche par étape, créée depuis `main` à jour (ex. `phase2a/etape3-composants`).
- Un ou quelques commits cohérents par étape, messages en français, terminés par la ligne `Co-Authored-By` de Claude.
- Présenter l'étape, puis fusionner dans `main` **uniquement après accord** de l'utilisateur, **toujours avec
  `git merge --no-ff`** (l'historique est suivi dans SourceTree, les branches doivent rester visibles ;
  `git config merge.ff false` est aussi réglé dans la config locale du dépôt, sur chaque machine).
- Après le merge : build + tests sur `main`, `git push origin main`, puis suppression de la branche.
- Jamais de réécriture d'historique publié (`push --force`) sans demande explicite.
- `Wh40kSim.sln` : ne pas utiliser `dotnet sln add` (il ajoute des dossiers de solution, des plateformes x64/x86 et un BOM).
  Ajouter les projets à la main dans le style existant : type de projet `{9A19103F-16F7-4668-BE54-9A1E7A4F7556}`,
  configurations `Any CPU` seulement, fins de ligne LF.

## Roadmap

- **Phase 1** (terminée) : moteur de simulation Monte Carlo (11e édition) + console pour l'interroger.
- **Phase 2** (en cours) : remplacement de la console par une interface graphique.
  - **2a** : desktop (MAUI Blazor Hybrid + MudBlazor), étapes détaillées ci-dessous ;
  - **2b** : web local ;
  - **2c** : API serveur + client web.
- **Phase 3** : graphiques. Distributions façon WarpHammer Math : histogrammes, probabilités cumulées P(≥ k),
  comparaison de plusieurs profils côte à côte.
- **Phase 4** : mots-clés, règles d'armée, détachements. Mots-clés du défenseur (vrai Anti-X…), règles d'armée et
  détachements, avec un stockage des règles.
- **Phase 5** : gestion et sauvegarde des unités. Bibliothèque d'unités, choix du loadout et des leaders.
- **Phase « Traduction »** (place à définir) : anglais, choix de la langue, messages du Core.

Le README parle encore de « phase 3 » pour les mots-clés : c'est l'ancienne numérotation (à corriger à l'étape 4).

## Phase 2a : étapes

1. **Core : progression.** Paramètre optionnel `IProgress<double>` sur `Simulator.Run` (0 → 1, rapporté par paquet),
   déterminisme à graine égale conservé ; tests.
2. **Wh40kSim.UI : services et modèles de formulaire.** Razor Class Library + MudBlazor ; `ISimulationService` /
   `LocalSimulationService` (Core exécuté hors du thread UI) ; `IScenarioFileService` implémenté par l'hôte ;
   formulaires modifiables (`ScenarioForm`…) avec `ToScenario()` et construction depuis un `Scenario` ;
   projet `Wh40kSim.UI.Tests` (allers-retours).
3. **Composants : page « Simulateur ».**
   - Attaquant : liste de profils d'armes (ajout / suppression / duplication) ; nom, tir/mêlée, nombre de figurines,
     A, CT/CC, F, PA, D (A et D acceptent "D6+1") ; mots-clés en cases à cocher / champs numériques (Torrent,
     Lethal Hits, Sustained Hits X, Devastating Wounds, Twin-linked, Blast, Rapid Fire X, Melta X, Heavy, Lance,
     Ignores Cover, Precision, touche / blessure critique sur X+).
   - Défenseur : nom, couvert, modificateurs ; liste **ordonnée** des groupes d'allocation (monter / descendre), avec
     nom, PERSONNAGE, nombre, PV, Endurance, Sv, InSv, FNP, réduction de dégâts. **Pas d'Endurance au niveau du
     défenseur** : elle est obligatoire sur chaque groupe (refactor du Core) ; un nouveau groupe reprend celle du précédent.
   - Situation : demi-portée / immobile / a chargé affichés seulement si une arme est concernée ; modificateurs ;
     relances ; cible de Precision s'il y a plusieurs PERSONNAGES et au moins une arme Precision.
     Pour ne pas mettre de règle dans l'UI, ajouter au Core des propriétés en lecture seule sur `WeaponProfile` :
     `UsesHalfRange` (Rapid Fire, Melta), `UsesRemainedStationary` (Heavy sur arme de tir), `UsesCharged` (Lance),
     utilisées par le moteur, la console et l'UI. Version minimale : la vraie gestion des mots-clés reste en phase 4.
   - Erreurs de `Scenario.Validate()` affichées clairement ; erreur de saisie de dés affichée sous le champ concerné ;
     bouton Lancer désactivé tant que le scénario est invalide.
   - Panneau de simulation : nombre de simulations (défaut 1 000 000), graine optionnelle, Lancer, barre de
     progression, Annuler.
   - Résultats en texte et tableaux (pas de graphique, c'est la phase 3) : les mêmes informations que `ReportPrinter`
     (moyennes, écarts-types, médianes, fourchette 10-90 %, unité détruite, pertes par groupe, tableaux P(= k) et
     P(≥ k) pour les figurines tuées et les PV retirés, lignes négligeables masquées aux extrémités).
   - Boutons Ouvrir / Enregistrer (JSON compatible avec `scenarios/`) et Nouveau.
   - Textes de l'interface via `IStringLocalizer` + `.resx` (voir Traduction).
   - Tests de composants avec bUnit : Lancer désactivé si invalide, situation conditionnelle, monter / descendre.
4. **Wh40kSim.Desktop (MAUI Blazor Hybrid).**
   - Vérifier `dotnet workload list` (sinon indiquer `dotnet workload install maui-windows`).
   - Projet MAUI Blazor Hybrid qui affiche les composants de Wh40kSim.UI dans un `BlazorWebView`.
   - Cible Windows uniquement : `net10.0-windows10.0.19041.0`, `WindowsPackageType=None` (non empaqueté).
     macOS écarté (compilation impossible sous Windows, non testable). `Directory.Build.props` impose
     `TargetFramework=net10.0` : le projet redéfinit `TargetFramework` (au singulier).
   - Enregistre `LocalSimulationService` et un `IScenarioFileService` : `FilePicker` de MAUI pour ouvrir (ne permet pas
     de choisir le dossier de départ), `FileSaver` de CommunityToolkit.Maui pour enregistrer (dossier de départ
     `scenarios/` s'il est trouvé).
   - Thème MudBlazor qui suit Windows au démarrage + bouton de bascule clair / sombre ; fenêtre ~1400 × 900 centrée,
     redimensionnable avec une taille minimale ; titre « Wh40kSim – Simulateur de dégâts ».
   - Projet ajouté à la solution ; README : prérequis (workload MAUI, WebView2), lancement depuis Rider et en ligne
     de commande, Windows uniquement ; roadmap du README renumérotée.
   - Fin de phase 2a : tag `v0.2a`.

## Décisions techniques

- **Hôte desktop : MAUI Blazor Hybrid**, pas Photino.Blazor. Photino.Blazor 4.0.13 (janvier 2025) ne cible que
  net8 / net9 et le support .NET 10 n'est pas publié (issue #164, PR #168 ouvertes) ; MAUI est maintenu par Microsoft.
- **Versions** (vérifiées le 2026-10-26) : MudBlazor 9.11.0, Microsoft.AspNetCore.Components.Web 10.0.14,
  CommunityToolkit.Maui 15.0.1, bUnit 2.13.3, MAUI 10.0.1xx. Tests xUnit : mêmes versions que `Wh40kSim.Core.Tests`.
- **Formulaires (Wh40kSim.UI/Forms)** : classes modifiables avec un constructeur depuis le record du Core, un
  constructeur vide (= valeurs par défaut du Core, rien n'est recopié dans l'UI) et une méthode `ToXxx()`.
  `WeaponForm` regroupe `WeaponGroup` + `WeaponProfile`. Expressions de dés en texte, lues par le Core.
  Sustained Hits / Rapid Fire / Melta : case à cocher + valeur séparées. `ToScenario()` lève `FormatException` si une
  expression de dés est invalide.
- **IScenarioFileService** : `OpenAsync()` → `OpenedScenario(FileName, Scenario)` ou null si annulé ;
  `SaveAsync(scenario, suggestedFileName)` → nom du fichier écrit ou null si annulé.
- **Traduction** préparée dès la phase 2 : textes de l'UI dans des `.resx` (français seulement pour l'instant), nombres
  formatés selon la culture de l'appli, fixée à fr-FR (pas celle de la machine). Messages du Core et anglais : phase
  Traduction. Traduction du contenu (règles d'armée, unités) : à trancher en phase 4.
- **Tests aller-retour** : comparer les scénarios via `ScenarioJson.Serialize` (les records contenant des listes ne sont
  pas égaux par valeur).

## Environnement de développement

- SDK .NET 10 ; IDE Rider ; historique suivi dans SourceTree. Desktop développé et testé sous Windows uniquement.
- Charge de travail MAUI : `dotnet workload install maui-windows` (à faire sur chaque machine).
- Si .NET est installé dans le dossier utilisateur plutôt que dans `C:\Program Files\dotnet`, lancer un `.exe`
  directement échoue (0x80008083, runtime introuvable) sans la variable `DOTNET_ROOT` ; `dotnet run` et Rider fonctionnent.
- Si les chemins longs Windows sont désactivés (`LongPathsEnabled = 0`), le build MAUI échoue au-delà de 260
  caractères : ne pas placer le dépôt dans un dossier trop profond (dans le dépôt, le chemin le plus long fait ~180).
- Le modèle `dotnet new maui-blazor` cible aussi Android / iOS : ne garder que la cible Windows.
- Pas de `.gitattributes` : avec `core.autocrlf=true`, la copie de travail est en CRLF et l'index en LF. La règle
  « fins de ligne LF » du `.sln` porte sur le contenu versionné (vérifier avec `git ls-files --eol`).

## Avancement

- Phase 2a, étape 1 (progression) : fusionnée dans `main`.
- Phase 2a, étape 2 (Wh40kSim.UI, services, formulaires) : fusionnée dans `main`.
- **Prochaine : étape 3 (composants)**, en attente du feu vert.

État vérifié le 2026-10-27 (deuxième machine, `main` à `c062271`) : build sans avertissement, 143 tests verts
(110 Core, 33 UI). Wh40kSim.UI ne contient encore que `Forms/` et `Services/` : aucun composant `.razor`, pas de
`.resx`, pas de bUnit.

Points relevés à la relecture, à trancher au début de l'étape 3 :
- **Endurance par groupe** : déjà faite (commit `c6f233b`) : `ModelGroup.Toughness`, rien sur `DefenderUnit`, JSON
  strict. Mais elle a une valeur par défaut (4) : un groupe JSON sans `toughness` reçoit E4 sans erreur. Le
  « refactor du Core » prévu à l'étape 3 se réduit donc à décider si elle devient vraiment obligatoire (`required`).
- **Ancienne numérotation** : « phase 3 » pour les mots-clés du défenseur subsiste aussi dans les commentaires de
  `WeaponAbilities.cs` (dont un qui cite Precision comme non géré, alors qu'il l'est), pas seulement dans le README.
- `UsesHalfRange` / `UsesRemainedStationary` / `UsesCharged` n'existent pas encore (prévu à l'étape 3).
