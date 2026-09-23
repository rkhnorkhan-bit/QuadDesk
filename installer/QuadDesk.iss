#define MyAppName "QuadDesk"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\QuadDesk"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\release"
#endif

[Setup]
AppId={{AABAF36D-1C11-4D3F-A6C9-84AE93FC5F6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=QuadDesk contributors
DefaultDirName={localappdata}\Programs\QuadDesk
DefaultGroupName=QuadDesk
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=QuadDesk-v{#MyAppVersion}-win-x64-setup
SetupIconFile=..\src\QuadDesk\Assets\QuadDesk.ico
UninstallDisplayIcon={app}\QuadDesk.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductName=QuadDesk
VersionInfoDescription=QuadDesk installer
VersionInfoCompany=QuadDesk contributors

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\QuadDesk.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\config.default.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\layouts\*"; DestDir: "{app}\layouts"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\QuadDesk"; Filename: "{app}\QuadDesk.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\QuadDesk"; Filename: "{app}\QuadDesk.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\QuadDesk.exe"; Description: "Launch QuadDesk"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\*.backup-*"
Type: files; Name: "{app}\QuadDesk.update-*.exe"
