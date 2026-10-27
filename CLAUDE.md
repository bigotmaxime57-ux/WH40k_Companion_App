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
- **« voir sc »** = regarder la capture d'écran que l'utilisateur a mise dans le presse-papiers. L'enregistrer dans le
  scratchpad (PowerShell : `[System.Windows.Forms.Clipboard]::GetImage().Save(<chemin>.png, [System.Drawing.Imaging.ImageFormat]::Png)`
  après `Add-Type -AssemblyName System.Windows.Forms, System.Drawing`), puis la lire comme une image.

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

## Backlog : GitHub Issues

Les idées non planifiées, bugs et points d'outillage sont des **tickets GitHub** du dépôt
(`bigotmaxime57-ux/WH40k_Companion_App`), pas des listes dans ce fichier. CLAUDE.md ne garde que la roadmap décidée.

- Labels : `idée` (fonctionnalité non planifiée), `outillage` (build, CI, configuration du dépôt), `bug`,
  `dette technique`. Pas de milestones : quand une idée entre dans une phase, elle est décrite dans la roadmap
  ci-dessous, qui cite le ticket, et le ticket est fermé par le commit qui la réalise.
- **Claude crée un ticket sans redemander dès qu'une idée est validée en discussion** (autorisation donnée le
  2026-10-27) : titre court, contexte de la discussion, phase concernée, points à trancher.
- Un commit qui réalise un ticket le référence (`Closes #n`) ; consulter les tickets ouverts avant de planifier une
  phase (`gh issue list`).
- Outil : CLI GitHub `gh`, à installer et authentifier sur chaque machine (voir « Environnement de développement »).

## Roadmap

Chaque phase garde une ligne **Objectif** : sa formulation d'origine, qui ne se modifie qu'avec l'accord de
l'utilisateur. Le découpage en étapes peut évoluer ; celui des phases futures est indicatif et sera précisé au début
de chaque phase. Numérotation fixée le 2026-10-27 : le README et des commentaires du code suivent encore l'ancienne
(mots-clés en « phase 3 »), à corriger à l'étape 6 de la phase 2a.

### État de l'existant (2026-10-27)

- **Core** : expressions de dés (`D6+1`) ; modèle (armes et mots-clés d'arme, unités, groupes d'allocation, situation) ;
  séquence d'attaque 11e édition (pools, couvert sur la CT, Endurance recalculée à chaque pool 05.02.01, allocation
  05.03, Precision, Devastating Wounds, FNP, réduction de dégâts, relances) ; simulation parallèle déterministe à graine
  égale, avec progression et annulation ; JSON strict (les anciens formats sont refusés).
- **Console** : saisie interactive ou fichier JSON, rapport texte.
- **Wh40kSim.UI** : formulaires modifiables (allers-retours testés), services de simulation et de fichiers ; aucun
  composant visuel.
- **Hypothèses restantes** : réduction de dégâts non appliquée aux mortelles ; cible de Precision supposée visible ;
  ordre des pools = ordre de saisie ; Anti-X approché par « blessure critique sur X+ ».
- 143 tests verts (110 Core, 33 UI), build sans avertissement.

### Phase 1 : moteur de simulation (terminée)

**Objectif** : moteur de simulation Monte Carlo (11e édition) + console pour l'interroger.

### Phase 2 : interface graphique (en cours)

**Objectif** : remplacement de la console par une interface graphique.

- **2a : desktop** (MAUI Blazor Hybrid + MudBlazor) : étapes détaillées dans « Phase 2a : étapes ».
- **2b : API + client web.** Le « web local » prévu à l'origine est abandonné (2026-10-27) : le desktop couvre l'usage
  local, et ce qu'il devait apporter (fichiers côté navigateur, Wh40kSim.UI dans un hôte web) est repris ici.
  Structure Blazor Web App : `Wh40kSim.Web` (serveur ASP.NET Core : API + service du client) et
  `Wh40kSim.Web.Client` (WebAssembly).
  1. **Contrat et API** (Minimal API) : contrat JSON du résultat, `POST` d'un scénario validé par `Scenario.Validate`
     (erreurs en `ProblemDetails`), plafond d'itérations, tests d'intégration (`WebApplicationFactory`).
  2. **Progression et annulation** : progression envoyée au client (Server-Sent Events), annulation si le client se
     déconnecte.
  3. **Client WebAssembly** : `ApiSimulationService` implémente `ISimulationService` ; `IScenarioFileService` côté
     navigateur (`InputFile`, téléchargement) ; composants de Wh40kSim.UI inchangés.
  4. **Finitions** : configuration, README, tag `v0.2b`. Hébergement public hors périmètre sauf décision contraire.

### Phase 3 : traduction

**Objectif** : anglais, choix de la langue, messages du Core.

Placée avant les mots-clés : la phase 5 ajoutera beaucoup de messages au Core, autant qu'ils soient déjà sous forme
de codes.

1. **Messages du Core sous forme de codes** : `Validate()` renvoie des erreurs typées (code + paramètres) traduites par
   l'UI ; idem pour les erreurs de lecture JSON et de dés.
2. **Anglais** : `.resx` anglais, choix de la langue dans l'interface (mémorisé), nombres formatés selon la langue.

### Phase 4 : graphiques et comparaison

**Objectif** : « Affichage des distributions façon WarpHammer Math : histogrammes, probabilités cumulées P(≥ k) et
comparaison de plusieurs profils côte à côte » (qui tue le mieux l'adversaire).

Tous les chiffres viennent de `Wh40kSim.Simulation` ; les graphiques ne font que les dessiner, dans Wh40kSim.UI
(desktop et web en profitent).

1. **Graphiques d'un résultat** : choix de la bibliothèque (`MudChart` ou bibliothèque dédiée, à vérifier en début de
   phase) ; histogramme P(= k) et courbe P(≥ k) pour les figurines tuées et les PV retirés, à partir des mêmes lignes
   que les tableaux (`DiscreteDistribution`) ; les tableaux restent.
2. **Comparaison, côté Simulation** : une cible et une situation communes, N profils d'attaquant nommés ; simulation de
   chacun (progression globale, annulation) ; résultats alignés sur le même axe ; indicateurs calculés dans
   Simulation : moyenne, P(détruire l'unité), P(≥ k) pour un k choisi, meilleur profil pour chacun (pas de « meilleur »
   unique). Format JSON d'une comparaison (enregistrer / rouvrir).
3. **Comparaison, côté UI** : profils ajoutés depuis le scénario courant, par duplication ou depuis un JSON ; courbes
   P(≥ k) superposées et histogrammes ; tableau côte à côte, meilleur profil mis en évidence par indicateur.

### Phase 5 : mots-clés, règles d'armée, détachements

**Objectif** : mots-clés du défenseur (vrai Anti-X…), règles d'armée et détachements, avec un stockage des règles.

1. **Mots-clés du défenseur** (INFANTERIE, VÉHICULE, MONSTRE… ; PERSONNAGE reste sur les groupes) ; vrai Anti-X ;
   évolution compatible du JSON et de l'UI.
2. **Mots-clés d'arme restants** influant sur les dégâts : liste à établir ensemble d'après les règles 11e édition.
3. **Moteur d'effets** : catalogue d'effets typés dans le Core (relances, modificateurs, PA, critiques sur X+, FNP,
   réduction de dégâts…), chacun avec une condition (a chargé, contre tel mot-clé, demi-portée…) et une étape de la
   séquence d'attaque où il s'applique. Étape la plus structurante de la roadmap.
4. **Stockage des règles** : règles d'armée, détachements et stratagèmes en JSON (`data/`), qui référencent les effets
   du catalogue ; service de catalogue. On stocke la mécanique, pas le texte des règles de GW.
5. **UI** : choix de la faction et du détachement, stratagèmes activables, effets actifs visibles.

À trancher en début de phase : traduction du contenu (noms de règles, de factions).

### Phase 6 : gestion et sauvegarde des unités

**Objectif** : bibliothèque d'unités, choix du loadout et des leaders.

1. **Modèle « fiche d'unité »** : caractéristiques, armes et options, mots-clés, composition (min / max), leaders
   compatibles, règles d'unité (effets de la phase 5).
2. **Bibliothèque d'unités** : `IUnitRepository`, d'abord en fichiers JSON locaux (stockage serveur pour le web ensuite).
3. **Éditeur d'unités** (UI) : créer, modifier, dupliquer, supprimer.
4. **Composition d'un scénario** : choix du loadout et du leader attaché → génération du `Scenario` (groupes d'armes de
   l'attaquant, groupes d'allocation et ordre du défenseur).

## Phase 2a : étapes

1. **Core : progression.** Paramètre optionnel `IProgress<double>` sur `Simulator.Run` (0 → 1, rapporté par paquet),
   déterminisme à graine égale conservé ; tests.
2. **Wh40kSim.UI : services et modèles de formulaire.** Razor Class Library + MudBlazor ; `ISimulationService` /
   `LocalSimulationService` (Core exécuté hors du thread UI) ; `IScenarioFileService` implémenté par l'hôte ;
   formulaires modifiables (`ScenarioForm`…) avec `ToScenario()` et construction depuis un `Scenario` ;
   projet `Wh40kSim.UI.Tests` (allers-retours).
3. **Séparation Core / Simulation.** Le Core ne garde que les règles (résolution d'**une** séquence d'attaque) ;
   tout ce qui se calcule sur les résultats va dans un nouveau projet, que la console et l'UI ne font qu'afficher.
   - Capturer d'abord un résultat de référence (histogrammes à graine fixe) pour la non-régression.
   - `Wh40kSim.Simulation` (référence Core) : `Simulator` (parallélisme, paquets, graines dérivées, progression,
     annulation) et `SimulationResult` (ex-`SimulationSummary`) : scénario, graine, itérations, durée, `Kills`,
     `Damage`, pertes moyennes par groupe, probabilité de détruire l'unité.
   - `Statistics/DiscreteDistribution`, construite depuis un histogramme : moyenne, écart-type, médiane, centiles,
     fourchette 10-90 %, P(= k), P(≥ k), lignes de tableau sans les extrémités négligeables (seuil 0,05 %).
   - Console (`ReportPrinter` réduit à l'affichage) et UI (`ISimulationService`, `LocalSimulationService`) branchées
     sur la nouvelle API ; boucle de simulation toujours sans allocation par itération.
   - `Wh40kSim.Simulation.Tests` : `SimulatorTests` déplacés, tests de `DiscreteDistribution`, non-régression à graine
     égale (résultats identiques au bit près). Projets ajoutés au `.sln` à la main.
   - Pas d'anticipation de la phase 4 (pas de comparaison, pas de distribution par groupe).
4. **Excédent de dégâts (overflow).** Aujourd'hui la distribution des PV retirés est plafonnée aux PV de l'unité :
   tout ce qui dépasse est compté au maximum (le pic final vaut P(unité détruite)), sans rien dire de la marge.
   - Définition : la séquence est inchangée jusqu'à la destruction de l'unité ; ensuite, les attaques restantes
     continuent contre des figurines virtuelles identiques au **premier groupe non-PERSONNAGE dans l'ordre
     d'allocation** (à défaut, le premier groupe). Chacune arrive avec tous ses PV, l'excédent par figurine reste
     perdu, l'Endurance est celle de ce profil, Precision n'a plus de cible. Choix du profil par l'utilisateur : #5 ;
     « dégâts bruts » (gaspillage) : #6.
   - Core : `AttackResolver` peut continuer après la destruction ; `DefenderState` compte figurines et PV au-delà de
     l'unité. Les dés de l'excédent sont tirés sur un **flux séparé** : les résultats réels restent identiques au bit
     près (`NonRegressionTests` inchangés).
   - Simulation : deux distributions prolongées (figurines tuées, PV retirés) calculées dans la même simulation.
     Si le coût en temps se voit, en faire une option.
   - Console : affiche la marge (tableaux prolongés), pour tester l'étape sans interface.
   - Tests : séquences scriptées (continuation sur le profil garde du corps, sans garde du corps, Precision), et
     cohérence avec la distribution réelle (identique sous le maximum, P(prolongé ≥ max) = P(détruite)).
5. **Composants : page « Simulateur ».**
   - Attaquant : liste de profils d'armes (ajout / suppression / duplication) ; nom, tir/mêlée, nombre de figurines,
     A, CT/CC, F, PA, D (A et D acceptent "D6+1") ; mots-clés en cases à cocher / champs numériques (Torrent,
     Lethal Hits, Sustained Hits X, Devastating Wounds, Twin-linked, Blast, Rapid Fire X, Melta X, Heavy, Lance,
     Ignores Cover, Precision, touche / blessure critique sur X+).
   - Défenseur : nom, couvert, modificateurs ; liste **ordonnée** des groupes d'allocation (monter / descendre), avec
     nom, PERSONNAGE, nombre, PV, Endurance, Sv, InSv, FNP, réduction de dégâts. **Pas d'Endurance au niveau du
     défenseur** : elle est portée par chaque groupe (déjà le cas dans le Core) ; un nouveau groupe reprend celle du
     précédent.
   - Situation : demi-portée / immobile / a chargé affichés seulement si une arme est concernée ; modificateurs ;
     relances ; cible de Precision s'il y a plusieurs PERSONNAGES et au moins une arme Precision.
     Pour ne pas mettre de règle dans l'UI, ajouter au Core des propriétés en lecture seule sur `WeaponProfile` :
     `UsesHalfRange` (Rapid Fire, Melta), `UsesRemainedStationary` (Heavy sur arme de tir), `UsesCharged` (Lance),
     utilisées par le moteur, la console et l'UI. Version minimale : la vraie gestion des mots-clés reste en phase 5.
   - Erreurs de `Scenario.Validate()` affichées clairement ; erreur de saisie de dés affichée sous le champ concerné ;
     bouton Lancer désactivé tant que le scénario est invalide.
   - Panneau de simulation : nombre de simulations (défaut 1 000 000), graine optionnelle, Lancer, barre de
     progression, Annuler.
   - Résultats en texte et tableaux (pas de graphique, c'est la phase 4), lus dans `SimulationResult` sans aucun
     calcul : les mêmes informations que la console (moyennes, écarts-types, médianes, fourchette 10-90 %, unité
     détruite, pertes par groupe, tableaux P(= k) et P(≥ k) pour les figurines tuées et les PV retirés, lignes
     négligeables masquées aux extrémités). Choix d'affichage réel / prolongé (excédent de l'étape 4), avec le
     profil utilisé pour l'excédent indiqué clairement.
   - Boutons Ouvrir / Enregistrer (JSON compatible avec `scenarios/`) et Nouveau.
   - Textes de l'interface via `IStringLocalizer` + `.resx` (voir Traduction).
   - Tests de composants avec bUnit : Lancer désactivé si invalide, situation conditionnelle, monter / descendre.
6. **Wh40kSim.Desktop (MAUI Blazor Hybrid).**
   - Vérifier `dotnet workload list` (sinon indiquer `dotnet workload install maui-windows`).
   - Projet MAUI Blazor Hybrid qui affiche les composants de Wh40kSim.UI dans un `BlazorWebView`. Ce que Wh40kSim.UI
     attend de l'hôte (étape 5) : `services.AddWh40kSimUI()`, `UiCulture.Apply()` au démarrage, les fournisseurs
     MudBlazor dans la mise en page (`MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`,
     `MudSnackbarProvider`), les CSS / JS de MudBlazor, puis `<SimulatorPage />` (composant non routé). Depuis
     l'étape 7 : `AppShell` fournit déjà tout cela ; l'hôte charge en plus `_content/Wh40kSim.UI/wh40ksim.css`
     (le module `wh40ksim.js` des raccourcis est importé par la page elle-même).
   - Cible Windows uniquement : `net10.0-windows10.0.19041.0`, `WindowsPackageType=None` (non empaqueté).
     macOS écarté (compilation impossible sous Windows, non testable). `Directory.Build.props` impose
     `TargetFramework=net10.0` : le projet redéfinit `TargetFramework` (au singulier).
   - Enregistre `LocalSimulationService` et un `IScenarioFileService` : `FilePicker` de MAUI pour ouvrir (ne permet pas
     de choisir le dossier de départ), `FileSaver` de CommunityToolkit.Maui pour enregistrer (dossier de départ
     `scenarios/` s'il est trouvé).
   - Thème MudBlazor qui suit Windows au démarrage + bouton de bascule clair / sombre ; fenêtre ~1400 × 900 centrée,
     redimensionnable avec une taille minimale ; titre « Wh40kSim – Simulateur de dégâts ».
   - Projet ajouté à la solution ; README : prérequis (workload MAUI, WebView2), lancement depuis Rider et en ligne
     de commande, Windows uniquement ; roadmap du README et commentaires du code alignés sur la nouvelle
     numérotation des phases.
   - Fin de phase 2a : tag `v0.2a`.
7. **Finitions de l'interface** (ajoutée après `v0.2a` : interface jugée « encore un peu austère »). Tout dans
   Wh40kSim.UI, aucune règle ni calcul ajouté côté interface.
   - Style : thème à identité Warhammer 40k, en-têtes de section avec icônes, cartes en relief. Prototype validé sur
     captures. **Deux styles sombres gardés** : Grimdark (presque noir, or terni, rouge sang, matière ; par défaut) et
     Acier (acier et laiton, le premier prototype), plus le clair parchemin. Choix par l'utilisateur : ticket #9.
   - Résultats : tuiles de synthèse en tête (figurines tuées, PV retirés, unité détruite) ; barre proportionnelle dans
     la colonne P(= k) des tableaux (pas encore les graphiques de la phase 4).
   - Mise en page en deux colonnes : saisie à gauche, simulation et résultats à droite, toujours visibles.
   - Profils d'armes et groupes du défenseur repliables, avec un résumé en en-tête (`Describe()` du Core).
   - **Sélecteur de mots-clés** (idée de l'utilisateur, remplace les puces) : sur la carte d'une arme, les mots-clés
     actifs en étiquettes (rappel de règle au survol, croix pour retirer) et un bouton « Mots-clés » qui ouvre un
     **panneau latéral** : recherche (Entrée ajoute), filtre par catégorie, valeur modifiable sur place. Composant
     générique `KeywordPicker<TTarget>` + catalogue `KeywordDefinition` (dans l'UI : accès aux champs du formulaire
     seulement), réutilisable en phase 5 avec une portée (arme, unité…).
   - Raccourcis clavier : Ctrl+Entrée lancer, Ctrl+S enregistrer, Ctrl+O ouvrir.
   - Confirmation avant de perdre une saisie modifiée (Nouveau / Ouvrir) ; « * » après le nom du fichier si modifié.
   - Nombre de simulations en choix rapides (10 000 / 100 000 / 1 000 000 / 10 000 000) ; rappel de règle en une
     phrase pour chaque mot-clé (sélecteur et infobulles des étiquettes).
   - Retours de l'utilisateur intégrés : groupes du défenseur en cartes aérées (numéro, filet rouge pour un
     PERSONNAGE), libellés des champs à 85 % et non coupés, tuile « Unité détruite » rouge sous 50 %, verte au-delà de
     84 %, libellés « modificateur au jet » précisés.
8. **Modifications de caractéristiques** (ajoutée pendant l'étape 7 : un modificateur au jet de touche, plafonné à ±1,
   n'est pas une modification de la CT / CC, non plafonnée ; seul le couvert était géré). Règles confirmées par
   l'utilisateur : pas de limite sur la modification de CT / CC (le résultat reste borné entre 2+ et 6+, 02.02.01, déjà
   appliqué au couvert), PA jamais au-dessus de 0, Force jamais sous 1.
   - **Boosts de chaque côté** (choix final de l'utilisateur, après deux essais : tout dans la Situation, puis
     défenseur sans boosts) : chacun saisit les siens, le moteur les cumule (ex. +2 dégâts de l'attaquant et -1 dégât
     du défenseur interagissent, sans calcul à la main). Convention : valeurs du point de vue de l'attaquant (+1 =
     mieux pour lui).
     - Attaquant (`AttackerUnit.Boosts`, record `AttackerBoosts`) : CT / CC, jet de touche, relance des touches ;
       Force, jet de blessure, relance des blessures ; A, PA, D.
     - Défenseur (`DefenderUnit.Boosts`, record `DefenderBoosts`) : couvert, CT / CC des attaquants, jet de touche ;
       jet de blessure, PA des attaques reçues, dégâts des attaques reçues (`Damage` : avec la réduction de dégâts
       des groupes, après Melta, minimum 1, pas sur les blessures mortelles).
     - `AttackContext` ne garde que la situation (demi-portée, immobile, a chargé, cible de Precision).
     - Caractéristiques appliquées au profil avant les jets, sans plafond, dans leurs bornes (CT / CC 2+ à 6+ ; F, A,
       D au moins 1 ; PA jamais au-dessus de 0) ; modificateurs au jet inchangés (±1 au total).
   - JSON calqué sur l'interface (pas d'ancien fichier à préserver, choix de l'utilisateur) : `attacker.boosts`,
     `defender.boosts`, `context` réduit à la situation ; les anciens champs (`context.hitReroll`,
     `defender.inCover`…) sont refusés. Boosts absents = neutres. Non-régression au bit près.
   - Tests : dés scriptés pour chaque boost des deux côtés, cumuls (couvert, attaquant + défenseur), bornes, JSON.
   - UI : bloc **« Boosts »** repliable dans la carte de l'attaquant et dans celle du défenseur (replié tant qu'aucun
     boost n'est saisi, nombre de boosts actifs dans l'en-tête), rangées Touche / Blessure / Profil. La **Situation**
     ne garde que les conditions (demi-portée, immobile, a chargé, cible de Precision). **Aucune annotation sous les
     champs** : explications dans des bulles « ? » (infobulle après 1 s de survol). README : séquence d'attaque.
   - Retours de l'utilisateur : titre « Armement » entre les boosts et les armes (comme « Groupes d'allocation ») ;
     bloc Boosts teinté ; **couleurs de camp** façon cartes de déploiement, dans tous les thèmes : rouge pour
     l'attaquant, vert pour le défenseur (variable CSS `--wh-accent` des sections `.wh-attacker` / `.wh-defender`) ;
     filet du PERSONNAGE passé en or (le rouge désigne l'attaquant) ; choix rapide du nombre de simulations en
     surbrillance.
   - **Marge (excédent de l'étape 4) clarifiée** : la moyenne des figurines tuées changeait avec le mode prolongé et
     dépassait l'effectif. Les tuiles restent toujours les pertes réelles ; « Afficher la marge » ajoute sous leur moyenne
     « +1,33 (Marge) », expliqué en infobulle, tiret si l'unité n'est jamais détruite (figurines et PV en plus, en
     moyenne quand l'unité est détruite : `SimulationResult.KillMarginWhenWiped` /
     `DamageMarginWhenWiped`, via `DiscreteDistribution.MeanExcessAbove`) et prolonge les tableaux, lignes au-delà
     de l'unité notées « 5 + 2 » sous un trait. La console affiche aussi la marge.
   - Fin d'étape : tag `v0.2a.1`.

## Décisions techniques

- **Hôte desktop : MAUI Blazor Hybrid**, pas Photino.Blazor. Photino.Blazor 4.0.13 (janvier 2025) ne cible que
  net8 / net9 et le support .NET 10 n'est pas publié (issue #164, PR #168 ouvertes) ; MAUI est maintenu par Microsoft.
- **Core = règles, Simulation = Monte Carlo et statistiques** (2026-10-27) : le Core résout une séquence d'attaque et
  ne connaît ni le nombre d'itérations, ni les threads, ni les statistiques. `Wh40kSim.Simulation` répète, agrège en
  histogrammes (dans la boucle parallèle, pas de stockage des résultats bruts) et calcule toutes les statistiques.
  La console et les composants ne font que mettre en forme : aucun calcul dupliqué entre eux.
- **Excédent de dégâts** (2026-10-27) : « PV retirés » reste la mesure réelle (règles du jeu, plafonnée) ;
  l'excédent est une distribution prolongée à part, contre des copies du premier garde du corps (pas du dernier tué,
  souvent le leader, unique et plus résistant). Flux de dés séparé pour ne pas modifier les résultats réels.
- **Web (phase 2b)** : Blazor Web App, serveur `Wh40kSim.Web` (API + client) et client WebAssembly
  `Wh40kSim.Web.Client` ; le calcul se fait sur le serveur.
- **Versions** (vérifiées le 2026-10-27) : MudBlazor 9.11.0, Microsoft.AspNetCore.Components.Web 10.0.14,
  Microsoft.Extensions.Localization 10.0.14, CommunityToolkit.Maui 15.0.1, bUnit 2.13.3, Microsoft.Maui.Controls et
  Microsoft.AspNetCore.Components.WebView.Maui 10.0.120 (charge de travail MAUI 10.0.110), Microsoft.Extensions.Logging.Debug
  10.0.14. Paquets MAUI à version fixe, pas `$(MauiVersion)` (qui suit la charge de travail installée sur chaque machine).
  Tests xUnit : mêmes versions que `Wh40kSim.Core.Tests`.
- **Formulaires (Wh40kSim.UI/Forms)** : classes modifiables avec un constructeur depuis le record du Core, un
  constructeur vide (= valeurs par défaut du Core, rien n'est recopié dans l'UI) et une méthode `ToXxx()`.
  `WeaponForm` regroupe `WeaponGroup` + `WeaponProfile`. Expressions de dés en texte, lues par le Core.
  Sustained Hits / Rapid Fire / Melta : case à cocher + valeur séparées. `ToScenario()` lève `FormatException` si une
  expression de dés est invalide.
- **IScenarioFileService** : `OpenAsync()` → `OpenedScenario(FileName, Scenario)` ou null si annulé ;
  `SaveAsync(scenario, suggestedFileName)` → nom du fichier écrit ou null si annulé.
- **Traduction** préparée dès la phase 2 : textes de l'UI dans des `.resx` (français seulement pour l'instant), nombres
  formatés selon la culture de l'appli, fixée à fr-FR (pas celle de la machine). Messages du Core et anglais : phase 3.
  Traduction du contenu (règles d'armée, unités) : à trancher en phase 5.
- **Endurance** : portée par chaque groupe, valeur par défaut E4 conservée (un groupe JSON sans `toughness` reçoit E4).
  Les anciens fichiers avec `toughness` sur le défenseur sont refusés au chargement (JSON strict, testé).
- **Tests aller-retour** : comparer les scénarios via `ScenarioJson.Serialize` (les records contenant des listes ne sont
  pas égaux par valeur).
- **Composants (Wh40kSim.UI/Components)** : `SimulatorPage` tient le `ScenarioForm` et le revalide (`ToScenario()` puis
  `Validate()`) à chaque `Changed` remonté par un panneau. Les options de situation dépendent du dernier scénario lisible
  (stable pendant une saisie de dés invalide). Listes « aucune » / « premier PERSONNAGE » : valeur sentinelle (0, "")
  convertie en null dans le composant. Textes : `IStringLocalizer<Strings>` injecté dans `_Imports.razor`, un seul
  `Resources/Strings.resx` (clé `Zone_Élément`), `StringsTests` vérifie qu'aucune clé ne manque ni ne traîne.
- **Tests bUnit** : `ComponentTestContext` (services, culture, faux services, `MudPopoverProvider` rendu d'abord,
  JS en mode `Loose`, `IAsyncLifetime` car des services MudBlazor ne se libèrent qu'en asynchrone). Repérage par
  attributs `data-test` : MudBlazor les pose sur l'`<input>` lui-même (sélecteur `input[data-test=…]`). Clic dans une carte
  qui contient des infobulles MudBlazor (rendu asynchrone, clic parfois perdu) : `ClickUntil(sélecteur, effet)`.
  Tests UI exécutés sans parallélisme (`TestAssemblyInfo.cs`) : en parallèle, la charge faisait perdre des
  interactions (environ une exécution sur huit) ; 0 échec sur 15 exécutions ensuite.
- **Aides à l'écran** : pas de `HelperText` ni de légende sous les champs ; explications dans `HelpBubble` (bulle
  « ? », infobulle après 1 s), via le paramètre `Help` de `SectionHeader` / `SubSection` (liste de textes du `.resx`).
- **Ordre d'allocation (05.03)** : règle portée par le Core (`AllocationOrder` : `FindViolation`, `IsValid`, `Arrange`),
  utilisée par `Scenario.Validate` et par l'interface. Le panneau du défenseur l'impose à la saisie : flèches désactivées
  si un PERSONNAGE passerait devant un garde du corps, groupes remis en ordre après chaque modification (cocher
  PERSONNAGE envoie le groupe en fin de liste), nouveau groupe inséré avant les PERSONNAGES. Un fichier JSON mal ordonné
  reste signalé par la validation.
- **Mise en page** : les points de rupture de MudGrid (`sm`, `md`, `lg`) suivent la largeur de la *fenêtre*, pas du
  panneau ; dans un panneau en demi-largeur, n'utiliser que `xs` / `sm`.
- **Hôte desktop (Wh40kSim.Desktop)** : réduit au minimum. Toute l'interface est `AppShell` (Wh40kSim.UI) :
  fournisseurs MudBlazor, barre de titre, thème qui suit le système au démarrage (`GetSystemDarkModeAsync`, préférence
  lue par la WebView) et bascule, puis `SimulatorPage` ; réutilisable tel quel par l'hôte web (2b). L'hôte fournit
  `MauiProgram` (services), la fenêtre (`App` : titre lu dans `Strings.resx`, 1400 × 900 centrée, minimum 900 × 600),
  `DesktopScenarioFileService` et `wwwroot/index.html` (CSS / JS de MudBlazor). Pas de `Package.appxmanifest`
  (application non empaquetée).
- **Vérification visuelle** : pas de test automatique du rendu. Captures d'écran de la fenêtre par un script PowerShell
  (`CopyFromScreen`, clic et molette via `user32`) dans le scratchpad de Claude, lues comme des images.

## Environnement de développement

- SDK .NET 10 ; IDE Rider ; historique suivi dans SourceTree. Desktop développé et testé sous Windows uniquement.
- Charge de travail MAUI : `dotnet workload install maui-windows`, à faire sur chaque machine **et pour chaque
  installation de .NET** (les charges de travail ne sont pas partagées entre `C:\Program Files\dotnet`, qui demande un
  terminal administrateur, et un .NET installé dans le dossier utilisateur). Rider affiche le SDK qu'il utilise en tête
  du build (« Use build tool: … »). Sans la charge de travail, le build de la solution échoue (projet Wh40kSim.Desktop).
  Après l'avoir installée, relancer `dotnet restore` : sinon l'erreur NETSDK1047 (« project.assets.json n'a aucune
  cible pour …/win-x64 ») persiste, la restauration précédente ne connaissant pas la cible Windows.
- Si .NET est installé dans le dossier utilisateur plutôt que dans `C:\Program Files\dotnet`, lancer un `.exe`
  directement échoue (0x80008083, runtime introuvable) sans la variable `DOTNET_ROOT` ; `dotnet run` et Rider fonctionnent.
- Si les chemins longs Windows sont désactivés (`LongPathsEnabled = 0`), le build MAUI échoue au-delà de 260
  caractères : ne pas placer le dépôt dans un dossier trop profond (dans le dépôt, le chemin le plus long fait ~180).
- Le modèle `dotnet new maui-blazor` cible aussi Android / iOS : ne garder que la cible Windows.
- CLI GitHub (tickets) : `winget install --id GitHub.cli -e --source winget --accept-source-agreements
  --accept-package-agreements` (sans ces options, winget bloque sur les conditions du Microsoft Store), puis
  `gh auth login` dans un terminal séparé (questions interactives : GitHub.com, HTTPS, navigateur). Une session ouverte
  avant l'installation n'a pas `gh` dans son PATH : `C:\Program Files\GitHub CLI\gh.exe`.
- Pas de `.gitattributes` (ticket #8) : avec `core.autocrlf=true`, la copie de travail est en CRLF et l'index en LF. La règle
  « fins de ligne LF » du `.sln` porte sur le contenu versionné (vérifier avec `git ls-files --eol`).

## Avancement

- Phase 2a, étape 1 (progression) : fusionnée dans `main`.
- Phase 2a, étape 2 (Wh40kSim.UI, services, formulaires) : fusionnée dans `main`.
- Roadmap refaite et renumérotée le 2026-10-27 (web local abandonné, traduction en phase 3, étape « Séparation
  Core / Simulation » insérée en 2a).
- Phase 2a, étape 3 (séparation Core / Simulation) : fusionnée dans `main`. 156 tests verts (95 Core,
  28 Simulation, 33 UI) ; non-régression au bit près sur deux scénarios (`NonRegressionTests`) ; sortie de la console
  identique à celle d'avant le refactor.
- Étape « Excédent de dégâts » insérée en 4 le 2026-10-27 (composants en 5, desktop en 6).
- Backlog passé en GitHub Issues le 2026-10-27 : labels `idée`, `outillage`, `dette technique` (+ `bug` par défaut),
  tickets #1 à #8 repris de l'ancienne section Backlog. `gh` installé et authentifié sur la deuxième machine
  seulement : **à faire sur l'autre machine** (voir « Environnement de développement »).
- Phase 2a, étape 4 (excédent de dégâts) : fusionnée dans `main`.
  169 tests verts (103 Core, 33 Simulation, 33 UI) ; `NonRegressionTests` inchangés (résultats réels
  identiques au bit près) ; pas de surcoût mesurable (5 M simulations, unité détruite à 89 %), donc pas d'option.
- Phase 2a, étape 5 (composants) : fusionnée dans `main`.
  211 tests verts (114 Core, 33 Simulation, 64 UI dont bUnit). Pas encore vue à l'écran : aucun hôte avant l'étape 6.
- Phase 2a, étape 6 (Wh40kSim.Desktop) : fusionnée dans `main`, **tag `v0.2a`** (fin de la phase 2a).
  213 tests verts (114 Core, 33 Simulation, 66 UI). Application lancée et vérifiée à l'écran
  (captures) : fenêtre, thème, saisie, simulation et résultats ; deux défauts de mise en page corrigés (groupe
  d'allocation, valeurs de Sustained Hits / Rapid Fire / Melta). Numérotation des phases alignée dans le README et le
  code.
- Phase 2a, étape 7 (finitions de l'interface) : fusionnée dans `main` (sans tag : `v0.2a.1` à la fin de l'étape 8).
  234 tests verts (114 Core, 33 Simulation, 87 UI). Vérifiée à l'écran par l'utilisateur au fil de l'eau.
- Phase 2a, étape 8 (modifications de caractéristiques) : terminée sur `phase2a/etape8-modifications-caracteristiques`,
  en attente de validation avant fusion et du tag `v0.2a.1`. 271 tests verts (145 Core, 33 Simulation, 93 UI) ;
  `NonRegressionTests` inchangés (sans modification, résultats identiques au bit près).
- Prochaine ensuite : phase 2b (API + client web).

Points notés pour la suite :
- Charge de travail MAUI : installée sur la deuxième machine ; **à installer sur l'autre machine** (puis
  `dotnet restore`).
- Raccourcis clavier : Ctrl+S / Ctrl+O sont interceptés par `wh40ksim.js` (action du navigateur empêchée) ; à revérifier
  dans un vrai navigateur en phase 2b.
