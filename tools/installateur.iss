; Installateur Windows de Wh40kSim (Inno Setup 6), compilé par tools\publier.ps1, qui fournit les définitions :
;   AppVersion   version affichée (InformationalVersion du projet)
;   PublishDir   dossier de l'application publiée (profil win-x64)
;   ScenariosDir dossier des scénarios d'exemple à copier
;   OutputDir    dossier où écrire Wh40kSim-<version>-setup.exe
;
; Installation pour l'utilisateur seul (pas de droits administrateur) dans %LOCALAPPDATA%\Programs\Wh40kSim ;
; raccourcis dans le menu Démarrer (et sur le bureau si coché) ; scénarios d'exemple dans Documents\Wh40kSim\scenarios,
; jamais écrasés ni supprimés (ce sont les fichiers de l'utilisateur).

#ifndef AppVersion
  #error Lancer ce script avec tools\publier.ps1
#endif

[Setup]
; Identifiant fixe : une nouvelle version remplace l'ancienne au lieu de s'installer à côté
AppId={{576BF633-3187-4D2A-81EC-25BD3CF50ED2}
AppName=Wh40kSim
AppVersion={#AppVersion}
AppVerName=Wh40kSim {#AppVersion}
AppComments=Simulateur de dégâts pour Warhammer 40 000 (11e édition)
AppSupportURL=https://github.com/bigotmaxime57-ux/WH40k_Companion_App/issues
DefaultDirName={autopf}\Wh40kSim
DefaultGroupName=Wh40kSim
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
SetupIconFile={#PublishDir}\appicon.ico
UninstallDisplayIcon={app}\Wh40kSim.Desktop.exe
UninstallDisplayName=Wh40kSim
OutputDir={#OutputDir}
OutputBaseFilename=Wh40kSim-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Une version de test lancée pendant une mise à jour : proposée à la fermeture
CloseApplications=yes

[Languages]
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Les fichiers d'une version précédente ne doivent pas se mélanger à ceux de la nouvelle
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LISEZMOI.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ScenariosDir}\*"; DestDir: "{userdocs}\Wh40kSim\scenarios"; Flags: onlyifdoesntexist uninsneveruninstall recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\Wh40kSim"; Filename: "{app}\Wh40kSim.Desktop.exe"
Name: "{group}\Lisez-moi"; Filename: "{app}\LISEZMOI.txt"
Name: "{group}\Scénarios"; Filename: "{userdocs}\Wh40kSim\scenarios"
Name: "{group}\{cm:UninstallProgram,Wh40kSim}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Wh40kSim"; Filename: "{app}\Wh40kSim.Desktop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Wh40kSim.Desktop.exe"; Description: "{cm:LaunchProgram,Wh40kSim}"; Flags: nowait postinstall skipifsilent
