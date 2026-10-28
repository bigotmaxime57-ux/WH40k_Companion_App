---
name: debut-journee
description: Lancement d'une session de travail sur Wh40kSim. Récupère le dépôt central (git fetch, main à jour, branches des deux machines), relit CLAUDE.md, vérifie l'environnement par un build et des tests complets (et aide à le corriger), puis fait le point sur l'existant, l'endroit où on s'était arrêté, les prochaines étapes et les questions en attente. À utiliser quand l'utilisateur commence sa journée (« on reprend », « bonjour, où en est-on ? »…) ou tape /debut-journee.
---

# Début de journée

But : partir d'un dépôt à jour et donner à l'utilisateur une vue claire du projet en une lecture. Ce skill **ne
code rien** et ne commence aucune étape : il se termine par une proposition, l'utilisateur décide.

Opérations permises sans demander : `git fetch`, `git pull --ff-only` (seulement si rien ne peut être perdu),
`git switch` vers une branche sans modification locale en cours, lectures (`git log`, `gh issue list`…).
Toujours demander avant : fusion, rebase, `stash`, suppression de branche, tout ce qui touche à des modifications
non commitées. En cas de doute ou de divergence, **s'arrêter et expliquer**.

## 1. Synchroniser avec le dépôt central

L'utilisateur alterne entre deux PC : du travail a pu être poussé depuis l'autre machine.

```
git fetch --prune
git status --short --branch
git branch -vv
git branch -r
```

- **Modifications locales** : les signaler (les fichiers non suivis de `scenarios/` sont des scénarios personnels,
  normaux, à ignorer). Ne jamais faire `git add -A`.
- **`main`** : comparer avec `origin/main` (`git rev-list --left-right --count main...origin/main`).
  En retard seulement → mettre à jour (`git pull --ff-only` si on est sur `main`, sinon
  `git fetch origin main:main`). En avance (commits non poussés) ou divergente → le signaler, ne rien faire.
- **Branche courante** (si ce n'est pas `main`) : même comparaison avec sa branche distante. En retard seulement →
  `git pull --ff-only`. Signaler si `main` a avancé depuis sa création (`git log --oneline <branche>..main`) : c'est
  une information, pas une action.
- **Branches de l'autre machine** : une branche distante sans copie locale (`origin/phase…`) est sans doute une étape
  commencée sur l'autre PC ; la mentionner avec son dernier commit (`git log -1 --format="%h %s (%cr)" origin/<b>`).
- **Branche de l'étape en cours** : « Avancement » cite souvent la branche de travail (« à coder sur la branche … »).
  Vérifier qu'elle existe (locale, distante, les deux, aucune) et si elle contient déjà des commits par rapport à
  `main`. Si on est sur une autre branche et que rien n'est en cours, proposer d'y basculer.

## 2. Relire CLAUDE.md

Le CLAUDE.md chargé en début de session est la version **d'avant** la synchronisation : s'il a changé avec le pull
(`git diff --stat HEAD@{1} -- CLAUDE.md`, ou lecture directe), **le relire en entier** avec l'outil Read. Dire en
une ligne ce qui a changé, s'il a changé.

Relire aussi la mémoire propre à cette machine (`MEMORY.md` et les fichiers qu'il cite) : elle dit ce qui manque
ici (SDK, charge de travail MAUI, `gh`…).

## 3. Vérifier l'environnement : build et tests complets

But (demande de l'utilisateur) : après un changement de machine ou de configuration, tout remettre d'aplomb
**avant** de commencer la journée. Build de **toute la solution**, Wh40kSim.Desktop compris.

```
dotnet --version
dotnet workload list
dotnet build -nologo -v q
dotnet test --no-build -nologo -v q
```

- Lire le **code de retour** de chaque commande (`$LASTEXITCODE` en PowerShell, une commande à la fois en Bash),
  jamais seulement la fin de la sortie filtrée. Attendu : 0 erreur, 0 avertissement, tous les tests verts ; noter
  le nombre de tests par projet et le comparer au dernier chiffre d'« Avancement ».
- Comparer SDK et charges de travail avec ce que demandent CLAUDE.md (« Décisions techniques », « Environnement de
  développement ») et la mémoire de la machine (qui dit quel .NET utilise Rider : il peut y en avoir deux).

En cas d'échec, **diagnostiquer et proposer la correction**, sans contourner (pas de build partiel présenté comme
vert). Cas connus :

| Symptôme | Cause probable | Correction |
|---|---|---|
| NETSDK1147, charge de travail manquante | MAUI absent pour ce .NET | `dotnet workload install maui-windows` (terminal administrateur pour `C:\Program Files\dotnet`), puis `dotnet restore` |
| NETSDK1047 (pas de cible …/win-x64) | restauration faite avant l'installation de MAUI | `dotnet restore` (avec le .NET qu'utilise Rider aussi) |
| NETSDK1045, SDK trop ancien | SDK 10 absent du .NET utilisé | installer le SDK 10 (voir mémoire de la machine) |
| MSB3021 / MSB3027, fichier verrouillé | application Wh40kSim.Desktop ouverte | demander de la fermer, ou vérifier avec `dotnet build -c Release` |
| NU1102 / paquet introuvable | version de paquet absente du cache, ou hors ligne | `dotnet restore`, vérifier la connexion |
| chemin trop long (> 260) | `LongPathsEnabled = 0` et dépôt trop profond | voir « Environnement de développement » |
| test UI isolé en échec | instabilité bUnit connue | relancer une fois ; s'il échoue encore, c'est un vrai échec |

- Installations (SDK, charge de travail, `gh`) et commandes en administrateur : **proposer**, pas lancer sans
  accord ; si une commande demande un terminal administrateur, la donner à l'utilisateur.
- Une correction de configuration propre à la machine (nouvelle installation, chemin, réglage) est notée dans la
  mémoire de la machine, pas dans CLAUDE.md. Un point qui vaut pour les deux machines va dans CLAUDE.md.
- Après correction, relancer build et tests jusqu'au vert, ou laisser le point en suspens dans le compte rendu si
  l'utilisateur préfère s'en occuper plus tard.

## 4. Rassembler l'état

- **Avancement** (CLAUDE.md) : dernière étape fusionnée, étape en cours et son statut (précisée / à coder / en
  cours / en attente de validation), branches non fusionnées, « Points notés pour la suite ».
- **Historique récent** : `git log --oneline -10 main` ; sur une branche d'étape, ses commits propres
  (`git log --oneline main..HEAD`) et un `git diff --stat main...HEAD` pour voir ce qui est déjà fait.
- **Tickets** : `gh issue list --state open --limit 50` (si `gh` est absent du PATH :
  `C:\Program Files\GitHub CLI\gh.exe` ; s'il n'est pas installé ou pas authentifié sur cette machine, le dire et
  continuer sans). Repérer les tickets cités par l'étape en cours ou la suivante.
- **Points propres à cette machine** : croiser « Points notés pour la suite » (ex. charge de travail MAUI, `gh` « à
  faire sur l'autre machine ») avec la mémoire de la machine, pour dire ce qui concerne **ce** PC.

## 5. Questions en attente

Chercher ce qui attend une décision de l'utilisateur :
- dans la roadmap : « à trancher », « à arrêter avec l'utilisateur », « points à trancher », « définitions à
  arrêter » (dans l'étape en cours, la suivante, et « À trancher en début de phase » de la phase en cours) ;
- une étape « en attente de validation » : c'est la première question à poser ;
- les tickets liés à l'étape en cours ou à la suivante (leurs « points à trancher ») ;
- une incohérence repérée entre CLAUDE.md et le code ou les branches (ex. étape notée « à coder » mais commits déjà
  présents) : la signaler.

Ne poser une question que si elle bloque ou oriente le travail du jour ; les questions de phases lointaines vont dans
une ligne « plus tard ».

## 6. Compte rendu

En français, dans le terminal, court et structuré (une lecture d'une minute). Pas de recopie de CLAUDE.md : résumer.

```
## Synchronisation
main à jour (3 commits récupérés : …) · branche courante : … · modifications locales : aucune

## Environnement
Build : 0 erreur, 0 avertissement · tests : 340 verts (172 Core, 48 Simulation, 120 UI), comme à la dernière étape

## Où on en est
Phase 3 : graphiques et comparaison — étape 5/9 (dégâts bruts)
- Fait récemment : étape 4 (points) fusionnée, 340 tests verts
- En cours : étape 5 précisée et validée, à coder sur `phase3/etape5-degats-bruts` (aucun commit pour l'instant)

## Prochaines étapes
1. Étape 5 — dégâts bruts (#6) : <contenu en 2-3 lignes>
2. Étape 6 — statistiques détaillées : <contenu en 2-3 lignes>
3. Étape 7 — Full sim (UI) : <une ligne>

## Questions en attente
- <question bloquante ou utile aujourd'hui, avec l'étape concernée>
- Plus tard : <questions des étapes suivantes, en une ligne>

## Sur cette machine
- <ce qui a été corrigé ou reste à corriger ici, ex. `dotnet restore` relancé après NETSDK1047>

## Tickets ouverts
#1 …, #5 …, #9 … (seulement les titres, mettre en avant ceux qui touchent l'étape en cours)
```

Terminer par **une** proposition concrète pour la journée (ex. « Commencer l'étape 5 ? »), sans la démarrer.
