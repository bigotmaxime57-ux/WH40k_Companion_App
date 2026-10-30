# Wh40kSim : consignes pour Claude Code

Ce fichier est lu automatiquement par Claude Code au début de chaque session. Il garde les règles, la roadmap et les
décisions du projet, pour reprendre le travail sur n'importe quelle machine. **Le tenir à jour** : l'avancement est mis
à jour à la fin de chaque étape, dans le commit de l'étape (en attente de validation), puis à sa validation
(`/valider-etape`).

**Deux machines.** L'utilisateur travaille en alternance sur deux PC Windows. Ce fichier (versionné) contient
uniquement ce qui vaut pour les deux : règles, décisions, avancement. Ce qui est propre à une machine (chemin du SDK,
charges de travail installées, identité git, réglages Windows…) va dans la mémoire utilisateur de Claude Code de
cette machine (`~/.claude/projects/<projet>/memory/`), jamais dans le dépôt. En début de session : skill `/debut-journee`
(`.claude/skills/debut-journee/SKILL.md`) avant toute chose (synchronisation, build et tests complets pour remettre
l'environnement d'aplomb, point sur l'avancement et les questions en attente) ; en fin de session : tout pousser, et mettre à jour
« Avancement » si l'état a changé (étape en cours, branche non fusionnée, point en suspens).

Voir aussi `README.md` (structure de la solution, règles de jeu implémentées, format JSON).

## Règles de travail

- **Core pur** : aucune règle de jeu dans l'UI, aucune dépendance UI dans le Core. L'UI ne fait qu'appeler le Core
  (`Scenario.Validate`, `DiceExpression.Parse`, `Simulator.Run`…).
- **Langue** : interface et commentaires en français, noms de code (types, méthodes, propriétés) en anglais.
- **Ne rien coder sans feu vert explicite** pour une étape. Les étapes sont décrites à l'avance (voir plus bas) ;
  signaler avant de commencer tout point qui contredit le code existant ou demande une décision.
- **Après chaque étape** : `dotnet build` et `dotnet test` sans erreur ni avertissement, puis un commit dédié. Une fois
  la branche poussée, l'**intégration continue** (GitHub Actions, `.github/workflows/ci.yml` : tests sous Linux, build
  complet sous Windows) doit être verte avant de présenter l'étape (`gh run list --branch <branche>`, `gh run view`).
- **En fin d'étape**, expliquer brièvement ce qui a été créé et pourquoi, en expliquant les notions C# / Blazor
  introduites (sans faire un cours).
- **Versions de paquets** : vérifier sur NuGet la dernière version stable compatible .NET 10 avant de référencer un paquet.
- **Wh40kSim.Console** est conservé comme outil de debug : ne pas le supprimer, le garder compilable.
- **Ne pas anticiper les phases suivantes** pendant une phase (ne pas la surcharger).
- **« voir sc »** = regarder la capture d'écran que l'utilisateur a mise dans le presse-papiers : skill `/voir-sc`
  (`.claude/skills/voir-sc/SKILL.md`), qui l'enregistre dans le scratchpad puis la lit comme une image.
- **Tests à l'écran par l'utilisateur** (consigne du 2026-10-29) : après une modification, Claude ne lance pas
  l'application et ne prend pas de captures pour vérifier ; il dit quoi tester, et l'utilisateur relance l'appli
  lui-même depuis Rider (plus rapide). Lancer l'appli et prendre des captures seulement sur demande explicite.
- **Captures supprimées après usage** : toute capture enregistrée par Claude (demande explicite, `/voir-sc`) est
  effacée du scratchpad une fois lue et exploitée, pour ne pas accumuler d'images.

## Git

- Une branche par étape, créée depuis `main` à jour (ex. `phase2a/etape3-composants`).
- Un ou quelques commits cohérents par étape, messages en français, terminés par la ligne `Co-Authored-By` de Claude.
- **Fusion dans `main`** : présenter l'étape, puis fusionner **uniquement quand l'utilisateur la valide**, avec le
  skill `/valider-etape` (`.claude/skills/valider-etape/SKILL.md`), qui décrit toute la procédure (avancement,
  `git merge --no-ff`, build et tests, push, suppression de la branche, tickets). Autorisation permanente du
  2026-10-27 : à la validation, Claude enchaîne sans redemander ; les tags restent soumis à accord.
  `git config merge.ff false` est aussi réglé dans la config locale du dépôt, sur chaque machine.
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
de chaque phase. Numérotation revue le 2026-10-27, après la phase 2a : graphiques et comparaison en 3 (pour avancer
sur les statistiques avant le web), traduction en 4, l'ancienne 2b (API + client web) devient la phase 7. Revue à
nouveau le 2026-10-30 (décisions de l'utilisateur) : la phase 3 se termine avec l'overwatch, le web remonte en 4, la
comparaison (5) et l'optimiseur (6) deviennent des phases à part, puis traduction (7), mots-clés et règles (8),
unités (9, avec le rapport en points).

**Après la phase 4 (web)**, chaque phase livre aussi sa partie web : ce qu'elle ajoute au Core, à Simulation et à
Wh40kSim.UI est couvert par l'API et le client WebAssembly dans la même phase (décision du 2026-10-30).

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

### Phase 2 : interface graphique (terminée)

**Objectif** : remplacement de la console par une interface graphique.

- **2a : desktop** (MAUI Blazor Hybrid + MudBlazor) : étapes détaillées dans « Phase 2a : étapes ». Tags `v0.2a`,
  `v0.2a.1`, `v0.2a.2`.
- L'ancienne **2b** (API + client web) est devenue la phase 7 (2026-10-27), puis la phase 4 (2026-10-30).

### Phase 3 : graphiques et Full sim

**Objectif** : « Affichage des distributions façon WarpHammer Math : histogrammes, probabilités cumulées P(≥ k) ».
(La fin de la formulation d'origine, la comparaison de plusieurs profils côte à côte, est passée en phase 5 le
2026-10-30, avec l'accord de l'utilisateur.)

Tous les chiffres viennent de `Wh40kSim.Simulation` ; les graphiques ne font que les dessiner, dans Wh40kSim.UI
(desktop maintenant, web en phase 4). Modes ouverts depuis un écran d'accueil (décision du 2026-10-27), une tuile
chacun :

- **Quick sim** : l'écran actuel, gardé tel quel (une page, A contre B, les grandes lignes, pas de graphique en plus).
- **Full sim** : A contre B, même scénario que le Quick sim (partagé), avec beaucoup plus de stats, graphiques et
  analyses (tableau de bord).
- Plus tard : **Comparaison** (phase 5) et **Optimiseur** (phase 6).

Le tir en **overwatch** n'est pas un mode : c'est un interrupteur de la Situation (Quick sim et Full sim), qui fait
tirer selon ses règles (étape 9, #11). Dernière étape : relances par arme (« pêche aux critiques », étape 10).

Étapes détaillées dans « Phase 3 : étapes ». **Le choix des technologies doit tenir compte de la phase 4** : tout ce
qu'on ajoute à Wh40kSim.UI (bibliothèque de graphiques, navigation) doit fonctionner aussi bien dans le
`BlazorWebView` du desktop que dans le client Blazor WebAssembly, sans API propre au desktop, avec un poids raisonnable
à télécharger dans le navigateur. Les résultats (simple, détaillé) doivent rester sérialisables : ils formeront le
contrat JSON de l'API.

### Phase 4 : intégration web

**Objectif** : API serveur + client web (ancienne phase 2b, déplacée le 2026-10-27, remontée en 4 le 2026-10-30).

Le « web local » prévu à l'origine est abandonné (2026-10-27) : le desktop couvre l'usage local, et ce qu'il devait
apporter (fichiers côté navigateur, Wh40kSim.UI dans un hôte web) est repris ici. Structure Blazor Web App :
`Wh40kSim.Web` (serveur ASP.NET Core : API + service du client) et `Wh40kSim.Web.Client` (WebAssembly). Le calcul se
fait sur le serveur. Ce qu'a apporté la phase 3 (Quick sim, Full sim et ses rapports, fichiers partiels, thèmes,
overwatch, pêche aux critiques) est à couvrir par l'API et le client ; les phases suivantes livrent leur propre partie
web.

Découpage revu avec l'utilisateur le 2026-10-30. Décisions :
- **Mise en ligne publique** (décision contraire à « hébergement hors périmètre ») : site **public, sans compte** ;
  **image Docker** qui tourne chez n'importe quel hébergeur, choisi à la dernière étape avec l'utilisateur (c'est lui
  qui crée le compte) ; **déploiement automatique depuis GitHub** (GitHub Actions).
- **Résultat transmis en histogrammes bruts** : le serveur envoie les compteurs bruts, le navigateur reconstruit
  `SimulationResult` avec le même code `Wh40kSim.Simulation` (écrans inchangés, contrat JSON petit et stable).
- **Progression en Server-Sent Events** sur la requête `POST` elle-même (pris en charge par .NET 10) ; la fermer
  annule le calcul.
- **Protections du serveur** : plafond de **1 000 000** simulations par lancement (au-delà : refus, message clair) ;
  calculs simultanés limités (les suivants attendent, progression « en attente ») ; limite de lancements par adresse IP.
  Le desktop n'a toujours pas de plafond.

Chaque étape sera précisée et validée avant d'être codée :

1. **Contrat du résultat** (Simulation, sans web). Précisée et validée par l'utilisateur le 2026-10-30 :
   - `SimulationData` (record sérialisable) : les compteurs **bruts** de l'accumulateur, entiers seulement — scénario
     (format de `ScenarioJson`), graine, durée (le nombre de simulations se déduit des histogrammes) ; unité :
     histogrammes des figurines tuées, des PV retirés, prolongés (marge), des dégâts bruts, du gaspillage, gaspillage
     cumulé quand l'unité survit ; gardes du corps : histogrammes ; par groupe : cumuls et histogrammes, pertes « grâce
     à Precision », compteurs des armes Precision (groupes PERSONNAGE, si l'attaquant en a) ; par pool : histogrammes
     des PV retirés et des figurines achevées, dégâts bruts, tirs sur une unité déjà détruite, gaspillage par total,
     compteurs de conversion. Moyennes, parts et centiles recalculés à l'arrivée.
   - L'accumulateur produit un `SimulationData` ; le passage aux statistiques (`ToResult`) devient le constructeur
     public `SimulationResult(SimulationData)` ; `SimulationResult.Data` donne les données brutes. API de
     `SimulationResult` inchangée : console, Quick sim et Full sim intacts ; aucun dé en plus.
   - `SimulationJson` (Wh40kSim.Simulation) : lecture et écriture avec les réglages stricts de `ScenarioJson` ;
     cohérence vérifiée à la lecture (tailles des histogrammes, nombre de groupes et de pools, simulations comptées),
     erreur claire sinon. Pas de numéro de version (serveur et client déployés ensemble).
   - Tests : aller-retour JSON sur le scénario riche (Precision, pools, marge, gaspillage) qui redonne exactement les
     mêmes statistiques ; données incohérentes refusées ; `NonRegressionTests` inchangés ; temps mesuré.
2. **Serveur et API** (`Wh40kSim.Web`, Minimal API). Précisée et validée par l'utilisateur le 2026-10-30 :
   - Projets `Wh40kSim.Web` (ASP.NET Core, référence Wh40kSim.Simulation ; servira le client à l'étape 3) et
     `Wh40kSim.Web.Tests` (`WebApplicationFactory`, version de `Microsoft.AspNetCore.Mvc.Testing` vérifiée sur NuGet),
     ajoutés au `.sln` à la main ; desktop et console inchangés.
   - `POST /api/simulations` : `{ "scenario": …, "iterations": …, "seed": … }` (scénario au format des fichiers
     complets, lecture stricte ; graine facultative). Refus avant calcul en `ProblemDetails` (titre en français, liste
     des erreurs) : 400 pour un JSON illisible, un scénario invalide (`Scenario.Validate`) ou un nombre de simulations
     hors de 1 à 1 000 000 ; 429 au-delà de 10 lancements par minute et par adresse IP (`Retry-After`).
   - Réponse en **Server-Sent Events** (.NET 10) : `queued` (position dans la file, renvoyée quand elle change),
     `progress` (0 à 1, par point de pourcentage), `result` (`SimulationData` au format de `SimulationJson`), `error`
     (échec inattendu, sans planter le serveur).
   - Décisions de l'utilisateur : **un seul calcul à la fois** (tous les cœurs), **file d'attente sans limite** (ordre
     d'arrivée), **10 lancements par minute et par IP**, valeurs **réglables** (`appsettings.json`, variables
     d'environnement). Annulation : requête fermée → sortie de la file ou arrêt du calcul. Calcul derrière une
     interface interne (`ISimulationRunner`, `Simulator.Run` par défaut) pour tester attente, annulation et erreurs.
   - Fichier `.http` d'exemple (demande valide, demande invalide) pour essayer l'API depuis Rider.
   - Tests : bout en bout (progression puis résultat, identique au bit près à `Simulator.Run` à graine égale) ;
     refus (JSON illisible, propriété inconnue, scénario invalide avec ses erreurs, 0 et 1 000 001 simulations,
     11e lancement de la minute) ; file (`queued` position 1, puis résultat) ; annulation (place libérée) ; erreur
     inattendue (événement `error`).
   - Reporté à l'étape 5 : Docker, HTTPS, vraie adresse IP derrière un proxy (`X-Forwarded-For`), point de santé.
3. **Client WebAssembly** (`Wh40kSim.Web.Client`). Précisée et validée par l'utilisateur le 2026-10-30 :
   - Blazor Web App : `Wh40kSim.Web` sert la page et le client ; `Wh40kSim.Web.Client` (WebAssembly, référence
     Wh40kSim.UI), rendu WebAssembly interactif **sans prérendu**. Écrans de Wh40kSim.UI inchangés, seuls les
     services changent. `dotnet run --project src/Wh40kSim.Web` ouvre le site sur `http://localhost:5080`.
   - `ApiSimulationService` (`ISimulationService`) : `POST /api/simulations`, flux SSE lu au fil de l'eau, progression,
     résultat brut reconstruit en `SimulationResult`, Annuler ferme la requête.
   - **Attente dans la file** (décision de l'utilisateur) : **fenêtre** « En attente : N calculs avant vous », mise à
     jour, avec Annuler, fermée au démarrage du calcul ; `ISimulationService` reçoit un suivi facultatif de la file
     (`IProgress<int>`), jamais utilisé par le desktop.
   - Erreurs du serveur dans le bandeau de message (refus 400 avec ses erreurs, 429 avec le délai, événement `error`,
     serveur injoignable).
   - Fichiers : ouvrir par le sélecteur du navigateur (bouton ou Ctrl+O, fichiers partiels compris) ; **enregistrer =
     toujours un téléchargement** sous le nom proposé (décision de l'utilisateur).
   - Préférences dans `localStorage` ; thème sans flash (script de la page pour la barre de chargement, thème lu avant
     d'afficher `AppShell`) ; version affichée sur l'accueil.
   - **Plafond demandé au serveur** (`GET /api/simulations/limits`, décision de l'utilisateur), exposé par
     `ISimulationService` (aucun en desktop) ; **choix rapide 10 M masqué** en web, saisie au-delà refusée, Lancer
     désactivé, bulle « ? » explicative.
   - À vérifier par l'utilisateur dans un vrai navigateur : raccourcis (Ctrl+Entrée, Ctrl+S, Ctrl+O), quatre thèmes,
     parcours complet (ouvrir, modifier, lancer, attente avec deux onglets, annuler, enregistrer).
   - Tests : nouveau `Wh40kSim.Web.Client.Tests` (`ApiSimulationService` face à un faux flux SSE : progression, file,
     résultat, 400, 429, `error`, annulation ; fichiers et préférences avec le JavaScript simulé par bUnit) ;
     Wh40kSim.UI.Tests (choix 10 M masqué et saisie refusée avec un plafond, fenêtre d'attente, desktop inchangé) ;
     Wh40kSim.Web.Tests (page servie avec le client, `GET /api/simulations/limits`).
4. **Intégration continue** (#7). Précisée et validée par l'utilisateur le 2026-10-30 :
   - `.github/workflows/ci.yml`, déclenché à **chaque push sur toutes les branches** et sur les pull requests
     (décision de l'utilisateur) ; dépôt public : minutes gratuites.
   - **Deux tâches** (décision de l'utilisateur) : **Linux** (`ubuntu-latest`) compile et lance tous les tests sur un
     filtre de solution sans le desktop (`Wh40kSim.CI.slnf`) ; **Windows** (`windows-latest`) installe `maui-windows`
     et compile toute la solution, desktop compris, sans relancer les tests.
   - 0 erreur et 0 avertissement exigés (`-warnaserror` dans la CI) ; résultats `.trx` joints en cas d'échec des
     tests ; cache des paquets NuGet ; délai maximal par tâche.
   - Formats de nombres fr-FR : si des tests échouent sous Linux pour une différence de données de culture (espaces
     insécables), les rendre indépendants de ce détail, sans changer l'affichage.
   - Badge « CI » dans le README ; CLAUDE.md et `/valider-etape` : CI verte de la branche vérifiée (`gh run list`,
     `gh run view`) avant de présenter et de fusionner une étape. Ticket #7 fermé par le commit (`Closes #7`).
   - **Publication AOT reportée à l'étape 5** (décision de l'utilisateur), avec Docker et le déploiement.
5. **Docker et mise en ligne**. Précisée et validée par l'utilisateur le 2026-10-30 :
   - Décisions de l'utilisateur : hébergeur **Fly.io** ; **déploiement à chaque tag de version** (`v*`, et à la main
     depuis l'onglet « Actions ») ; **sous-domaine de Fly** (`<nom>.fly.dev`, domaine à soi plus tard) ; **veille
     automatique** (machine arrêtée au repos, redémarrage à la première visite).
   - `Dockerfile` en deux temps (compilation : SDK .NET 10 + `wasm-tools` + Python, publication avec
     `-p:RunAOTCompilation=true` ; exécution : ASP.NET Core 10, utilisateur sans privilèges, port 8080) et
     `.dockerignore` (bin, obj, dist, scénarios personnels, desktop).
   - Production dans `Wh40kSim.Web` : vraie adresse IP lue dans `X-Forwarded-For` derrière le proxy de Fly (activé par
     la configuration seulement, désactivé en local) pour la limite par IP ; point de santé `/health` ; HTTPS assuré
     par Fly (redirection du HTTP).
   - `fly.toml` : région Paris (`cdg`), veille automatique, zéro machine au repos, machine partagée 1 processeur, 1 Go
     (ajustable), vérification de santé sur `/health`.
   - `.github/workflows/deploy.yml` : déclenché par un tag `v*` ou à la main ; réutilise `ci.yml` (déploiement seulement
     si la CI est verte) ; `flyctl deploy` (image construite par Fly, AOT comprise) ; jeton dans le secret GitHub
     `FLY_API_TOKEN`.
   - À faire par l'utilisateur (compte et jeton) : compte fly.io, `flyctl`, `fly apps create <nom>`,
     `fly tokens create deploy` puis `gh secret set FLY_API_TOKEN`.
   - Vérifications : image construite et lancée en local si Docker est installé, sinon au premier déploiement manuel ;
     tests d'intégration (`/health`, adresse IP lue seulement quand c'est activé) ; README (adresse, déploiement,
     commandes Fly). Premier déploiement manuel vérifié par l'utilisateur, puis tag **`v0.4`** (avec son accord), qui
     déploie et clôt la phase 4.

### Phase 5 : comparaison

**Objectif** : « comparaison de plusieurs profils côte à côte » (qui tue le mieux l'adversaire) ; formulation reprise
de l'Objectif d'origine de la phase 3 (2026-10-30). Mode à part, avec sa tuile sur l'accueil.

1. **Comparaison, côté Simulation.** Deux sens (#1) : N attaquants contre une cible, ou un attaquant contre N cibles ;
   situation commune. Même graine pour toutes les entrées (tirages partagés : écarts plus fiables). Indicateurs :
   moyennes, P(détruire l'unité), P(≥ k), % de l'unité détruite et points détruits (comparables entre cibles
   différentes) ; meilleure entrée pour chaque indicateur, ex æquo compris. JSON d'une comparaison (sérialisable :
   contrat de l'API) ; console.
2. **Comparaison, côté UI.** Entrées prises dans le scénario courant ou dans des fichiers (partiels ou complets),
   dupliquées, renommées ; distributions superposées ; tableau côte à côte, meilleure entrée mise en évidence ;
   ouvrir / enregistrer.
3. **Partie web** : comparaison dans l'API et le client.

### Phase 6 : optimiseur du séquencement des armes

**Objectif** : meilleurs ordres de résolution des armes d'un même loadout (#3, ajouté le 2026-10-29 ; phase à part
depuis le 2026-10-30). Mode à part (tuile sur l'accueil), pour ne pas alourdir les simulations.

À partir du scénario courant (ou d'un fichier), toutes les permutations des **pools** (pas des armes : celles d'un même
pool sont lancées ensemble), avec les statistiques du Full sim. Décisions de l'utilisateur :
- **Rondes éliminatoires** façon Raidbots : tous les ordres avec peu d'itérations (~10 000), puis les meilleurs avec
  ~100 000, puis les derniers en lice avec ~1 000 000 ; nombres à calibrer d'après le temps mesuré, progression
  affichée par ronde. Proposé (à confirmer en début de phase) : élimination selon l'intervalle de confiance
  (garder tout ordre qui chevauche le meilleur, avec un plafond) plutôt qu'une proportion fixe, et sur les trois
  métriques (un ordre reste s'il est parmi les meilleurs sur l'une d'elles) ; même graine pour tous les ordres
  (écarts plus fiables, comme la comparaison) ; avertissement ou plafond au-delà de ~6 pools (720 ordres).
- **Tableau des 10 meilleurs ordres**, trié par défaut par P(unité détruite), triable par % de kill, PV retirés,
  figurines tuées ; meilleur de chaque colonne mis en évidence.
- **Export JSON** de l'ordre choisi, réutilisable au Quick sim et au Full sim : le format « attaquant seul » (ou
  scénario complet), qui garde l'ordre des armes, suffit (pas de nouveau format).
- Partie web (API et client) ; découpage en étapes au début de la phase. Fin de phase : ticket #3 fermé.

### Phase 7 : traduction

**Objectif** : anglais, choix de la langue, messages du Core.

Placée avant les mots-clés : la phase 8 ajoutera beaucoup de messages au Core, autant qu'ils soient déjà sous forme
de codes.

1. **Messages du Core sous forme de codes** : `Validate()` renvoie des erreurs typées (code + paramètres) traduites par
   l'UI ; idem pour les erreurs de lecture JSON et de dés (et celles de la comparaison, phase 5) ; erreurs de l'API
   (`ProblemDetails`) comprises.
2. **Anglais** : `.resx` anglais, choix de la langue dans l'interface (mémorisé), nombres formatés selon la langue
   (graphiques compris), desktop et client web.

### Phase 8 : mots-clés, règles d'armée, détachements

**Objectif** : mots-clés du défenseur (vrai Anti-X…), règles d'armée et détachements, avec un stockage des règles.

1. **Mots-clés du défenseur** (INFANTERIE, VÉHICULE, MONSTRE… ; PERSONNAGE reste sur les groupes) ; vrai Anti-X ;
   évolution compatible du JSON et de l'UI. Avec les mots-clés de l'attaquant : avertissement non bloquant si une
   unité TITANIC tire en overwatch (snap shooting, décision du 2026-10-30, phase 3 étape 9).
2. **Mots-clés d'arme restants** influant sur les dégâts : liste à établir ensemble d'après les règles 11e édition.
3. **Moteur d'effets** : catalogue d'effets typés dans le Core (relances, modificateurs, PA, critiques sur X+, FNP,
   réduction de dégâts…), chacun avec une condition (a chargé, contre tel mot-clé, demi-portée…) et une étape de la
   séquence d'attaque où il s'applique. Étape la plus structurante de la roadmap.
4. **Stockage des règles** : règles d'armée, détachements et stratagèmes en JSON (`data/`), qui référencent les effets
   du catalogue ; service de catalogue. On stocke la mécanique, pas le texte des règles de GW.
5. **UI** : choix de la faction et du détachement, stratagèmes activables, effets actifs visibles.

À trancher en début de phase : traduction du contenu (noms de règles, de factions).

### Phase 9 : gestion et sauvegarde des unités

**Objectif** : bibliothèque d'unités, choix du loadout et des leaders.

1. **Modèle « fiche d'unité »** : caractéristiques, armes et options, mots-clés, composition (min / max), leaders
   compatibles, règles d'unité (effets de la phase 8).
2. **Bibliothèque d'unités** : `IUnitRepository`, en fichiers JSON locaux pour le desktop et stockage serveur pour le
   web.
3. **Éditeur d'unités** (UI) : créer, modifier, dupliquer, supprimer.
4. **Composition d'un scénario** : choix du loadout et du leader attaché → génération du `Scenario` (groupes d'armes de
   l'attaquant, groupes d'allocation et ordre du défenseur).
5. **Rapport en points** (#2 ; ancienne étape 9 de la phase 3, déplacée le 2026-10-30 pour traiter les points avec les
   fiches d'unité). Onglet du Full sim, dans l'esprit des onglets Groupes et Armes.
   - Points portés par la fiche d'unité (et par chaque leader), repris dans le scénario composé ; saisie au Full sim
     pour un scénario hors bibliothèque : à préciser. Points détruits et rendement de la phase 3, étape 4.
   - **Kill threat** (décision de l'utilisateur, 2026-10-28 : rentabilité) : distribution des points détruits par
     simulation (au prorata des PV, en points entiers arrondis à l'inférieur : exact pour P(≥ son coût)),
     P(détruire au moins sa propre valeur).
   - **Tanking** (décision de l'utilisateur, 2026-10-28 : les deux) : valeur conservée (points du défenseur qui
     survivent, en moyenne ; P(garder au moins la moitié de sa valeur), comptée exactement) et coût pour détruire
     (points de l'attaquant × PV de l'unité / moyenne des PV retirés **excédent compris**, approximation linéaire).
   - Tests : valeurs exactes en points.

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
   - Pas d'anticipation de la phase 3 (pas de comparaison, pas de distribution par groupe).
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
     utilisées par le moteur, la console et l'UI. Version minimale : la vraie gestion des mots-clés reste en phase 8.
   - Erreurs de `Scenario.Validate()` affichées clairement ; erreur de saisie de dés affichée sous le champ concerné ;
     bouton Lancer désactivé tant que le scénario est invalide.
   - Panneau de simulation : nombre de simulations (défaut 1 000 000), graine optionnelle, Lancer, barre de
     progression, Annuler.
   - Résultats en texte et tableaux (pas de graphique, c'est la phase 3), lus dans `SimulationResult` sans aucun
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
     la colonne P(= k) des tableaux (pas encore les graphiques de la phase 3).
   - Mise en page en deux colonnes : saisie à gauche, simulation et résultats à droite, toujours visibles.
   - Profils d'armes et groupes du défenseur repliables, avec un résumé en en-tête (`Describe()` du Core).
   - **Sélecteur de mots-clés** (idée de l'utilisateur, remplace les puces) : sur la carte d'une arme, les mots-clés
     actifs en étiquettes (rappel de règle au survol, croix pour retirer) et un bouton « Mots-clés » qui ouvre un
     **panneau latéral** : recherche (Entrée ajoute), filtre par catégorie, valeur modifiable sur place. Composant
     générique `KeywordPicker<TTarget>` + catalogue `KeywordDefinition` (dans l'UI : accès aux champs du formulaire
     seulement), réutilisable en phase 8 avec une portée (arme, unité…).
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
     filet (plus large) et étiquette du PERSONNAGE dans une teinte plus foncée du camp ; choix rapide du nombre de simulations en
     surbrillance.
   - **Marge (excédent de l'étape 4) clarifiée** : la moyenne des figurines tuées changeait avec le mode prolongé et
     dépassait l'effectif. Les tuiles restent toujours les pertes réelles ; « Afficher la marge » ajoute sous leur moyenne
     « +1,33 (Marge) », expliqué en infobulle, tiret si l'unité n'est jamais détruite (figurines et PV en plus, en
     moyenne quand l'unité est détruite : `SimulationResult.KillMarginWhenWiped` /
     `DamageMarginWhenWiped`, via `DiscreteDistribution.MeanExcessAbove`) et prolonge les tableaux, lignes au-delà
     de l'unité notées « 5 + 2 » sous un trait. La console affiche aussi la marge. Explication de la marge dans une bulle « ? »
     à côté de la valeur, comme ailleurs.
   - Fin d'étape : tag `v0.2a.1`.
9. **Choix du thème** (ajoutée après `v0.2a.1`, ticket #9). Quatre thèmes : Parchemin (clair), Grimdark, Acier et
   **Néon** (nuit indigo, violet pour l'attaquant, bleu pour le défenseur, halos et dégradés ; essai de la branche
   `essai/theme-neon`, validé par l'utilisateur). Décisions de l'utilisateur :
   - **Menu de 4 thèmes** (icône palette dans la barre de titre), qui remplace la bascule clair / sombre.
   - **Premier lancement** : Parchemin si le système est clair, Grimdark s'il est sombre ; ensuite, le dernier thème
     choisi, quel que soit le système (pas d'option « automatique »).
   - **Mémorisation par l'hôte** : `IPreferenceService` (Wh40kSim.UI), implémenté par `DesktopPreferenceService`
     (préférences MAUI) ; en web (phase 4), stockage du navigateur. L'hôte desktop lit le thème avant le premier affichage
     et le passe à `AppShell` (`InitialTheme`) : pas de flash, fond de la fenêtre et barre de chargement aux couleurs
     du thème.
   - Retour de l'utilisateur : poubelles (armes, groupes) de la couleur du camp, qui passent au rouge au survol.
   - `ThemeChoice` (Parchment, Grimdark, Steel, Neon) remplace `DarkStyle` ; `Wh40kTheme.For`, `IsDark`, `Default`,
     `Background`, `NameKey`.
   - Fin d'étape : tag `v0.2a.2`.

## Phase 3 : étapes

Découpage validé par l'utilisateur le 2026-10-27, revu le 2026-10-28 : le Full sim se construit écran par écran
(étapes 6 à 8) au lieu de tout calculer avant l'interface ; chaque rapport est une tranche complète (statistiques dans
`Wh40kSim.Simulation`, testées, puis l'écran qui ne fait que les dessiner). Chaque étape sera précisée et validée
avant d'être codée.

1. **Générateur aléatoire sans biais** (#10). `new Random(graine)` garde l'ancien algorithme de .NET, légèrement biaisé
   (~0,15 point de trop sur P(unité détruite) dans le cas mesuré, ~10 fois l'erreur statistique).
   - Générateur à graine écrit dans le Core (`Dice/`) : xoshiro256** initialisé par SplitMix64, utilisé par
     `RandomDiceRoller` ; tirage d'un dé sans biais de modulo. Vitesse mesurée (au moins aussi rapide).
   - Déterminisme à graine égale conservé, mais résultats changés une fois : références de `NonRegressionTests`
     recapturées (changement noté ici).
   - Test statistique contre une valeur théorique exacte (cas binomial du ticket, recréé dans le test), tolérance de
     quelques écarts-types.
2. **Écran d'accueil et navigation.** Tuiles vers les modes existants seulement (Quick sim, puis Full sim et
   Comparaison quand ils arrivent ; unités en phase 9). Routeur Blazor (`/`, `/quick-sim`…), utilisable tel quel par le
   client web ; barre de titre commune (thèmes, retour à l'accueil). `SimulatorPage` devient le Quick sim sans
   changement. Scénario courant tenu par un service (et non par la page) : conservé en changeant d'écran, partagé avec
   le Full sim. Décisions de l'utilisateur : noms « Quick sim » / « Full sim » gardés dans l'interface française ;
   accueil avec les seules tuiles des écrans existants ; retour à l'accueil bloqué pendant une simulation.
3. **JSON partiels.** Même format, sections facultatives : `{ "attacker": … }` (fichier attaquant), `{ "defender": … }`
   (fichier défenseur), les trois sections (scénario, inchangé). Boosts inclus dans leur partie. Lecture stricte (rien
   d'utile, ou une situation seule, est refusé). On peut reprendre l'attaquant ou le défenseur de n'importe quel
   fichier, scénario complet compris. Quick sim : Ouvrir / Enregistrer dans les cartes Attaquant et Défenseur (le
   scénario complet garde ses boutons) ; `IScenarioFileService` étendu, hôte desktop adapté. Premier pas vers la phase 9,
   sans figer la fiche d'unité. Décisions de l'utilisateur : « Ouvrir » de la barre accepte un fichier partiel et ne
   remplace que cette partie ; confirmation avant de remplacer une partie seulement si la saisie est modifiée (précisé
   le 2026-10-28 : si **cette partie** a été modifiée à la main depuis son dernier chargement ou enregistrement ;
   `ScenarioSession.AttackerDirty` / `DefenderDirty`, le scénario gardant son propre indicateur et son « * ») ; mêmes
   dossiers que les scénarios. Conséquence : un scénario complet exige désormais attaquant et défenseur (une partie
   absente était remplacée en silence par une unité vide).
4. **Points (#2, version légère).** Points optionnels sur l'attaquant et sur chaque groupe du défenseur (un leader a son
   propre coût) ; JSON et UI. Indicateurs dans Simulation : points détruits, rendement (points détruits / points de
   l'attaquant). Décisions de l'utilisateur : points détruits **au prorata des PV retirés** (calcul lissé : un char à
   moitié détruit vaut la moitié de ses points), via les PV retirés par groupe (`DefenderState.DamageInGroup`, cumulés
   par le simulateur, aucun dé consommé : non-régression au bit près) ; indicateurs seulement si **tous** les groupes ont
   des points ; saisie et résultats **au Full sim seulement** (le Quick sim n'analyse pas les points : aucun champ, mais il
   garde ceux d'un fichier ouvert) ; en attendant, points écrits dans le JSON et résultats affichés par la console.
   Marge exclue (on ne détruit pas plus que la valeur de l'unité). Ticket #2 commenté, pas fermé.
5. **Dégâts bruts (#6).** Dégâts de chaque attaque non sauvegardée comptés en entier (après réduction de dégâts et FNP),
   sans plafond par figurine ni arrêt à la destruction de l'unité ; distribution et gaspillage (bruts - PV retirés)
   dans Simulation. Fait tôt pour figurer dans le Full sim. Précisé et validé le 2026-10-27 :
   - Core : `DefenderState.RawDamage` cumule les dégâts perdus par chaque attaque avant plafonnement aux PV de la
     figurine (`lost` dans `ApplyDamage` : après boost de dégâts du défenseur, réduction et FNP), y compris contre les
     figurines virtuelles de l'excédent (pas d'arrêt à la destruction) et pour les mortelles de Devastating Wounds.
     Aucun dé consommé : non-régression au bit près.
   - Simulation : distribution des dégâts bruts ; gaspillage = bruts - (PV retirés + PV de l'excédent). Décision de
     l'utilisateur : le gaspillage ne compte que la perte **par figurine** ; ce qui dépasse la destruction de l'unité
     reste mesuré à part par la marge (pas de double compte).
   - Console : section « Dégâts bruts » (moyenne, gaspillage et sa part). Pas d'affichage dans l'interface (Full sim).
   - Tests : dés imposés (D3 contre 1 PV, réduction, FNP, Devastating Wounds), bruts ≥ PV retirés, égalité avec des
     armes D1, valeur exacte ; ticket #6 fermé par le commit (`Closes #6`).
Règles communes aux étapes 6 à 8 (décisions du 2026-10-28, à reprendre pour le rapport en points de la phase 9) :
- Aucun dé supplémentaire : non-régression au bit près. Le Core n'apporte que l'attribution nécessaire (pool qui a
  infligé chaque dégât, attaque « grâce à Precision »).
- Temps mesuré à chaque étape ; si les suivis ralentissent la boucle, option de simulation pour garder le Quick sim
  rapide.
- Résultats en records simples (sérialisables, contrat JSON de la phase 4).
- Indicateurs en points seulement si l'attaquant et tous les groupes ont des points (comme l'étape 4).
- **Console** (décision de l'utilisateur, 2026-10-28) : outil de debug, alignée sur le Quick sim ; les statistiques
  du Full sim n'y sont pas ajoutées (ce qu'elle affiche déjà, marge, points, dégâts bruts, reste).

6. **Full sim : squelette et graphiques.** Précisée avec l'utilisateur le 2026-10-28 :
   - **Graphiques d'abord** : prototype sur captures dans les 4 thèmes (critères : desktop et WebAssembly, couleurs
     de camp, barres + courbe sur le même graphique, infobulles, N courbes superposées pour la comparaison, poids,
     testable avec bUnit ; `MudChart` d'abord, composant SVG maison seulement si un critère échoue ; bibliothèque JS
     déconseillée). L'utilisateur choisit sur les captures ; page d'essai retirée ensuite.
     **Choix du 2026-10-28 : SVG maison** (prototype sur captures dans les 4 thèmes). MudChart n'a qu'un type par
     graphique (pas de barres + courbe), garde une taille fixe (texte minuscule, barres fines) et des infobulles
     brutes ; le SVG maison passe tous les critères, couleurs par le CSS du thème et du camp, personnalisable à volonté
     (marqueurs, infobulles détaillées, repères).
   - **Écran de saisie** (`/full-sim`, tuile sur l'accueil) : même scénario que le Quick sim (`ScenarioSession`) ;
     colonne Attaquant et colonne Défenseur côte à côte, Situation en pleine largeur dessous ; pas de résultats.
     **Bandeau en haut** : Nouveau / Ouvrir / Enregistrer et nom du fichier, puis les réglages du Quick sim (nombre de
     simulations, graine, Lancer, progression, Annuler). Erreurs de validation affichées comme au Quick sim. Mêmes
     raccourcis clavier. À la fin d'une simulation, ouverture de l'écran des résultats ; annulée, on reste sur la saisie.
   - **Écran des résultats** (`/full-sim/results`, même fenêtre, décision de l'utilisateur) : un onglet par graphique
     ou tableau de bord ; en-tête (scénario, simulations, graine, durée) ; retour à la saisie. Sans résultat : message
     qui renvoie à la saisie.
   - **Premier onglet « Distributions »** : le contenu des tableaux du Quick sim en **vrai histogramme** (décision de
     l'utilisateur) : figurines tuées et PV retirés, barres P(= k) et courbe P(≥ k), infobulle au survol, bascule
     « Afficher la marge » (prolongement au-delà de l'unité, comme au Quick sim).
   - **Résultat séparé** du Quick sim (décision de l'utilisateur) : le Full sim a son propre résultat, son nombre de
     simulations et sa graine dans `ScenarioSession` ; le scénario reste commun.
   - Quick sim inchangé à l'écran ; ce que les deux écrans partagent (barre de fichiers, confirmation avant de perdre
     une saisie, lancement et annulation) est mis en commun plutôt que copié.
   - Tests bUnit : navigation saisie → résultats, Lancer désactivé si invalide, résultat séparé, onglet sans résultat.
   - Retours de l'utilisateur intégrés (vérifiés sur captures) : libellés en mots (« Exactement », « Au moins »,
     Quick sim compris) ; accueil en colonne ; tuiles du Quick sim en tête de l'onglet (`SummaryTiles`), titre en
     bandeau au style du bouton Lancer, valeur à gauche, médiane / écart-type / 80 % à droite avec symboles et bulles
     « ? » ; histogramme : courbe lissée monotone (`MonotoneCurve`) passant par les seules valeurs possibles, barres
     « unité détruite » hachurées à 45°, moyenne en trait plein, limite de l'unité en pointillé, nombre exact sous
     l'axe, légende complète (moyenne marge comprise expliquée) ; queue de droite des distributions filtrée sur
     P(≥ k) (`DiscreteDistribution.Rows`, la colonne de l'unité ne disparaît plus avec la marge) ; cartes d'arme et
     de groupe découpées en lignes (en-tête, « Profil », « Mots-clés » / « Protections », filets, grilles CSS),
     bandeau du type d'arme (viseur / épées, Lucide) ; duplication d'un groupe ; champs de dés vérifiés à la sortie
     du champ ; confirmation par partie (attaquant / défenseur) ; fenêtre desktop rouverte agrandie.
7. **Rapport par groupe** (avec Precision). Précisée avec l'utilisateur le 2026-10-28, en partant de l'onglet voulu :
   nouvel onglet **« Groupes »** des résultats du Full sim, en trois sections ; pertes réelles seulement (pas de marge).
   - **1. Pertes par groupe** (« groupes de tanking ») : une carte par groupe d'allocation, dans l'ordre (numéro, filet
     et étiquette PERSONNAGE comme à la saisie) : figurines tuées en moyenne sur l'effectif, P(groupe entièrement tué)
     (= P(tuer le PERSONNAGE) pour un groupe PERSONNAGE), PV retirés en moyenne, petit histogramme (`HistogramChart`).
     Histogramme des **figurines tuées** pour un groupe de plusieurs figurines, des **PV retirés** pour une figurine
     seule (leader, véhicule : 0 ou 1 figurine n'apprendrait rien ; décision de l'utilisateur, à ajuster au besoin).
   - **2. Gardes du corps** : tous les groupes non-PERSONNAGES ensemble : P(tous les gardes du corps détruits, donc
     PERSONNAGES exposés), distribution et moyenne des figurines de garde tuées. Affichée seulement s'il y a au moins
     un PERSONNAGE (sinon elle répéterait l'onglet Distributions).
   - **3. Frappe Precision** : affichée si l'attaquant a une arme Precision et le défenseur un PERSONNAGE. Pour le seul
     PERSONNAGE visé (`Scenario.PrecisionTargetGroup` : la cible choisie, sinon le premier PERSONNAGE ; décision de
     l'utilisateur du 2026-10-29, les autres PERSONNAGES n'y ayant pas d'intérêt) — refaite avec l'utilisateur le
     2026-10-29, la tuile « grâce à Precision » étant peu parlante ;
     « armes Precision » = pools dont le profil a le mot-clé, un pool ne mélangeant jamais les deux) :
     - **barre « tué » en deux parties** : P(tué) = tué par les armes Precision seules (tous ses PV perdus viennent
       d'armes Precision) + tué avec l'aide des autres armes ;
     - **barre « PV retirés » en deux parties** : PV moyens retirés par les armes Precision + par les autres armes,
       sur ses PV ;
     - **tué alors que des gardes du corps vivaient encore** (kill impossible sans Precision) ;
     - **quand les armes Precision ne suffisent pas** (pas tué par elles seules) : PV qu'elles lui laissent en moyenne,
       et la probabilité de ce cas ;
     - **histogramme empilé** des PV retirés au PERSONNAGE : chaque barre P(= k) découpée selon la part moyenne des
       armes Precision et des autres armes dans les simulations qui donnent k.
     Écartés : « tué par Precision seule » en tuile (redondant avec la barre), gain par rapport à la même arme sans
     Precision (peu utile hors stratagème), Precision « gaspillée » (les sauvegardes ratées repartent sur les autres
     groupes). Le suivi « grâce à Precision » de la première version (`PrecisionKilledInGroup`…) reste calculé mais
     n'est plus affiché.
   - **Cible de Precision** (déjà en place : liste de la Situation et `context.precisionTarget` quand il y a plusieurs
     PERSONNAGES et une arme Precision, pour les unités avec un leader et un support). Fonctionnement confirmé par
     l'utilisateur : le pool Precision envoie ses sauvegardes ratées d'abord sur la cible choisie, puis, la cible
     morte, dans l'ordre d'allocation normal du défenseur ; une cible choisie est la **seule** visée (les pools
     Precision suivants ne visent pas l'autre PERSONNAGE) ; sans choix, chaque pool vise le premier PERSONNAGE en vie.
   - Core : `DefenderState` compte par groupe les figurines tuées et PV retirés par les attaques « grâce à
     Precision » (signalées par `AttackResolver`), et les PV retirés par des armes Precision
     (`PrecisionWeaponDamageInGroup`). Simulation : distributions des figurines tuées et des PV retirés
     par groupe, des figurines de garde tuées, P(entièrement tué), parts Precision, en records simples. Aucun dé en
     plus (non-régression au bit près) ; temps mesuré. Quick sim et console inchangés.
   - Tests : dés imposés (Precision avec et sans garde du corps, deux PERSONNAGES et cible choisie, figurine achevée
     par une attaque Precision) ; cohérence (P(dernier groupe entièrement tué) = P(unité détruite), somme des moyennes
     par groupe = moyenne totale) ; bUnit (sections affichées ou masquées selon le scénario).
   - Retours de l'utilisateur intégrés à la validation (2026-10-29, vérifiés à l'écran par lui) :
     - Onglet Groupes : tableau récapitulatif en tête (barre « Groupe détruit » à la couleur de fiabilité) ; sections
       repliables (`CollapsibleSection`), titres plus grands et espacés (rouge sang en Grimdark, blanc lumineux en
       Néon, pour les distinguer des sous-titres) ; groupes en sections sans cadre avec sous-titre ; deux histogrammes
       (figurines, PV) pour un groupe de plusieurs figurines ; tuiles dans l'ordre de l'onglet Distributions
       (figurines, PV, destruction à la couleur de fiabilité) ; gardes du corps : PV retirés (`BodyguardDamage`) ;
       Frappe Precision refaite (voir ci-dessus), pour le seul PERSONNAGE visé.
     - Résultats du Full sim à la couleur du thème, sans couleur de camp (`wh-report`) ; grille secondaire en pointillé
       (5 parts) sur les histogrammes ; barre d'onglets collante, avec un retour compact (flèche ronde) une fois
       collée (`watchSticky`, wh40ksim.js) ; dernier onglet retenu pour un même résultat (`SimulationRun.ResultsTab`).
     - Résultats périmés : avertissement au Full sim (revoir les anciens résultats, relancer, Annuler / Échap, choix
       retenu pour la session) et bandeau au Quick sim, d'après `ScenarioSession.IsStale`.
     - Saisie : Situation en tête ; cible de Precision sur les cartes PERSONNAGE (rouge clignotante / grise, synchronisée
       avec la liste de la Situation ; dès un seul PERSONNAGE) ; cible supprimée, décochée ou renommée suivie ; armes
       réordonnables (flèches) ; panneaux Attaquant / Défenseur repliables sans perdre l'état des cartes ; bouton de
       repli commun (`CollapseToggle`) à gauche partout ; panneau des mots-clés fermé au clic en dehors.
     - Quick sim : lignes à 0,0 % masquées dans les tableaux (`DiscreteDistribution.Rows(skipNegligibleInside)`).
8. **Rapport par arme** (avec les dégâts bruts). Précisée avec l'utilisateur le 2026-10-29, en partant de l'onglet
   voulu : nouvel onglet **« Armes »** des résultats du Full sim, à la couleur du thème, en sections repliables sans cadre
   (comme l'onglet Groupes). Découpage **par pool** (armes aux attaques identiques lancées ensemble : dés
   indiscernables), nommé d'après ses armes (« Bolter + Pistolet bolter »), dans l'ordre de résolution ; figurine
   attribuée à l'attaque qui l'achève ; pertes réelles seulement (pas la marge).
   - **1. Récapitulatif par arme** : **barre multicolore** de la répartition des PV retirés entre les pools (une bande,
     une couleur par pool, légende), puis un tableau, une ligne par pool : PV retirés (moyenne, part du total),
     figurines tuées (moyenne, part du total), dégâts bruts (moyenne), gaspillage (part des dégâts bruts perdus).
   - **2. Une section par pool** (sous-titre comme les groupes) : tuiles figurines tuées, PV retirés, dégâts bruts /
     gaspillage, et **« Tire sur une unité déjà détruite »** = P(unité détruite avant que ce pool ne tire) (utile pour
     l'ordre des armes, prépare l'optimiseur de la phase 6) ; histogrammes des figurines tuées puis des PV retirés par ce pool.
   - **3. Gaspillage, toutes armes confondues** (dégâts bruts de l'étape 5, jamais affichés jusqu'ici) : tuiles dégâts
     bruts moyens, gaspillage et sa part, « aucune perte » (P(gaspillage = 0)), « perdu quand l'unité survit » (le
     gaspillage qui coûte : quand l'unité est détruite, il n'a rien coûté) ; barre retirés / perdus ; **histogramme
     des PV perdus par activation** (`SimulationResult.Waste`, `MeanWasteWhenUnitSurvives`), **empilé par arme** aux
     couleurs de la barre de répartition (part moyenne de chaque pool dans les k PV perdus, marge comprise :
     `DefenderState.WasteByPool`, `WeaponPoolLosses.WasteShareByTotal`, `HistogramChart.Stack`). Retour de l'utilisateur :
     l'histogramme des bruts superposé aux PV retirés de la première version ne montrait pas ce qui est perdu. Dans
     cet onglet pour l'instant (décision de l'utilisateur : à déplacer dans Distributions si elle s'y insère mieux).
   - **Conversion des attaques** (ajoutée à la demande de l'utilisateur, 2026-10-29) : section avec un tableau par pool,
     attaques → touches (part des attaques) → blessures (part des touches) → sauvegardes ratées (part des blessures),
     moyennes par activation où le pool tire sur une unité en jeu (dés comptés jusqu'au bout si l'unité meurt pendant
     qu'il tire). Sous-lignes, seulement si le mot-clé joue : attaques de Blast et Rapid Fire, touches de Sustained
     Hits, blessures Lethal Hits (avec le gain estimé : touches Lethal qui auraient raté le jet de blessure, au taux de
     l'arme), Devastating Wounds (avec les sauvegardes évitées estimées). Core : `PoolConversion`
     (`AttackResolver.ConversionOf`) ; Simulation : `WeaponConversion` (`WeaponPoolLosses.Conversion`, null si le pool
     ne tire jamais sur une unité en jeu). Aucun dé en plus ; temps inchangé (médianes 3,69 s → 3,72 s, mesurées en
     alternance).
   - Retours de l'utilisateur : plus haute valeur de chaque colonne du tableau en gras (gaspillage en teinte
     d'alerte) ; histogrammes des figurines tuées puis des PV retirés par pool (`WeaponPoolLosses.Kills`) ; barre
     dégâts bruts retirés / perdus par pool (partie perdue en teinte d'alerte).
   - **Gaspillage par pool** (décision de l'utilisateur) : les dégâts bruts d'un pool ne comptent que jusqu'à la
     destruction de l'unité ; gaspillage = bruts − PV retirés par ce pool (perte par figurine). Les attaques tirées
     sur une unité déjà détruite ne sont pas du gaspillage : l'indicateur « tire sur une unité déjà détruite » les
     mesure. (Le gaspillage global de l'étape 5, lui, compte aussi la marge : il reste tel quel.)
   - Core : `DefenderState` compte par pool (indice dans `AttackResolver.Pools`) les PV retirés, les figurines
     achevées et les dégâts bruts de la séquence réelle ; le résolveur note si l'unité était déjà détruite au début de
     chaque pool. Aucun dé en plus (non-régression au bit près). Simulation : distributions des PV retirés et des figurines par pool,
     moyennes des bruts et du gaspillage, P(unité détruite avant le pool), en records simples ; temps
     mesuré. Quick sim et console inchangés.
   - Tests : dés imposés (attribution au pool, attaque qui achève, bruts arrêtés à la destruction, pool tiré sur une
     unité détruite) ; cohérence (sommes par pool = totaux réels, parts = 100 %) ; bUnit (onglet, tableau, barre,
     sections par pool, dégâts bruts).
   Roadmap revue le 2026-10-30 (décisions de l'utilisateur) : l'ancienne étape 9, **rapport en points** (#2), est
   passée en phase 9 (unités) ; la comparaison et l'optimiseur forment leurs propres phases, 5 et 6, après le web
   (phase 4), chacun étant un mode à part sur l'accueil. Il ne reste que l'overwatch.
9. **Overwatch** (#11, ajouté le 2026-10-29). Interrupteur « Tir en overwatch » dans la **Situation** (Quick sim et Full sim,
   décision de l'utilisateur) : le Core résout l'attaque avec les restrictions de l'overwatch, les écrans et les
   statistiques restent les mêmes, résultats marqués « Overwatch » ; mode enregistré dans le JSON du scénario.
   Précisée avec l'utilisateur le 2026-10-30, d'après une analyse des règles V11 qu'il a fournie (dataslates jusqu'au
   30/09/2026). L'Overwatch (stratagème 15.08, fin de la phase de Mouvement adverse) tire selon la règle générique
   **Snap Shooting (15.09)**, seule utile au moteur :
   - **Jet de touche** (seul changé) : touche sur un 6 non modifié seulement, quelle que soit la CT ; tous les
     modificateurs ignorés (couvert, boosts de CT / CC et de jet de touche des deux camps, Heavy) ; aucune relance de
     touche ; ces 6 **ne sont pas des touches critiques** (dataslate du 30/09/2026, texte de l'application ; cité par
     Wahapedia) : ni Lethal Hits, ni Sustained Hits, « touche critique sur X+ » sans effet (FAQ d'août 2026). Torrent
     touche toujours automatiquement.
   - **Inchangé** : nombre d'attaques (A, Blast, Rapid Fire à demi-portée), jet de blessure (modificateurs, relances,
     Twin-linked, blessure critique sur X+, Anti-X, Devastating Wounds), PA, sauvegardes, dégâts, Melta, FNP,
     réduction de dégâts, allocation, Precision.
   - **Situation** : « A chargé » et « Immobile » sans objet (Lance ne joue jamais, Heavy est un modificateur de
     touche) : masqués quand l'interrupteur est actif ; « demi-portée » gardée (cible à 24" ou moins).
   - **Armes de mêlée** (décision de l'utilisateur) : gardées dans le scénario, mais ne tirent pas en snap shooting ;
     **avertissement non bloquant** qui le dit. Nouvelle notion du Core : `Scenario.Warnings()` (messages qui
     n'empêchent pas de lancer, à côté de `Validate()`), affichés par l'interface dans une teinte d'alerte distincte
     des erreurs. Les pools de mêlée sont écartés de la résolution (absents de l'onglet Armes). Sans aucune arme de
     tir, la simulation ne mesurerait rien : erreur de validation qui bloque le lancement (décision de
     l'utilisateur).
   - **Restrictions de jeu hors du moteur** (décision de l'utilisateur) : portée, visibilité, unité non engagée,
     coût en PC laissés au joueur (rappel dans la bulle « ? »). Seule exception voulue : avertissement non bloquant si
     l'unité qui tire est TITANIC (exclue par les règles) ; l'attaquant n'a pas encore de mots-clés d'unité, donc
     **créé en phase 8** avec les mots-clés (décision de l'utilisateur), rappel dans la bulle « ? » d'ici là.
   - Core : `AttackContext.SnapShooting` (nom générique, décision de l'utilisateur : le snap shooting sert aussi au
     tir indirect et à des règles de faction, réutilisable en phase 8) ; `AttackResolver.ResolveHits` (un D6 par
     attaque, 6 = touche ordinaire, Torrent automatique) ; non-régression au bit près sans snap shooting.
   - JSON : `context.snapShooting` (absent = `false`), lecture stricte.
   - UI : interrupteur « Tir en overwatch » dans la Situation, avec bulle « ? » (règle résumée, restrictions laissées au
     joueur) ; boosts sans effet **grisés** quand il est actif (CT / CC, jet de touche, relance des touches de
     l'attaquant ; couvert, CT / CC, jet de touche du défenseur), valeurs conservées ; mention « Overwatch » sur les
     résultats du Quick sim et du Full sim. Console : mention « Overwatch » dans le rapport.
   - Tests : dés imposés (5 qui rate malgré les boosts, 6 sans Lethal ni Sustained, aucune relance, critique sur X+
     sans effet, Torrent, Devastating Wounds actif, mêlée écartée), avertissements, JSON ; bUnit (interrupteur,
     options masquées, boosts grisés, avertissement, mention sur les résultats). Ticket #11 fermé par le commit.
10. **Relances par arme : « pêche aux critiques »** (ajoutée le 2026-10-30, idée de l'utilisateur, à coder après la validation
   de l'étape 9). Relancer aussi les réussites ordinaires pour chercher un critique n'a de sens qu'avec un effet sur
   critique, et seulement pour les armes qui en ont un : la stratégie se choisit **pool par pool**, plus au global.
   Nommée « pêche aux critiques » plutôt que « pêche aux 6 » (décision de l'utilisateur : critiques sur 5+, 4+… à
   venir).
   Décisions de l'utilisateur :
   - **Boosts** (relance des touches, relance des blessures) : « tout sauf critique » et « les échecs » disparaissent,
     remplacés par une **relance complète**, qui relance par défaut les échecs (comportement de l'ancien « les
     échecs »). Choix restants : aucune, les 1, relance complète. JSON : la valeur `Failed` reste et désigne la
     relance complète ; `NonCritical` est **refusé à la lecture** (JSON strict, message clair).
   - **Sur chaque arme**, choix « pêche aux critiques », affiché seulement si les conditions sont réunies (sinon absent) :
     - touche : l'arme a un effet sur touche critique (Lethal Hits, Sustained Hits) et l'attaquant a la relance
       complète des touches (jamais avec Torrent, ni en overwatch : pas de relance de touche) ;
     - blessure : l'arme a Devastating Wounds et la relance complète des blessures est disponible : boosts de
       l'attaquant, ou **Twin-linked** (qui permet de relancer le jet de blessure, décision de l'utilisateur).
     Le moteur relance tout sauf les critiques pour cette arme seulement, et seulement si les conditions sont encore
     réunies (un choix resté coché après le retrait du mot-clé ou de la relance est ignoré).
   - **Pools** (décision de l'utilisateur) : le choix fait partie de l'identité du pool ; deux armes identiques avec un
     choix différent forment deux pools (une seule stratégie par groupe de dés), visibles séparément dans l'onglet Armes.
   - Core : `RerollPolicy` sans `NonCritical` ; `WeaponProfile.FishForCriticalHits` / `FishForCriticalWounds` (JSON de
     l'arme, absents = `false`) ; conditions portées par le Core (ex. `CanFishForCriticalHits(boosts, context)`),
     utilisées par le moteur, la console et l'interface ; `AttackPool.MakeIdenticalAttacks` compare les deux choix.
   - UI : liste de relance des boosts à trois choix ; sur la carte d'une arme, **deux interrupteurs indépendants**
     « Pêche aux critiques (touche) » et « Pêche aux critiques (blessure) » (une arme avec des effets critiques à la touche et à la
     blessure a les deux, précisé par l'utilisateur), avec bulle « ? », chacun seulement quand il a un sens. Console :
     mêmes choix.
   - Non-régression : le scénario riche de `NonRegressionTests` relance les blessures en « tout sauf critique » pour
     toutes ses armes, dont deux sans Devastating Wounds : plus exprimable, ses références seront recapturées une fois
     (changement noté ici) ; l'autre scénario reste identique au bit près.
   - Tests : dés imposés (pêche aux critiques sur une arme, relance des échecs sur l'autre dans le même scénario,
     Twin-linked + Devastating Wounds, choix ignoré si les conditions disparaissent, pools séparés), JSON (`NonCritical`
     refusé), bUnit (interrupteurs affichés ou non selon les mots-clés et les boosts).
   - Fin de phase : README, tag `v0.3`.

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
- **Web (phase 4)** : Blazor Web App, serveur `Wh40kSim.Web` (API + client) et client WebAssembly
  `Wh40kSim.Web.Client` ; le calcul se fait sur le serveur. **Client compilé AOT** (décision du 2026-10-30 après essai :
  affichage interprété trop lent, ~1 s par changement d'écran ; nettement plus fluide en AOT) : premier téléchargement
  6,7 Mo en Brotli au lieu de 3,2 (31 Mo décompressés au lieu de 10,7), publication d'environ 4 min. **Demandée dans
  la commande de publication** : `dotnet publish src/Wh40kSim.Web -c Release -p:RunAOTCompilation=true` (vérifier :
  `dotnet.native*.wasm` d'environ 23 Mo au lieu de 2,8). Pas dans le `.csproj` : .NET exigerait alors `wasm-tools`
  dès la restauration et le build (CI Linux en échec, constaté) ; et une condition sur `_IsPublishing` n'est pas vue
  par le client quand le serveur le publie (constaté : publication sans AOT). Depuis Rider : interprété.
  `wasm-tools` nécessaire seulement pour publier : machines qui publient, image Docker.
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
- **IScenarioFileService** (depuis la phase 3, étape 3) : l'hôte ne manipule que du texte. `OpenAsync()` →
  `OpenedFile(FileName, Content)` ou null si annulé ; `SaveAsync(content, suggestedFileName)` → nom du fichier écrit ou
  null si annulé. L'interface interprète le contenu avec le Core : `ScenarioJson.ReadFile` (scénario complet ou
  partiel, `ScenarioFile` et `Kind`), `Deserialize` (scénario complet exigé), `Serialize` (scénario, attaquant seul,
  défenseur seul).
- **Traduction** préparée dès la phase 2 : textes de l'UI dans des `.resx` (français seulement pour l'instant), nombres
  formatés selon la culture de l'appli, fixée à fr-FR (pas celle de la machine). Messages du Core et anglais : phase 7.
  Traduction du contenu (règles d'armée, unités) : à trancher en phase 8.
- **Endurance** : portée par chaque groupe, valeur par défaut E4 conservée (un groupe JSON sans `toughness` reçoit E4).
  Les anciens fichiers avec `toughness` sur le défenseur sont refusés au chargement (JSON strict, testé).
- **Tests aller-retour** : comparer les scénarios via `ScenarioJson.Serialize` (les records contenant des listes ne sont
  pas égaux par valeur).
- **Composants (Wh40kSim.UI/Components)** : le scénario courant (`ScenarioForm`, fichier, modifications, simulation en
  cours) est tenu par `ScenarioSession` (service scoped, enregistré par `AddWh40kSimUI`), qui survit au changement
  d'écran ; chaque mode y a ses réglages et son dernier résultat (`SimulationRun` : `QuickSim`, `FullSim`), effacés
  tous deux quand un autre scénario est chargé. Les écrans de saisie (`SimulatorPage`, `FullSimPage`) héritent de
  `ScenarioEditorPage` (fichiers, confirmation, validation, lancement, raccourcis ; `Run` désigne le mode,
  `OnSimulationCompleted` la suite), avec `ScenarioToolbar` et `KeywordDrawer` en commun ; ils revalident le scénario
  (`ToScenario()` puis `Validate()`) à chaque `Changed` remonté par un panneau. Graphiques : composants SVG maison
  (`HistogramChart`), couleurs par le CSS (`--wh-accent` du camp), libellés en mots (« Exactement », « Au moins »)
  plutôt qu'en notation P(= k) ; « 1 chance sur N » essayé puis retiré (redondant avec le pourcentage). Les options de situation dépendent du dernier scénario lisible
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
  fournisseurs MudBlazor, barre de titre (retour à l'accueil, nom de l'écran, menu des thèmes), puis le routeur Blazor
  vers les écrans (`HomePage` `/`, `SimulatorPage` `/quick-sim`, `FullSimPage` `/full-sim`, `FullSimResultsPage`
  `/full-sim/results` ; adresses dans `AppRoutes`). Ouverture d'un écran
  depuis l'accueil par `ScreenNavigator` : barre de chargement dessinée avant la construction de l'écran (première
  construction lente : compilation à la volée), retirée ensuite. Pas de `RegisterLocationChangingHandler` asynchrone :
  bUnit ne termine pas la navigation dans ce cas. Réutilisable tel quel par
  l'hôte web (phase 4). L'hôte fournit `MauiProgram` (services), la fenêtre (`App` : titre lu dans `Strings.resx`,
  1400 × 900 centrée, minimum 900 × 600, thème au démarrage), `DesktopScenarioFileService`,
  `DesktopPreferenceService` et `wwwroot/index.html` (CSS / JS de MudBlazor). Pas de `Package.appxmanifest`
  (application non empaquetée). Démarrage sans flash : l'hôte passe le thème (mémorisé, sinon selon Windows) à
  `AppShell` (`InitialTheme` ; sans lui, préférence puis mode du navigateur, après le premier rendu) ; fond de la page
  MAUI aux couleurs du thème, `index.html` transparente avec une barre de chargement colorée selon l'attribut
  `data-theme`, posé par un script de `index.html` d'après l'adresse de départ (`MainPage` : `StartPath = "/?theme=…"` ;
  un script injecté « à la création du document » arrivait parfois trop tard), WebView2 transparente jusqu'au premier
  rendu.
- **Vérification visuelle** : pas de test automatique du rendu ; c'est l'utilisateur qui teste à l'écran (voir
  « Règles de travail »). Sur demande explicite seulement : captures d'écran de la fenêtre par un script PowerShell
  (`CopyFromScreen`, clic et molette via `user32`) dans le scratchpad de Claude, lues comme des images, puis supprimées.

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
- Charge de travail `wasm-tools` (`dotnet workload install wasm-tools`, terminal administrateur pour
  `C:\Program Files\dotnet`) : nécessaire pour **publier** la version web (compilation AOT du client) ; build, tests et
  lancement depuis Rider fonctionnent sans elle.
- CLI GitHub (tickets) : `winget install --id GitHub.cli -e --source winget --accept-source-agreements
  --accept-package-agreements` (sans ces options, winget bloque sur les conditions du Microsoft Store), puis
  `gh auth login` dans un terminal séparé (questions interactives : GitHub.com, HTTPS, navigateur). Une session ouverte
  avant l'installation n'a pas `gh` dans son PATH : `C:\Program Files\GitHub CLI\gh.exe`. Sans droits
  administrateur (le paquet winget est un MSI pour toute la machine) : zip officiel des Releases de `cli/cli`
  (empreinte SHA256 vérifiée avec le fichier `checksums.txt`), dézippé dans `%LOCALAPPDATA%\Programs\GitHub CLI`, et
  son dossier `bin` ajouté au PATH de l'utilisateur ; pas de mise à jour automatique.
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
- Phase 2a, étape 8 (modifications de caractéristiques) : fusionnée dans `main`, **tag `v0.2a.1`**.
  275 tests verts (145 Core, 35 Simulation, 95 UI) ;
  `NonRegressionTests` inchangés (sans modification, résultats identiques au bit près).
- Phase 2a, étape 9 (choix du thème) : fusionnée dans `main`, **tag `v0.2a.2`**. 280 tests verts (145 Core,
  35 Simulation, 100 UI). Branche `essai/theme-neon` reprise dans l'étape, puis supprimée.
- Phase 2a validée par l'utilisateur ; roadmap renumérotée le 2026-10-27 : graphiques et comparaison en 3,
  traduction en 4, web (ex-2b) en 7.
- Phase 3 découpée le 2026-10-27 (voir « Phase 3 : étapes ») : #10, #2 (version légère), #6 et #1 y entrent.
- Phase 3, étape 1 (générateur sans biais, #10) : fusionnée dans `main`.
  295 tests verts (154 Core, 41 Simulation, 100 UI). Vérifié avant de recapturer `NonRegressionTests` : cas à
  valeur exacte conformes à la théorie (20 graines × 4 M, |z| < 2 ; l'ancien générateur s'en écartait, jusqu'à
  z ≈ -5,6) ; scénarios sans valeur exacte conformes à un générateur indépendant (`new Random()` de .NET, 32 M) ;
  l'ancien surestimait les PV retirés d'environ 0,1 % sur le scénario riche (≈ 16 écarts-types). ~28 % plus rapide.
- Phase 3, étape 2 (accueil et navigation) : fusionnée dans `main`. 302 tests verts (154 Core, 41 Simulation,
  107 UI ; UI stables sur 8 exécutions). Skill `/valider-etape` ajouté dans la même branche.
- Phase 3, étape 3 (JSON partiels) : fusionnée dans `main`. 325 tests verts (166 Core, 41 Simulation, 118 UI ;
  UI stables sur 6 exécutions).
- Barre de chargement à l'ouverture d'un écran (gel au premier clic sur Quick sim, signalé par l'utilisateur), avec
  fond uni du thème, et barre du démarrage enfin aux couleurs du thème mémorisé : fusionnées dans `main`. 327 tests
  verts (166 Core, 41 Simulation, 120 UI ; UI stables sur 6 exécutions).
- Phase 3, étape 4 (points, #2 version légère) : fusionnée dans `main` (ticket #2 commenté, laissé ouvert).
  340 tests verts (172 Core, 48 Simulation, 120 UI) ; `NonRegressionTests` inchangés. Champs de points retirés du
  Quick sim à la demande de l'utilisateur (saisie au Full sim).
- Skill `/debut-journee` (synchronisation, build et tests complets, point du jour) : fusionné dans `main`.
  340 tests verts (172 Core, 48 Simulation, 120 UI).
- Phase 3, étape 5 (dégâts bruts, #6) : fusionnée dans `main` (ticket #6 fermé). 349 tests verts (178 Core,
  51 Simulation, 120 UI) ; `NonRegressionTests` inchangés (aucun dé supplémentaire).
  Gaspillage déduit des moyennes (bruts - PV retirés et excédent), sans distribution propre.
- Fin de la phase 3 redécoupée le 2026-10-28 (Full sim écran par écran, voir « Phase 3 : étapes ») ; définitions
  du kill threat, du tanking et de la part de Precision arrêtées avec l'utilisateur (étapes 7 et 9).
- Skill `/voir-sc` (capture d'écran du presse-papiers) : fusionné dans `main`, pendant l'étape 6.
- Phase 3, étape 6 (Full sim : squelette et graphiques) : fusionnée dans `main`. 377 tests verts (178 Core,
  52 Simulation, 147 UI) ; `NonRegressionTests` inchangés. Prochaine étape : 7 (rapport par groupe, avec Precision),
  à préciser.
- Distribution pour les tests (branche `outillage/distribution-apercu`) : fusionnée dans `main`. Zip autonome
  (`tools/publier.ps1`, profil `win-x64`), version affichée sur l'accueil ; préversion **`v0.3-apercu.1`** publiée
  dans les Releases GitHub (avec les scénarios locaux de l'utilisateur, accord du 2026-10-28). 379 tests verts
  (178 Core, 52 Simulation, 149 UI). Retours du testeur attendus (tickets ou messages).
- Installateur (branche `outillage/installateur`) : fusionné dans `main`, préversion **`v0.3-apercu.2`** publiée
  avec le `setup.exe`. Retour du testeur, racine du zip trop
  chargée (122 dossiers de langues, 432 fichiers). `tools/publier.ps1` produit maintenant
  `dist/Wh40kSim-<version>-setup.exe` (Inno Setup, `tools/installateur.iss`) : installation sans droits
  administrateur, menu Démarrer, scénarios dans `Documents\Wh40kSim\scenarios` (dossier de départ de l'enregistrement
  hors du dépôt) ; traductions limitées au français et à l'anglais (75 Mo au lieu de 109). Version `0.3-apercu.2`.
  Installation, lancement et désinstallation vérifiés sur cette machine. 379 tests verts. Inno Setup à installer sur
  l'autre machine (`winget`, voir README).
- Dépôt déplacé sur cette machine vers `E:\Dev\WH40k_Companion_App` (disque C plein) le 2026-10-28 : sans effet sur
  le dépôt ni sur l'autre machine.
- Phase 3, étape 7 (rapport par groupe) : fusionnée dans `main`, validée par l'utilisateur à l'écran le 2026-10-29
  après de nombreux retours (voir l'étape 7 dans « Phase 3 : étapes »). Onglet « Groupes » (`GroupsReport`) ;
  `GroupLosses`, `BodyguardKills` / `BodyguardDamage`, `PrecisionWeaponLosses` dans `SimulationResult` ;
  `DefenderState.PrecisionWeaponDamageInGroup` ; `Scenario.PrecisionTargetGroup`. 439 tests verts (189 Core,
  63 Simulation, 187 UI) ; `NonRegressionTests` inchangés. Temps mesuré (10 M simulations) : 1,77 s sur `main`, 1,87 s
  sur la branche (scénario d'exemple) ; suivi des armes Precision : médianes 3,76 s → 4,02 s sur un scénario avec
  Precision, dans le bruit de mesure, donc pas d'option. Prochaine étape : 8 (rapport par arme), à préciser.
- `gh` (installé depuis le zip officiel, sans droits administrateur), charge de travail MAUI et Inno Setup sont
  maintenant installés sur les deux machines (2026-10-29).
- Roadmap complétée le 2026-10-29 (branche `docs/roadmap-optimiseur-overwatch`) : optimiseur du séquencement des
  armes en étape 12 (#3, commenté), overwatch en étape 13 (interrupteur de la Situation, ticket #11 créé) ; fin de phase 3 déplacée à l'étape 13.
  Prochaine étape inchangée : 8 (rapport par arme), à préciser.
- Phase 3, étape 8 (rapport par arme) : fusionnée dans `main`, validée par l'utilisateur à l'écran le 2026-10-29
  après plusieurs retours (voir l'étape 8 dans « Phase 3 : étapes »). Onglet « Armes » (`WeaponsReport`) : répartition
  (`ShareBar`, plus haute valeur de chaque colonne en gras), une section par pool (deux histogrammes, barre retirés /
  perdus), conversion des attaques (`PoolConversion`, `WeaponConversion`), gaspillage par activation empilé par arme
  (`SimulationResult.Waste`, `HistogramChart.Stack`) ; `WeaponPoolLosses` (`SimulationResult.Pools`) ;
  `DefenderState.DamageByPool` / `KillsByPool` / `RawDamageByPool` / `WasteByPool` / `PoolFiredOnDestroyedUnit`.
  460 tests verts (194 Core, 70 Simulation, 196 UI) ; `NonRegressionTests` inchangés. Temps mesuré (10 M simulations,
  scénario d'exemple) dans le bruit de mesure à chaque ajout, donc pas d'option. Prochaine étape : 9 (rapport en
  points, #2), à préciser.
- Roadmap revue le 2026-10-30 (branche `docs/roadmap-points-phase6`) : le rapport en points (#2) passe en phase 6,
  étape 5, avec les fiches d'unité ; fin de la phase 3 renumérotée (comparaison en 9 et 10, optimiseur en 11,
  overwatch en 12, tag `v0.3` en fin de phase). Prochaine étape : 9 (comparaison, côté Simulation, #1), à préciser.
- Phases réordonnées le 2026-10-30 (branche `docs/roadmap-web-avant-comparaison`) : phase 3 « graphiques et Full sim »
  terminée par l'overwatch (étape 9, tag `v0.3`), puis web (4), comparaison (5), optimiseur (6), traduction (7),
  mots-clés et règles (8), unités (9, rapport en points en étape 5). Après le web, chaque phase livre sa partie web.
  Objectif de la phase 3 raccourci avec l'accord de l'utilisateur (comparaison passée en phase 5). Commentaires du
  code et README alignés. Prochaine étape : phase 3, étape 9 (overwatch, #11), à préciser.
- Phase 3, étape 9 (overwatch, #11) : précisée avec l'utilisateur le 2026-10-30 (snap shooting V11), fusionnée dans
  `main`, validée par l'utilisateur à l'écran le 2026-10-30 (ticket #11 fermé). Prochaine étape : 10 (relances par
  arme, « pêche aux critiques »), précisée, à coder sur sa propre branche ; tag `v0.3` à sa fin.
  `AttackContext.SnapShooting`, `Scenario.FiringWeapons` / `Warnings()` / `HalfRangeMatters` / `RemainedStationaryMatters`
  / `ChargedMatters` ; interrupteur dans `ContextPanel`, boosts de touche grisés (`BoostRowTitle`), avertissements dans
  `ScenarioErrors`, étiquette `OverwatchChip` sur les résultats. Retour de l'utilisateur : sous-ligne « Torrent :
  touches automatiques » dans la conversion des attaques (onglet Armes) ; gaspillage nul pour tous les pools : colonne
  dans la teinte positive plutôt qu'en alerte ; sélecteur de mots-clés : un clic sur la tuile active / désactive le
  mot-clé (la case et le champ de valeur gardent leurs clics). 487 tests verts (210 Core, 72 Simulation, 205 UI ; UI
  stables sur 3 exécutions) ; `NonRegressionTests` inchangés. Étape 10 (relances par arme, « pêche aux critiques ») ajoutée
  à la roadmap le 2026-10-30 ; tag `v0.3` reporté à sa fin. À coder après la validation de l'étape 9.
- Correctif du gel pendant une longue simulation (#12) : fusionné dans `main`, validé à l'écran par l'utilisateur le
  2026-10-30 (ticket #12 fermé) : `Simulator.Run` accepte `SimulationThreads` (nombre de threads, ordonnanceur ; résultats
  identiques au bit près) ; `LocalSimulationService` calcule sur tous les cœurs sauf un, sur des threads en priorité
  basse (`LowPriorityTaskScheduler`, indisponible dans le navigateur : ancien chemin) ; barre de progression redessinée
  à chaque point de pourcentage seulement. Temps mesuré (10 M simulations, 12 cœurs, scénario d'exemple) : ~1,3 s
  contre ~1,36 s, environ 5 % plus lent. 491 tests verts (210 Core, 75 Simulation, 206 UI).
- Phase 3, étape 10 (relances par arme, « pêche aux critiques ») : fusionnée dans `main`, validée par l'utilisateur
  le 2026-10-30. `RerollPolicy` sans `NonCritical` (refusé par `RerollPolicyJsonConverter`, message clair) ;
  `WeaponProfile.FishForCriticalHits` / `FishForCriticalWounds` (écrits seulement s'ils sont cochés),
  `CanFishForCriticalHits` / `CanFishForCriticalWounds` ; choix compris dans l'identité du pool ; interrupteurs sur la
  carte d'arme (`WeaponEditor`, ligne « Relances »), liste de relance à trois choix (« Relance complète »), console.
  Scénario riche de `NonRegressionTests` recapturé (relance « tout sauf critique » plus exprimable), l'exemple
  identique au bit près ; aucun scénario de `scenarios/` n'utilisait `NonCritical`. Avec le correctif #12 : 509 tests
  verts (223 Core, 74 Simulation, 212 UI). Dernière étape de la phase 3 : tag `v0.3` proposé (à poser avec l'accord
  de l'utilisateur). Prochaine phase : 4 (intégration web), à préciser.
- Tag **`v0.3`** posé sur `main` le 2026-10-30 (fin de la phase 3).
- Phase 4 découpée avec l'utilisateur le 2026-10-30 (branche `docs/phase4-decoupage`) : mise en ligne publique sans
  compte (image Docker, déploiement par GitHub Actions), résultat en histogrammes bruts, progression en SSE, plafond
  de 1 000 000 simulations, calculs simultanés et lancements par IP limités ; cinq étapes (contrat du résultat,
  serveur et API, client WebAssembly, intégration continue #7, Docker et mise en ligne). Prochaine étape : 1 (contrat
  du résultat), à préciser.
- Phase 4, étape 1 (contrat du résultat) : fusionnée dans `main`, validée par l'utilisateur le 2026-10-30.
  Prochaine étape : 2 (serveur et API), à préciser (valeurs des limites à trancher).
- Phase 4, étape 2 (serveur et API) : fusionnée dans `main`, validée par l'utilisateur après essai le 2026-10-30.
  Prochaine étape : 3 (client WebAssembly), à préciser.
- Phase 4, étape 3 (client WebAssembly) : fusionnée dans `main`, validée par l'utilisateur après essai dans Firefox
  le 2026-10-30. Prochaine étape : 4 (intégration continue, #7), à préciser.
- Phase 4, étape 4 (intégration continue, #7) : fusionnée dans `main`, validée par l'utilisateur le 2026-10-30
  (ticket #7 fermé). CI verte : Linux 1,1 min (565 tests : 223 Core, 84 Simulation, 218 UI, 21 Web, 19 client web),
  Windows 7,9 min (desktop compris). Prochaine étape : 5 (Docker et mise en ligne), à préciser (hébergeur, nom de
  domaine, déploiement automatique).
- Phase 4, étape 5 (Docker et mise en ligne) : fusionnée dans `main` le 2026-10-30 à la demande de l'utilisateur,
  pour pouvoir lancer le premier déploiement depuis l'onglet « Actions » (compte Fly.io, application « wh40ksim » et
  secret `FLY_API_TOKEN` faits par l'utilisateur). **Restent** : premier déploiement (lancé par l'utilisateur), site
  vérifié par lui, puis tag `v0.4` (avec son accord) qui clôt la phase 4.
  **Où on en est (2026-10-30, fin de session)** : flux « Déploiement » lancé à la main depuis `main` ; CI verte, mais
  l'étape `flyctl deploy` échoue sur le jeton. Premier essai : secret vide (`FLY_API_TOKEN:` sans `***` dans le
  journal). Deuxième essai (secret du 15:05:08) : « Authenticate: token validation error », sans doute un jeton abîmé
  par le `|` de PowerShell 5.1 vers `gh` (retour à la ligne ajouté). À faire par l'utilisateur (jamais par Claude, qui
  ne voit pas le jeton), dans PowerShell avec `flyctl` connecté :
  `gh secret set FLY_API_TOKEN --body ((fly tokens create deploy -a wh40ksim | Out-String).Trim())`
  (en Git Bash : `fly tokens create deploy -a wh40ksim | tr -d '\r\n' | gh secret set FLY_API_TOKEN`), vérifier la
  date avec `gh secret list`, puis « Re-run failed jobs » sur la dernière exécution (`gh run list --workflow
  deploy.yml`). Si le refus persiste : vérifier que `fly tokens create deploy -a wh40ksim` marche seul (application
  bien nommée « wh40ksim », compte connecté). Sur l'autre machine, `flyctl` n'est peut-être pas installé
  (`winget install flyctl` ou script d'installation de fly.io, puis `fly auth login`). `Dockerfile`, `.dockerignore`, `fly.toml` (application « wh40ksim », à ajuster
  au nom créé) ; `Wh40kSim.Web` : `/health`, `ForwardedHeaders:Enabled` (dernière adresse de `X-Forwarded-For`
  seulement) ; flux `deploy.yml` (tag `v*` ou à la main, CI réutilisée) et `docker.yml` (vérification de l'image :
  construction, démarrage, `/health`, page, client AOT) ; `ci.yml` réutilisable, branches seulement. Docker n'est pas
  installé sur cette machine : l'image est vérifiée par GitHub Actions. Premier déploiement manuel (`fly deploy
  --remote-only` par l'utilisateur) en échec sur la machine de construction de Fly (Depot), sans message, pendant la
  publication AOT (mémoire probablement) : l'image est désormais construite par GitHub Actions et envoyée à Fly
  (`flyctl deploy --local-only`). Un flux `workflow_dispatch` n'apparaît dans l'onglet « Actions » qu'une fois sur
  `main` : premier déploiement par GitHub après la fusion. `.github/workflows/ci.yml` (tâches Linux et
  Windows), `Wh40kSim.CI.slnf` (solution sans le desktop), badge dans le README, vérification de la CI ajoutée aux
  règles de travail et à `/valider-etape`. Corrigé grâce au premier passage de la CI : l'AOT dans le `.csproj`
  exigeait `wasm-tools` dès le build (elle est maintenant demandée dans la commande de publication, voir « Décisions
  techniques ») ; restauration faite en configuration Release (paquet de runtime du desktop manquant sinon) ; noms de
  fichier proposés à l'enregistrement nettoyés des caractères interdits par Windows sur tous les systèmes (Linux et le
  navigateur gardaient « : »).
  UI commune : `ISimulationService.MaxIterations` (aucun en desktop) et suivi de la file (`IProgress<int>`),
  `SimulationServiceException` affichée en message, `QueueDialog` / `QueueStatus`, `SimulationPanel.MaxIterations`
  (choix rapides au-delà masqués, saisie bornée, bulle « ? »). `Wh40kSim.Web.Client` : `Routes` (thème lu avant
  `AppShell`, barre de chargement retirée ensuite), `ApiSimulationService` (flux SSE, refus, pannes, annulation,
  plafond par `GET /api/simulations/limits`), `ServerSentEvents`, `BrowserScripts` (module `wh40ksim-web.js` :
  ouvrir, télécharger, mode sombre, barre de chargement), `BrowserScenarioFileService`, `BrowserPreferenceService`
  (`localStorage`, clé `wh40ksim.theme`) ; culture fr-FR (données de globalisation complètes). Serveur : page
  `Components/App.razor` (rendu WebAssembly sans prérendu, adresses des écrans de Wh40kSim.UI), `app.css` (barre de
  chargement), profil « http » qui ouvre le navigateur. En dehors de Development, le site demande une version publiée
  (fichiers statiques des projets référencés) : prévu à l'étape 5 (Docker). Test UI instable `PartialFileTests`
  (clic perdu après ouverture) corrigé avec `ClickUntil`. Retours de l'utilisateur après essai : profil de lancement
  sans débogage WebAssembly (Rider exigeait alors un navigateur Chromium ; l'utilisateur est sous Firefox) ; barre de
  progression figée vers 16 % en web (calcul de 1 M en ~0,2 s sur le serveur, page redessinée à chaque pourcent, plus
  lent en WebAssembly) : la barre devient un composant autonome (`SimulationProgressBar`, suivi de `ProgressState`) qui
  se redessine seule, la page n'est plus redessinée pendant le calcul (idée de l'utilisateur) ; barre à 100 % avant le
  traitement du résultat ; fond de la page aux couleurs du thème pendant le chargement (blanc auparavant ; sans thème
  mémorisé, selon le mode clair / sombre du système), puis celui de MudBlazor une fois la barre retirée ; affichage
  lent (~1 s par changement d'écran, même en Release) : client compilé AOT après un essai comparatif (branche
  `essai/aot`, reprise dans l'étape), voir « Décisions techniques » ; `wasm-tools` installé sur cette machine, **à
  installer sur l'autre**. 565 tests verts (223 Core, 84 Simulation, 218 UI, 21 Web, 19 client web ; UI stables sur
  3 exécutions). `Wh40kSim.Web` : `SimulationOptions` (section
  « Simulation »), `SimulationQueue` (file sans limite, positions), `ISimulationRunner` / `SimulatorRunner`,
  `SimulationEndpoints` (`POST /api/simulations`, SSE `queued` / `progress` / `result` / `error`, `ProblemDetails`),
  limiteur par IP (fenêtre fixe), `Wh40kSim.Web.http`, profil `http` sur le port 5080 ; `Wh40kSim.Web.Tests`
  (`Microsoft.AspNetCore.Mvc.Testing` 10.0.14). Vérifié aussi sur le vrai serveur (Kestrel, `curl`). Retours de
  l'utilisateur après essai : `SimulationJson` écrit sur une seule ligne (un seul champ `data:` en SSE au lieu de
  ~265), erreurs du scénario et nombre de simulations refusé signalés ensemble (« Demande refusée »). 534 tests verts
  (223 Core, 84 Simulation, 212 UI, 15 Web ; UI et Web stables sur 3 exécutions). `SimulationData` (+ `GroupData`,
  `PrecisionWeaponData`, `PoolData`, `ConversionCounts`, membres `required`) et `SimulationData.Validate()` ;
  l'accumulateur produit les données (`ToData`), `SimulationResult(SimulationData)` calcule tout, `SimulationResult.Data` ;
  `SimulationJson` (réglages de `ScenarioJson`). Rien de visible à l'écran : API de `SimulationResult` inchangée.
  `NonRegressionTests` inchangés ; temps inchangé (10 M simulations : 1,27 à 1,37 s). 518 tests verts (223 Core,
  83 Simulation, 212 UI).

Points notés pour la suite :
- Raccourcis clavier : Ctrl+S / Ctrl+O sont interceptés par `wh40ksim.js` (action du navigateur empêchée) ; à revérifier
  dans un vrai navigateur en phase 4.
