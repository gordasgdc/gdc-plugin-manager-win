; Instalator Windows pentru GDC Plugin Manager, cu Inno Setup
; (https://jrsoftware.org/isinfo.php — gratuit, standard pentru aplicatii
; mici Windows). Echivalentul .pkg-ului de pe Mac: instaleaza in Program
; Files, creeaza scurtatura in Start Menu, apare corect in "Apps & Features"
; cu dezinstalare curata.
;
; Cum se compileaza MANUAL (CI-ul din .github/workflows/build-windows.yml
; face toti pasii astia automat, inclusiv obfuscarea — asta e doar pentru
; un build local, pe Windows; o data ai nevoie de Inno Setup Compiler
; instalat — gratuit, https://jrsoftware.org/isdl.php):
;   1. dotnet publish src\GDCPluginManager.Client -c Release -r win-x64 --self-contained -o publish
;      (FARA -p:PublishSingleFile=true — vezi obfuscarea din CI, care are
;      nevoie de GDCPluginManager.Core.dll ca fisier separat, nu impachetat
;      in exe. Fara pasul de obfuscare, single-file merge la fel de bine.)
;   2. Deschide acest fisier (installer.iss) cu Inno Setup Compiler
;   3. Apasa "Compile" (sau F9)
;   4. Rezultatul apare in Output\GDCPluginManagerSetup.exe

#define MyAppName "GDC Plugin Manager"
#define MyAppVersion "1.25.1"
#define MyAppPublisher "Cristi Gordas"
#define MyAppExeName "GDCPluginManager.exe"
#define MyAppURL "https://gordas.dev"

[Setup]
AppId={{B6E1B3F0-2F0F-4B0E-9C1A-GDCPLUGINMGR1}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\GDC Plugin Manager
DefaultGroupName=GDC Plugin Manager
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=GDCPluginManagerSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Nu semnat cu certificat platit (acelasi caz ca .pkg-ul de pe Mac,
; nesemnat) — Windows SmartScreen va arata un avertisment "Unrecognized
; app" la prima rulare a instalatorului. Normal pentru distributie indie,
; se trece cu "More info" -> "Run anyway".
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
; "Romanian.isl" e un add-on separat, neinclus in instalarea de baza a
; Inno Setup (compiler:Languages\Romanian.isl nu exista implicit) - textele
; instalatorului raman doar in engleza. Aplicatia in sine ramane RO/EN/ES,
; asta afecteaza doar butoanele/mesajele din fereastra instalatorului.
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Tot ce e in publish\ (exe, DLL-urile .NET, PythonRuntime) — recursiv.
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Dezinstaleaza {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
