; Display Sleep Enforcer - force display sleep, including while locked.
; Copyright (C) 2026 ferdef150
;
; This program is free software: you can redistribute it and/or modify it under
; the terms of the GNU General Public License as published by the Free Software
; Foundation, either version 3 of the License, or (at your option) any later
; version.
;
; This program is distributed in the hope that it will be useful, but WITHOUT
; ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
; FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
;
; You should have received a copy of the GNU General Public License along with
; this program. If not, see <https://www.gnu.org/licenses/>.
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
#define AppVersion   "0.3.0"
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
VersionInfoVersion={#AppVersion}.0
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
; Checked by default (opt-out): the user can untick this to skip sign-in startup.
Name: "startup"; Description: "Start {#AppName} automatically when I sign in"; GroupDescription: "Startup:"
; Opt-in: unchecked by default.
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\compiled\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion{#FileSignFlag}

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Sign-in startup is a per-user HKCU Run value (matches the app's own toggle).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DisplaySleepEnforcer"; ValueData: """{app}\{#AppExeName}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
