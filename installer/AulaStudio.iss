; Inno Setup script for AULA Studio. Built by installer/build.ps1, which passes SourceDir and OutputDir.
#define AppName "AULA Studio"
#define AppVersion GetVersionNumbersString(SourceDir + "\AulaStudio.exe")

[Setup]
AppId={{3074097C-6FB5-4C19-83B0-104D0FB1921D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=AULA Studio
DefaultDirName={autopf}\AULA Studio
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=AulaStudio-Setup-{#AppVersion}
SetupIconFile={#SourcePath}\..\src\AulaStudio\Assets\app.ico
UninstallDisplayIcon={app}\AulaStudio.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
pt.StartWithWindows=Abrir o AULA Studio ao iniciar o Windows (na bandeja)
en.StartWithWindows=Open AULA Studio when Windows starts (in the tray)

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "{cm:StartWithWindows}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\AulaStudio.exe"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\AulaStudio.exe"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\AulaStudio.exe"; Parameters: "--tray"; Tasks: startup

[Run]
Filename: "{app}\AulaStudio.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
