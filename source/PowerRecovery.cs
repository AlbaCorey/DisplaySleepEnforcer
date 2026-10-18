/*
 * Display Sleep Enforcer - force display sleep, including while locked.
 * Copyright (C) 2026 AlbaCorey
 *
 * This program is free software: you can redistribute it and/or modify it under
 * the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later
 * version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along with
 * this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class PowerRecovery
{
    // Records the values of the power settings this app changes so that, if the
    // process is killed before it can put them back, the next launch can restore
    // the user's power plan instead of leaving it altered.
    private const string SubKeyPath = @"Software\DisplaySleepEnforcer\Recovery";
    private const string SchemeValue = "Scheme";
    private const string VideoConLockValue = "VideoConLock";
    private const string AllowDisplayValue = "AllowDisplay";
    private const string RunOncePath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string RunOnceValueName = "DisplaySleepEnforcerRestore";
    internal const string RestorePowerArgument = "--restore-power";

    public static bool Exists()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SubKeyPath, false))
            {
                return key != null && key.GetValue(SchemeValue) != null;
            }
        }
        catch
        {
            return false;
        }
    }

    public static bool Capture()
    {
        // A stored record holds the values to put back, so it must not be
        // overwritten. A record that cannot be read back (partially written, or the
        // scheme no longer exists) can never be restored, so drop it and capture a
        // fresh one - otherwise enforcement would run with no recovery record at all.
        if (Exists())
        {
            if (HasReadableRecord())
            {
                return false;
            }

            Clear();
        }

        Guid scheme;
        if (!PowerSettings.TryGetActiveSchemeGuid(out scheme))
        {
            return false;
        }

        uint vclAc;
        uint vclDc;
        uint allowAc;
        uint allowDc;
        if (!PowerSettings.TryReadVideoConLock(out vclAc, out vclDc) ||
            !PowerSettings.TryReadAllowDisplay(out allowAc, out allowDc))
        {
            return false;
        }

        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SubKeyPath))
            {
                if (key == null)
                {
                    return false;
                }

                key.SetValue(SchemeValue, scheme.ToString("D"), RegistryValueKind.String);
                key.SetValue(VideoConLockValue, vclAc + ";" + vclDc, RegistryValueKind.String);
                key.SetValue(AllowDisplayValue, allowAc + ";" + allowDc, RegistryValueKind.String);
            }

            WriteRunOnce();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryRestore(out string detail)
    {
        detail = null;

        Guid scheme;
        uint vclAc;
        uint vclDc;
        uint allowAc;
        uint allowDc;
        if (!TryReadRecord(out scheme, out vclAc, out vclDc, out allowAc, out allowDc))
        {
            return false;
        }

        if (!PowerSettings.TryRestore(scheme, vclAc, vclDc, allowAc, allowDc))
        {
            return false;
        }

        detail = "lock display timeout " + vclAc + "s, allow-display policy " + allowAc + "/" + allowDc;
        Clear();
        return true;
    }

    // Reads the stored record back into usable values. False when there is no
    // record or when it cannot be parsed.
    private static bool TryReadRecord(
        out Guid scheme, out uint vclAc, out uint vclDc, out uint allowAc, out uint allowDc)
    {
        scheme = Guid.Empty;
        vclAc = 0;
        vclDc = 0;
        allowAc = 0;
        allowDc = 0;

        string schemeText;
        string vclText;
        string allowText;
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SubKeyPath, false))
            {
                if (key == null)
                {
                    return false;
                }

                schemeText = key.GetValue(SchemeValue) as string;
                vclText = key.GetValue(VideoConLockValue) as string;
                allowText = key.GetValue(AllowDisplayValue) as string;
            }
        }
        catch
        {
            return false;
        }

        if (!Guid.TryParse(schemeText, out scheme))
        {
            return false;
        }

        return TryParsePair(vclText, out vclAc, out vclDc) &&
            TryParsePair(allowText, out allowAc, out allowDc);
    }

    private static bool HasReadableRecord()
    {
        Guid scheme;
        uint vclAc;
        uint vclDc;
        uint allowAc;
        uint allowDc;
        return TryReadRecord(out scheme, out vclAc, out vclDc, out allowAc, out allowDc);
    }

    public static void Clear()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(SubKeyPath, false);
        }
        catch
        {
        }

        ClearRunOnce();
    }

    // Schedules a one-shot cleanup for the next sign-in while a recovery record
    // exists. RunOnce runs independently of the start-at-sign-in setting, so the
    // power plan is put back even if the process is hard-killed and never launched
    // again on its own.
    private static void WriteRunOnce()
    {
        try
        {
            string exe = Application.ExecutablePath;
            if (string.IsNullOrEmpty(exe))
            {
                return;
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunOncePath))
            {
                if (key != null)
                {
                    key.SetValue(RunOnceValueName, "\"" + exe + "\" " + RestorePowerArgument,
                        RegistryValueKind.String);
                }
            }
        }
        catch
        {
        }
    }

    public static void ClearRunOnce()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunOncePath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(RunOnceValueName, false);
                }
            }
        }
        catch
        {
        }
    }

    private static bool TryParsePair(string text, out uint ac, out uint dc)
    {
        ac = 0;
        dc = 0;
        if (text == null)
        {
            return false;
        }

        string[] parts = text.Split(';');
        return parts.Length == 2 &&
            uint.TryParse(parts[0], out ac) &&
            uint.TryParse(parts[1], out dc);
    }
}
