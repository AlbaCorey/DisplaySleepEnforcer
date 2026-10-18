; Display Sleep Enforcer - force display sleep, including while locked.
; Copyright (C) 2026 AlbaCorey
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
#define AppVersion   "1.0.0"
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
; Do not silently reuse the mode of a previous install. Without this, Setup sees an
; existing all-users (admin-mode) install in HKLM and elevates immediately at launch,
; so the user never gets to choose "just me" without a UAC prompt.
UsePreviousPrivileges=no
OutputDir=..\compiled
OutputBaseFilename=DSE-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dynamic
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
; Seed sign-in startup with a per-user HKCU Run value. On its first launch the
; app migrates this to a robust per-user Task Scheduler logon task (with
; restart-on-failure) and removes the Run value. This keeps a working startup
; path in the window between install and first launch.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DisplaySleepEnforcer"; ValueData: """{app}\{#AppExeName}"""; Tasks: startup; Flags: uninsdeletevalue
; Initial appearance (0 = follow system, 1 = light, 2 = dark). Written once at
; install; the app manages it afterwards. Not removed on uninstall.
Root: HKCU; Subkey: "Software\DisplaySleepEnforcer\Settings"; ValueType: dword; ValueName: "Theme"; ValueData: "0"; Check: ThemeIsSystem
Root: HKCU; Subkey: "Software\DisplaySleepEnforcer\Settings"; ValueType: dword; ValueName: "Theme"; ValueData: "1"; Check: ThemeIsLight
Root: HKCU; Subkey: "Software\DisplaySleepEnforcer\Settings"; ValueType: dword; ValueName: "Theme"; ValueData: "2"; Check: ThemeIsDark

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove the per-user sign-in task the app creates (migrated from the Run seed).
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""DisplaySleepEnforcer"" /F"; Flags: runhidden; RunOnceId: "DeleteDSEStartupTask"

[Code]
var
  ThemePage: TInputOptionWizardPage;

procedure InitializeWizard();
begin
  ThemePage := CreateInputOptionPage(wpSelectTasks,
    'Appearance',
    'Choose how {#AppName} looks.',
    'Select a theme. You can change this later from the tray icon menu.',
    True, False);
  ThemePage.Add('Follow the Windows system theme');
  ThemePage.Add('Light');
  ThemePage.Add('Dark');
  ThemePage.Values[0] := True;
end;

function ThemeIsSystem(): Boolean;
begin
  Result := (ThemePage = nil) or ThemePage.Values[0];
end;

function ThemeIsLight(): Boolean;
begin
  Result := (ThemePage <> nil) and ThemePage.Values[1];
end;

function ThemeIsDark(): Boolean;
begin
  Result := (ThemePage <> nil) and ThemePage.Values[2];
end;
