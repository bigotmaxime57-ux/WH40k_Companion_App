---
name: voir-sc
description: Regarder la capture d'écran que l'utilisateur a mise dans le presse-papiers Windows (Wh40kSim). L'enregistrer dans le scratchpad, la lire comme une image, puis répondre à sa remarque. À utiliser dès que l'utilisateur écrit « voir sc », « regarde la capture », « cf. sc » ou tape /voir-sc.
---

# Voir la capture d'écran (« voir sc »)

L'utilisateur fait une capture (Win+Maj+S, Impr. écran…) qui reste dans le presse-papiers, puis écrit « voir sc » avec
sa remarque. Il faut l'enregistrer en fichier pour pouvoir la lire.

## 1. Enregistrer le presse-papiers

PowerShell (le presse-papiers Windows n'est pas accessible depuis Bash) :

```powershell
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$img = [System.Windows.Forms.Clipboard]::GetImage()
if ($img) {
    $p = "<scratchpad>\scN.png"
    $img.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
    "$p $($img.Width)x$($img.Height)"
} else { "NO_IMAGE" }
```

- `<scratchpad>` : le dossier scratchpad de la session (jamais le dépôt : une capture n'est pas versionnée).
- **Numéroter** les fichiers (`sc1.png`, `sc2.png`…) au fil de la session : ne pas écraser une capture précédente,
  l'utilisateur peut y faire référence.
- `NO_IMAGE` : le presse-papiers contient du texte ou rien. Le dire à l'utilisateur (la capture a peut-être été
  remplacée par un copier-coller) et lui demander de la refaire ; ne pas deviner ce qu'elle montrait.

## 2. Lire l'image

Lire le fichier avec l'outil Read. Une capture très large ou très haute est réduite à l'affichage : si un détail est
illisible (petit texte, graduations), recadrer la zone utile avec `System.Drawing` (`Bitmap.Clone(Rectangle, …)`) dans
un nouveau fichier du scratchpad, puis le lire.

## 3. Interpréter avant de répondre

- **Version affichée** : la capture vient de l'application telle que l'utilisateur l'a lancée. Si elle montre un
  élément déjà modifié ou retiré dans le code (ex. un texte supprimé au commit précédent), c'est sans doute une ancienne
  version encore ouverte : le signaler et proposer de relancer l'application, plutôt que de « corriger » à nouveau.
- **Ce qui est montré** : identifier précisément l'élément visé (légende, couleurs, valeurs) avant de conclure ; en
  cas de confusion possible (ex. un pointillé qui est la moyenne et non une limite), l'expliquer à l'utilisateur.
- **Données** : une capture peut contenir des informations personnelles (autres fenêtres, notifications). N'en parler
  que pour ce qui concerne la remarque, et ne jamais publier ni versionner la capture.

## 4. Répondre

En français, en partant de la remarque de l'utilisateur. Si elle demande une modification, suivre les règles
habituelles du projet (CLAUDE.md : étape en cours, build et tests, commit) ; la capture ne remplace pas une
vérification à l'écran après la modification.
