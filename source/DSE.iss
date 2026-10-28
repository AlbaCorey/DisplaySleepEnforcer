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
#define AppVersion   "1.0.5"
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
; The lock-screen slideshow keeps requesting display-on and defeats enforcement,
; so switching to Picture is recommended. Pre-checked below only when Windows
; currently has the slideshow on.
Name: "picturelock"; Description: "Switch the lock screen background to Picture (recommended - a slideshow blocks display sleep)"; GroupDescription: "Lock screen:"; Flags: unchecked
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
; path in the window between install and first launch. --minimized makes that
; startup open in the notification area rather than popping a window up.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DisplaySleepEnforcer"; ValueData: """{app}\{#AppExeName}"" --minimized"; Tasks: startup; Flags: uninsdeletevalue
; When the user keeps sign-in startup on, seed "Enable enforcement immediately
; on start" according to their answer on the follow-up page. The app manages
; this value afterwards.
Root: HKCU; Subkey: "Software\DisplaySleepEnforcer\Settings"; ValueType: dword; ValueName: "AutoStartEnforcement"; ValueData: "1"; Check: ShouldAutoStartEnforcement
; Initial appearance (0 = follow system, 1 = light, 2 = dark). Written once at
; install; the app manages it afterwards. Not removed on uninstall.
Root: HKCU; Subkey: "Software\DisplaySleepEnforcer\Settings"; ValueType: dword; ValueName: "Theme"; ValueData: "0"; Check: ThemeIsSystem
Root: HKCU; Subkey: "Software\DisplaySleepEnforcer\Settings"; ValueType: dword; ValueName: "Theme"; ValueData: "1"; Check: ThemeIsLight
Root: HKCU; Subkey: "Software\DisplaySleepEnforcer\Settings"; ValueType: dword; ValueName: "Theme"; ValueData: "2"; Check: ThemeIsDark
; Switch the lock-screen background to Picture when requested. This is the
; same value the Settings app writes when you pick Picture there, so the
; stored image is kept and only the background type changes. Left in place
; on uninstall (Picture is the Windows default).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Lock Screen"; ValueType: dword; ValueName: "SlideshowEnabled"; ValueData: "0"; Tasks: picturelock

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove the per-user sign-in task the app creates (migrated from the Run seed).
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""DisplaySleepEnforcer"" /F"; Flags: runhidden; RunOnceId: "DeleteDSEStartupTask"

[Code]
var
  ThemePage: TInputOptionWizardPage;
  AutoStartPage: TInputOptionWizardPage;
  LockScreenTaskTouched: Boolean;

function LockScreenSlideshowEnabled(): Boolean;
var
  SlideshowEnabled: DWORD;
begin
  { 1 = Slideshow; 0 or missing = Picture/Spotlight. Same value the Settings
    app uses under Personalization > Lock screen. }
  Result := RegQueryDWordValue(HKCU,
    'Software\Microsoft\Windows\CurrentVersion\Lock Screen',
    'SlideshowEnabled', SlideshowEnabled) and (SlideshowEnabled = 1);
end;

procedure CurPageChanged(CurPageID: Integer);
var
  I: Integer;
begin
  { Pre-check the Picture task when the lock-screen slideshow is currently on,
    since that is exactly the setup that defeats enforcement. Only done once so
    navigating back to the page does not override the user's choice. }
  if (CurPageID = wpSelectTasks) and (not LockScreenTaskTouched) then
  begin
    LockScreenTaskTouched := True;
    if LockScreenSlideshowEnabled() then
    begin
      for I := 0 to WizardForm.TasksList.Items.Count - 1 do
      begin
        if Pos('Switch the lock screen background', WizardForm.TasksList.ItemCaption[I]) > 0 then
          WizardForm.TasksList.Checked[I] := True;
      end;
    end;
  end;
end;

procedure InitializeWizard();
begin
  { Follow-up to the "start with Windows" task: offer to also begin enforcing
    as soon as the app opens. Shown only when sign-in startup is selected. }
  AutoStartPage := CreateInputOptionPage(wpSelectTasks,
    'Startup enforcement',
    'Start enforcing as soon as {#AppName} opens?',
    '{#AppName} will start automatically when you sign in to Windows. If you ' +
    'also turn on "Enable enforcement immediately on start", it begins ' +
    'enforcing right away (including at sign-in) using the modes you select in ' +
    'the app. Otherwise it opens stopped and you press Turn on yourself.',
    True, False);
  AutoStartPage.Add('Also enable enforcement immediately on start');
  AutoStartPage.Add('No, open stopped (I will press Turn on myself)');
  AutoStartPage.Values[0] := True;

  ThemePage := CreateInputOptionPage(AutoStartPage.ID,
    'Appearance',
    'Choose how {#AppName} looks.',
    'Select a theme. You can change this later from the tray icon menu.',
    True, False);
  ThemePage.Add('Follow the Windows system theme');
  ThemePage.Add('Light');
  ThemePage.Add('Dark');
  ThemePage.Values[0] := True;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  { The enforcement-on-start question only makes sense when sign-in startup is
    on, so skip it when the user unticked that task. }
  Result := (PageID = AutoStartPage.ID) and (not IsTaskSelected('startup'));
end;

function ShouldAutoStartEnforcement(): Boolean;
begin
  Result := IsTaskSelected('startup') and
            (AutoStartPage <> nil) and AutoStartPage.Values[0];
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
