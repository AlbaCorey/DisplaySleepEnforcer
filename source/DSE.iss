; ---------------------------------------------------------------------------
;  Display Sleep Enforcer (DSE) - Inno Setup installer script
;
;  Build with:  ISCC.exe DSE.iss
;  Output:      ..\compiled\DSE-Setup.exe
;
;  Supports both per-user (default, no admin/UAC) and per-machine (admin)
;  installs. The user picks the mode on the first wizard page.
; ---------------------------------------------------------------------------

#define AppName      "Display Sleep Enforcer"
#define AppVersion   "0.1.0"
#define AppPublisher "DSE Project"
#define AppExeName   "DisplaySleepEnforcer.exe"

; Optional Authenticode signing. build.ps1 defines DSE_SIGN together with the
; "dse_sign" Sign Tool command (ISCC /Sdse_sign=...) when a signing certificate
; is configured. When enabled, the bundled application ([Files] signonce),
; Setup.exe and the uninstaller are all signed in this single ISCC run.
#ifdef DSE_SIGN
  #define FileSignFlag " signonce"
#else
  #define FileSignFlag ""
#endif

[Setup]
; Keep this AppId stable across versions so upgrades and uninstall work.
AppId={{47CA84A3-0810-435D-80D6-139E034AD2E2}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion=0.1.0.0
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Default to a per-user install (no UAC). The user can still select a
; per-machine install in the wizard, which triggers elevation.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\compiled
OutputBaseFilename=DSE-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=DSE.ico
#ifdef DSE_SIGN
SignTool=dse_sign
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "Start {#AppName} automatically when I sign in"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "..\compiled\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion{#FileSignFlag}

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: startup; Check: not IsAdminInstallMode
Name: "{commonstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: startup; Check: IsAdminInstallMode

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
