; Qwen Code Manager — Inno Setup script
; Build: build-installer.ps1 (passes /DAppVersion, /DPublishDir, /DOutputDir, /DOutputBase)
; Single installer technology for the unpackaged WinUI 3 app.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif
#ifndef OutputBase
  #define OutputBase "QwenCodeManager-Setup-x64"
#endif

#define AppName "Qwen Code Manager"
#define AppPublisher "Qwen Code Manager"
#define AppExeName "QwenCodeManager.exe"
; Stable identity — never change between versions (upgrade depends on it).
#define AppId "QwenCodeManager"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}.0
VersionInfoProductVersion={#AppVersion}.0
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBase}
SetupIconFile=..\src\QwenCodeManager.App\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
; User data lives in %LOCALAPPDATA%\QwenCodeManager and is never touched by the installer.

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Entire self-contained publish output (.NET runtime + Windows App SDK included).
; No Qwen Code, no user data, no secrets — application files only.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only application files are removed. %LOCALAPPDATA%\QwenCodeManager (projects,
; settings, covers, checkpoints, logs) is intentionally preserved.
