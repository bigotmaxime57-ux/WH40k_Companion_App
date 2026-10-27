---
name: valider-etape
description: Clôture d'une étape validée par l'utilisateur dans Wh40kSim. Fusion --no-ff de la branche de l'étape dans main, build et tests, push, suppression de la branche, fermeture des tickets GitHub traités. À utiliser dès que l'utilisateur valide une étape (« validé », « ok pour l'étape », « fusionne »…) ou tape /valider-etape.
---

# Valider une étape

L'utilisateur a donné une autorisation permanente (2026-10-27) : **quand il valide une étape**, Claude enchaîne sans
redemander la fusion dans `main`, le push, la suppression de la branche et la fermeture des tickets traités.
Cette autorisation ne couvre que ces opérations. Il faut toujours demander pour :
- poser ou pousser un **tag** ;
- un `push --force` ou toute réécriture d'historique ;
- fusionner une branche que l'utilisateur n'a **pas** explicitement validée (une validation précédente ne vaut pas
  pour l'étape suivante).

Si une étape échoue (conflit, build ou test rouge), **s'arrêter** et l'expliquer à l'utilisateur, sans contourner.

## 1. Vérifier l'état de départ

- Branche courante = branche de l'étape (ex. `phase3/etape2-accueil-navigation`), pas `main`.
- `git status` : rien d'important non commité. Ne jamais faire `git add -A` : `scenarios/` contient des scénarios
  personnels non versionnés.
- `git fetch`, puis vérifier que `main` local est à jour avec `origin/main`.

## 2. Mettre à jour « Avancement » (sur la branche, avant la fusion)

Dans `CLAUDE.md`, section « Avancement », passer l'étape de « branche …, en attente de validation » à
« fusionnée dans `main` », avec le nombre de tests et, si la roadmap le prévoit pour cette étape, la mention du tag
(à poser seulement après accord, voir plus haut). Indiquer la prochaine étape. Commit dédié sur la branche :
`Avancement : <phase>, étape <n> validée`, terminé par la ligne `Co-Authored-By` de la session.

## 3. Fusionner

```
git switch main
git merge --no-ff <branche> -m "Merge de la branche <branche>

Co-Authored-By: <ligne de la session>"
```

Toujours `--no-ff` (l'utilisateur suit les branches dans SourceTree).

## 4. Build et tests sur main

```
dotnet build -nologo -v q
dotnet test --no-build -nologo -v q
```

Lire le **code de retour** (`$LASTEXITCODE` en PowerShell), pas seulement la fin de la sortie : 0 erreur et
0 avertissement exigés. Si le build échoue parce que des DLL sont verrouillées, l'application est sans doute ouverte :
demander à l'utilisateur de la fermer, puis relancer. Ne pas pousser tant que build et tests ne sont pas verts.

## 5. Pousser et nettoyer

```
git push origin main
git branch -d <branche>
```

Si la branche avait été poussée sur GitHub : `git push origin --delete <branche>`.

## 6. Tickets GitHub

CLI : `gh` (si absent du PATH : `C:\Program Files\GitHub CLI\gh.exe`).

- Les commits contenant `Closes #n` ferment le ticket au push sur `main` : vérifier avec
  `gh issue view <n> --json state` (la fermeture peut prendre quelques secondes).
- Un ticket traité par l'étape mais non référencé par `Closes` : le fermer avec un commentaire qui cite le commit de
  fusion, `gh issue close <n> --comment "Réalisé à la <phase>, étape <n> (<hash court>)."`
- Un ticket **partiellement** traité (ex. version légère) : ne pas le fermer ; ajouter un commentaire qui dit ce qui
  a été fait et ce qui reste (`gh issue comment`).

## 7. Compte rendu

Dire en quelques lignes : fusion faite (hash), tests (nombre), push fait, branche supprimée, tickets fermés ou
commentés. Puis proposer la suite (prochaine étape de la roadmap), sans la commencer sans feu vert.
