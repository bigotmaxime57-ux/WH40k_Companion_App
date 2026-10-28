<#
    Produit le zip à distribuer pour les tests : dist\Wh40kSim-<version>-win-x64.zip
    (application autonome publiée avec le profil win-x64, LISEZMOI.txt, dossier scenarios).

    Utilisation, depuis la racine du dépôt :
        powershell -ExecutionPolicy Bypass -File tools\publier.ps1                    # scénarios versionnés seulement
        powershell -ExecutionPolicy Bypass -File tools\publier.ps1 -LocalScenarios    # tout le dossier scenarios local

    La version vient de <InformationalVersion> dans src\Wh40kSim.Desktop\Wh40kSim.Desktop.csproj.
#>
param(
    [switch] $LocalScenarios
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\Wh40kSim.Desktop'

$csproj = [IO.File]::ReadAllText((Join-Path $project 'Wh40kSim.Desktop.csproj'))
if ($csproj -notmatch '<InformationalVersion>([^<]+)</InformationalVersion>') { throw 'InformationalVersion introuvable dans le projet.' }
$version = $Matches[1]
$name = "Wh40kSim-$version-win-x64"

Write-Host "Publication de la version $version..."
dotnet publish $project -p:PublishProfile=win-x64 -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Échec de dotnet publish (code $LASTEXITCODE)." }

$dist = Join-Path $root 'dist'
$staging = Join-Path $dist $name
$zip = Join-Path $dist "$name.zip"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force -Confirm:$false }
if (Test-Path $zip) { Remove-Item $zip -Force -Confirm:$false }
New-Item -ItemType Directory -Force $staging | Out-Null

Copy-Item (Join-Path $project 'bin\Publish\win-x64\*') $staging -Recurse
Copy-Item (Join-Path $PSScriptRoot 'LISEZMOI.txt') $staging

# Scénarios : ceux du dépôt, ou tout le dossier local (scénarios personnels non versionnés compris)
$scenarios = Join-Path $staging 'scenarios'
New-Item -ItemType Directory -Force $scenarios | Out-Null
if ($LocalScenarios) {
    Copy-Item (Join-Path $root 'scenarios\*') $scenarios -Recurse
} else {
    git -C $root ls-files scenarios | ForEach-Object {
        $target = Join-Path $staging $_
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        Copy-Item (Join-Path $root $_) $target
    }
}
Write-Host 'Scénarios inclus :'
Get-ChildItem $scenarios -Recurse -File | ForEach-Object { Write-Host "  $($_.FullName.Substring($scenarios.Length + 1))" }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($staging, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
$size = [math]::Round((Get-Item $zip).Length / 1MB)
Write-Host "Zip prêt : $zip ($size Mo)"
