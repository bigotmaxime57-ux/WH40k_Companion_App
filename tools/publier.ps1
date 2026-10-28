<#
    Produit l'installateur à distribuer pour les tests : dist\Wh40kSim-<version>-setup.exe
    (application autonome publiée avec le profil win-x64, LISEZMOI.txt, scénarios d'exemple ; voir installateur.iss).

    Utilisation, depuis la racine du dépôt :
        powershell -ExecutionPolicy Bypass -File tools\publier.ps1                    # scénarios versionnés seulement
        powershell -ExecutionPolicy Bypass -File tools\publier.ps1 -LocalScenarios    # tout le dossier scenarios local

    La version vient de <InformationalVersion> dans src\Wh40kSim.Desktop\Wh40kSim.Desktop.csproj.
    Prérequis : Inno Setup 6 (winget install --id JRSoftware.InnoSetup -e --scope user).
#>
param(
    [switch] $LocalScenarios
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\Wh40kSim.Desktop'
$publish = Join-Path $project 'bin\Publish\win-x64'

$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 introuvable : winget install --id JRSoftware.InnoSetup -e --scope user' }

$csproj = [IO.File]::ReadAllText((Join-Path $project 'Wh40kSim.Desktop.csproj'))
if ($csproj -notmatch '<InformationalVersion>([^<]+)</InformationalVersion>') { throw 'InformationalVersion introuvable dans le projet.' }
$version = $Matches[1]

Write-Host "Publication de la version $version..."
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force -Confirm:$false }
dotnet publish $project -p:PublishProfile=win-x64 -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Échec de dotnet publish (code $LASTEXITCODE)." }

# Traductions de WinUI (fichiers .mui, une centaine de langues) : seuls le français et l'anglais sont gardés.
# Celles des bibliothèques .NET sont déjà limitées par SatelliteResourceLanguages dans le projet.
Get-ChildItem $publish -Directory | Where-Object {
    $_.Name -notmatch '^(fr|en)(-|$)' -and
    -not (Get-ChildItem $_.FullName -Recurse -File | Where-Object Extension -ne '.mui')
} | Remove-Item -Recurse -Force -Confirm:$false

# Scénarios : ceux du dépôt, ou tout le dossier local (scénarios personnels non versionnés compris)
$dist = Join-Path $root 'dist'
$scenarios = Join-Path $dist 'scenarios'
if (Test-Path $scenarios) { Remove-Item $scenarios -Recurse -Force -Confirm:$false }
New-Item -ItemType Directory -Force $scenarios | Out-Null
if ($LocalScenarios) {
    Copy-Item (Join-Path $root 'scenarios\*') $scenarios -Recurse
} else {
    git -C $root ls-files scenarios | ForEach-Object {
        $target = Join-Path $dist $_
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        Copy-Item (Join-Path $root $_) $target
    }
}
Write-Host 'Scénarios inclus :'
Get-ChildItem $scenarios -Recurse -File | ForEach-Object { Write-Host "  $($_.FullName.Substring($scenarios.Length + 1))" }

Write-Host "Création de l'installateur..."
& $iscc /Q "/DAppVersion=$version" "/DPublishDir=$publish" "/DScenariosDir=$scenarios" "/DOutputDir=$dist" (Join-Path $PSScriptRoot 'installateur.iss')
if ($LASTEXITCODE -ne 0) { throw "Échec d'Inno Setup (code $LASTEXITCODE)." }

$setup = Join-Path $dist "Wh40kSim-$version-setup.exe"
$size = [math]::Round((Get-Item $setup).Length / 1MB)
Write-Host "Installateur prêt : $setup ($size Mo)"
