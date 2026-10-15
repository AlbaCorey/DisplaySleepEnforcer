# Display Sleep Enforcer

**Status:** Pre-release (v0.4.0)

Display Sleep Enforcer is a small Windows tray application that forces displays to turn off after an idle timeout. Two things set it apart: it enforces display sleep **even while the PC is locked**, when applications (media players, browsers, etc.) are holding active display power requests; and it can **lock the PC the moment the display turns off** — a safeguard Windows does not offer natively, and one that is not typical of other software.

## Why this exists

Normally, when an app holds a "display required" power request (e.g. a video player or web video prevents the screen from dimming/off), Windows ignores its normal idle display timeout. When the PC is locked, this becomes problematic: even if the system's lock-screen timeout would normally turn the display off, an active request keeps it on.

DSE works around this by temporarily overriding the global Windows policy `"Allow display required policy"` (setting it to `No`) while the PC is locked. That makes Windows ignore display power requests, allowing the display to turn off while the PC remains locked and media continues playing. It also restores the original policy on unlock, stop, or exit.

## Features

- **Two independent timers** — one for **unlocked** (idle) enforcement and one for **locked** enforcement. Enable either or both; each has its own `hh:mm:ss` timeout.
  - **Locked** (enabled by default, `00:01:00`): turns displays off N seconds after the workstation is locked, overriding display power requests.
  - **Unlocked** (disabled by default, `00:15:00`): turns displays off when the system has been idle for N seconds while unlocked.
- **Displays power requests are overridden while locked** — handles media players, browsers, etc.
- **Re-asserts** the policy periodically while locked to prevent other software from undoing it.
- **Force displays off now** — a button and a tray-menu item that turn the displays off immediately, whether or not enforcement is running.
- **Lock the PC when the display turns off** — a safeguard Windows does not provide natively and that is not typical of other software. It works independently of Start/Stop for as long as DSE is running, and reacts to any cause of display-off (the power plan, unlocked enforcement, or a manual "Force displays off now").
- **Crash recovery** — if DSE is killed before it can undo its power-plan edits, the original values are restored on the next launch.
- **Single instance** — launching a second copy tells you DSE is already running instead of disturbing the running one.
- **Tray minimization** (optional).
- **Per-user installer** or per-machine installer (choice on first wizard page).
- **Starts with Windows by default** — opt out in the installer or toggle "Start automatically when I sign in" in the app.

## Requirements

- Windows 10 or Windows 11 (x64; ARM64 via x64 emulation)
- .NET Framework 4.x (present on all supported Windows versions)

**Not supported:** Windows 8.1 and earlier, and Windows Server, are untested and not supported. The core mechanisms (the `VIDEOCONLOCK` power setting and `SystemEvents.SessionSwitch`) also exist on Windows 8/8.1, and the `powercfg` APIs predate that, so it *may* work there — but the "Allow display required policy" setting and modern lock-screen/power-request behavior are only documented for Windows 10/11, and DSE has only been tested on Windows 11. Treat 8.1 as untested, and 7/older as unsupported.

## Installation

1. Download the latest `DSE-Setup.exe` from [Releases](https://github.com/AlbaCorey/DisplaySleepEnforcer/releases).
2. Run it. The installer is **unsigned** — SmartScreen may show "unknown publisher." Select *More info → Run anyway* to continue.
3. Choose per-user or per-machine, and finish the installation.

You can also run the portable `DisplaySleepEnforcer.exe` directly (it's included in the release assets as well as in `compiled/` when built from source).

## How to use

1. Launch "Display Sleep Enforcer" (from Start Menu/Desktop, or it starts automatically if sign-in startup is on).
   - Use the **"Start automatically when I sign in"** checkbox to turn sign-in startup on/off at any time.
2. Choose the modes you want with the two checkboxes, each with its own **hh:mm:ss** timeout:
   - **"Enforce when locked"** (checked by default, `00:01:00`): turns the displays off this long after the PC is locked, overriding display power requests so the screen can go off even while media keeps playing.
   - **"Enforce when unlocked"** (unchecked by default, `00:15:00`): turns the displays off after the PC has been idle this long while it is unlocked.
   - Either, both, or neither can be selected. The unlocked countdown pauses while the PC is locked.
3. Adjust the re-check interval if desired, then press **Start**.

To turn the displays off immediately at any time, use **"Force displays off now"** — the button in the window or the right-click item on the tray icon. This works whether or not enforcement is running.

To also have the PC lock whenever the display goes to sleep — for example when the normal power plan turns the display off — tick **"Lock the PC when the display turns off."** Windows has no built-in option to lock on display-off, so this fills that gap. The toggle works independently of Start/Stop: it is active for as long as DSE is running.

The tray icon shows "Display Sleep Enforcer." Right-click to show the window, start/stop, force the displays off, or exit.

## Notes & caveats

- **Pre-release**: lightly tested on one machine (RDP, sleep/hibernate not extensively verified).
- **Global policy**: while locked, DSE writes `ALLOWDISPLAY=0` for the active power scheme (AC/DC). This is a system-wide setting change — it's necessary to ignore all display power requests. DSE captures and restores the original values on unlock/stop/exit.
- **Crash recovery**: before editing the power plan, DSE saves the original values to `HKCU\Software\DisplaySleepEnforcer\Recovery`. If the process is hard-killed, the next launch restores them and logs what it undid, then clears the key. A clean stop clears it immediately.
- **Unsigned binary**: no Authenticode signature; expect SmartScreen warnings.
- **Not admin required**: powercfg writes for the active scheme succeed for a standard user via the Power service.

## How it works

- Detects lock/unlock via `SystemEvents.SessionSwitch` with a debounced desktop-name fallback.
- **Unlocked** and **locked** enforcement are independent, each with its own enable toggle and timeout.
- While locked: sets the "console lock display off timeout" (`VIDEOCONLOCK`) to the configured **locked** timeout plus a 5-second margin (so Windows schedules the display-off), and sets `ALLOWDISPLAY=0` ("Allow display required policy" = No) to override display power requests.
- Re-asserts `ALLOWDISPLAY=0` every ~5 seconds while locked in case another application resets it.
- On unlock/stop: restores both `VIDEOCONLOCK` and `ALLOWDISPLAY` to their original values.
- The same 5-second margin applies to **unlocked** enforcement, so the display turns off `N + 5` seconds after the last input — but only while unlocked (the unlocked countdown pauses once the PC is locked).
- **Lock on display off** subscribes the main window to `GUID_CONSOLE_DISPLAY_STATE` power notifications and calls `LockWorkStation()` whenever the console reports the display off — it does not depend on the Start/Stop state.
- A named mutex enforces a single running instance, so a second copy cannot undo the power-plan changes out from under the live one.

## Building from source

Assumptions: .NET Framework 4.x (csc), PowerShell 5.1+, and [Inno Setup 6](https://jrsoftware.org/isinfo.php) installed at the default location (`%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`).

From the project root:

```powershell
powershell -ExecutionPolicy Bypass -File source\build.ps1
```

This produces:
- `compiled\DisplaySleepEnforcer.exe` (application)
- `compiled\DSE-Setup.exe` (installer)
- `source\DSE.ico` (generated icon; committed)

To build without optional signing, add `-NoSign`. See `source\signing.example.ps1` for signing instructions.

The code lives in `source/DisplaySleepEnforcer.cs`; `compiled/` is build output and should never be edited by hand.

## License

Copyright (C) 2026 AlbaCorey

This program is free software: you can redistribute it and/or modify it under the terms of the **GNU General Public License** as published by the Free Software Foundation, either **version 3 of the License, or (at your option) any later version** (SPDX: `GPL-3.0-or-later`).

It is distributed in the hope that it will be useful, but **WITHOUT ANY WARRANTY**; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the [full text](LICENSE) for details.
