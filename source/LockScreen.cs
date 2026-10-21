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

// The lock-screen slideshow keeps requesting that the display stay on, which
// defeats enforcement. Windows stores the background choice as a single DWORD:
// 1 = Slideshow; 0 or missing = Picture/Spotlight (not a slideshow). The
// Settings app writes the same value when you pick Picture there, so the
// stored image is kept and only the background type changes.
internal static class LockScreen
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Lock Screen";
    private const string SlideshowEnabledValue = "SlideshowEnabled";

    public static bool IsSlideshowEnabled()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, false))
            {
                object raw = key != null ? key.GetValue(SlideshowEnabledValue) : null;
                return raw is int && (int)raw == 1;
            }
        }
        catch
        {
            return false;
        }
    }

    public static bool TrySetPictureMode()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                if (key == null)
                {
                    return false;
                }

                key.SetValue(SlideshowEnabledValue, 0, RegistryValueKind.DWord);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}
