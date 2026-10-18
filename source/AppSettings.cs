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
using Microsoft.Win32;

internal sealed class AppSettings
{
    public bool UnlockedEnabled;
    public int UnlockedSeconds = 900;
    public bool LockedEnabled = true;
    public int LockedSeconds = 60;
    public bool MinimizeToTray = true;
    public bool LockOnSleep;
    public bool ConfirmExit;
    public bool AutoStartEnforcement;
    public int Theme;
}

internal static class SettingsStore
{
    private const string SubKeyPath = @"Software\DisplaySleepEnforcer\Settings";
    private const string UnlockedEnabledValue = "UnlockedEnabled";
    private const string UnlockedSecondsValue = "UnlockedSeconds";
    private const string LockedEnabledValue = "LockedEnabled";
    private const string LockedSecondsValue = "LockedSeconds";
    private const string MinimizeToTrayValue = "MinimizeToTray";
    private const string LockOnSleepValue = "LockOnSleep";
    private const string ConfirmExitValue = "ConfirmExit";
    private const string AutoStartValue = "AutoStartEnforcement";
    private const string ThemeValue = "Theme";

    public static AppSettings Load()
    {
        AppSettings settings = new AppSettings();
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SubKeyPath, false))
            {
                if (key == null)
                {
                    return settings;
                }

                settings.UnlockedEnabled = ReadBool(key, UnlockedEnabledValue, settings.UnlockedEnabled);
                settings.UnlockedSeconds = ReadInt(key, UnlockedSecondsValue, settings.UnlockedSeconds);
                settings.LockedEnabled = ReadBool(key, LockedEnabledValue, settings.LockedEnabled);
                settings.LockedSeconds = ReadInt(key, LockedSecondsValue, settings.LockedSeconds);
                settings.MinimizeToTray = ReadBool(key, MinimizeToTrayValue, settings.MinimizeToTray);
                settings.LockOnSleep = ReadBool(key, LockOnSleepValue, settings.LockOnSleep);
                settings.ConfirmExit = ReadBool(key, ConfirmExitValue, settings.ConfirmExit);
                settings.AutoStartEnforcement = ReadBool(key, AutoStartValue, settings.AutoStartEnforcement);
                settings.Theme = ReadInt(key, ThemeValue, settings.Theme);
            }
        }
        catch
        {
        }

        return settings;
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SubKeyPath))
            {
                if (key == null)
                {
                    return;
                }

                key.SetValue(UnlockedEnabledValue, settings.UnlockedEnabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(UnlockedSecondsValue, settings.UnlockedSeconds, RegistryValueKind.DWord);
                key.SetValue(LockedEnabledValue, settings.LockedEnabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(LockedSecondsValue, settings.LockedSeconds, RegistryValueKind.DWord);
                key.SetValue(MinimizeToTrayValue, settings.MinimizeToTray ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(LockOnSleepValue, settings.LockOnSleep ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(ConfirmExitValue, settings.ConfirmExit ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(AutoStartValue, settings.AutoStartEnforcement ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue(ThemeValue, settings.Theme, RegistryValueKind.DWord);
            }
        }
        catch
        {
        }
    }

    private static bool ReadBool(RegistryKey key, string name, bool fallback)
    {
        object value = key.GetValue(name);
        if (value == null)
        {
            return fallback;
        }

        try
        {
            return Convert.ToInt32(value) != 0;
        }
        catch
        {
            return fallback;
        }
    }

    private static int ReadInt(RegistryKey key, string name, int fallback)
    {
        object value = key.GetValue(name);
        if (value == null)
        {
            return fallback;
        }

        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return fallback;
        }
    }
}
