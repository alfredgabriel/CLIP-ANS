#define MyAppName "CLIP-ANS"
#define MyAppVersion "1.2.1"
#define MyAppPublisher "CLIP-ANS"
#define MyAppExeName "CLIP-ANS.exe"

[Setup]
AppId={{5DF0C955-46D9-4B52-87C9-09A96E25D11B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=CLIP-ANS-Setup
OutputDir=.
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\CLIP-ANS\Resources\app_icon.ico
UninstallDisplayIcon={app}\app_icon.ico
PrivilegesRequired=lowest
CloseApplications=force
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Ejecutar al iniciar Windows"; GroupDescription: "Opciones de inicio:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\CLIP-ANS\Resources\app_icon.ico"; DestDir: "{app}"; DestName: "app_icon.ico"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app_icon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app_icon.ico"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app_icon.ico"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
