#define MyAppName "Finalpass"
#define MyAppVersion "0.1.4"
#define MyAppPublisher "Ali Symeri"
#define MyAppExeName "Finalpass.exe"

[Setup]
AppId={{F10A5E08-5F93-44E9-AF43-DF54E973395D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoTextVersion={#MyAppVersion}
VersionInfoProductTextVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\Finalpass
DefaultGroupName=Finalpass
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.22000
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=Finalpass-{#MyAppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Finalpass.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Assets\AppIcon.ico
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Finalpass"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{autodesktop}\Finalpass"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\.fpass"; ValueType: string; ValueName: ""; ValueData: "Finalpass.Vault"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\Finalpass.Vault"; ValueType: string; ValueName: ""; ValueData: "Finalpass encrypted vault"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Finalpass.Vault\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Assets\AppIcon.ico,0"
Root: HKCU; Subkey: "Software\Classes\Finalpass.Vault\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Finalpass"; Flags: nowait postinstall skipifsilent
