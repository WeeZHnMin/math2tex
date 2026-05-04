; Math2Tex Inno Setup script
; Build with:  iscc setup.iss
; Or via:      .\build-installer.ps1   (publishes + compiles)

#define AppName        "Math2Tex"
#define AppVersion     "1.0.0"
#define AppPublisher   "WeeZHnMin"
#define AppURL         "https://github.com/WeeZHnMin/math2tex"
#define AppExeName     "Math2Tex.exe"
#define AppId          "{{F2A91E2C-7B0D-4B2E-9D8C-1234567890AB}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases

; Per-user install (no admin required) — installs into %LocalAppData%
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=no

; Output
OutputDir=installer
OutputBaseFilename=Math2Tex-Setup-{#AppVersion}
SetupIconFile=app.ico
UninstallDisplayIcon={app}\{#AppExeName}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; UI
WizardStyle=modern
ShowLanguageDialog=no
DisableWelcomePage=no

[Languages]
Name: "chs"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式:"
Name: "autostart";   Description: "开机自动启动";       GroupDescription: "启动选项:"; Flags: unchecked

[Files]
; Take everything from publish/ output of `dotnet publish`
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Optional autostart, only if the user ticked the task during install
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "立即运行 {#AppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Optionally clean user data; commented by default to preserve settings/history
; Type: filesandordirs; Name: "{userappdata}\{#AppName}"
