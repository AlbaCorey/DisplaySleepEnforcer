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
using System.Runtime.InteropServices;
using System.Text;

internal static class Native
{
    public const uint WM_SYSCOMMAND = 0x0112;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint WM_POWERBROADCAST = 0x0218;
    public const uint PBT_POWERSETTINGCHANGE = 0x8013;
    public const uint SMTO_ABORTIFHUNG = 0x0002;
    private const uint DESKTOP_READOBJECTS = 0x0001;
    private const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x0000;
    private const int UOI_NAME = 2;
    // DWMWA_USE_IMMERSIVE_DARK_MODE: 20 on Windows 10 20H1+, 19 on 1809/1903.
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModePre20H1 = 19;

    public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
    public static readonly IntPtr SC_MONITORPOWER = new IntPtr(0xF170);
    public static readonly IntPtr MONITOR_OFF = new IntPtr(2);
    public static readonly Guid ConsoleDisplayState =
        new Guid("6fe69556-704a-47a0-8f24-c28d936fda47");

    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public uint DataLength;
        public byte Data;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetUserObjectInformation(
        IntPtr hObj, int nIndex, StringBuilder pvInfo, int nLength, out int lpnLengthNeeded);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(
        IntPtr hRecipient, ref Guid powerSettingGuid, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void SetImmersiveDarkMode(IntPtr hwnd, bool enabled)
    {
        int value = enabled ? 1 : 0;
        try
        {
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModePre20H1, ref value, sizeof(int));
            }
        }
        catch
        {
        }
    }

    public static void TurnOffMonitors()
    {
        IntPtr result;
        SendMessageTimeout(
            HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_OFF,
            SMTO_ABORTIFHUNG, 1000, out result);
    }

    public static uint GetIdleMilliseconds()
    {
        LASTINPUTINFO info = new LASTINPUTINFO();
        info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        if (!GetLastInputInfo(ref info))
        {
            return 0;
        }
        return unchecked((uint)Environment.TickCount - info.dwTime);
    }

    public static bool IsWorkstationLocked()
    {
        // The current input desktop is "Default" while unlocked and the secure
        // "Winlogon" desktop while the session is locked. OpenInputDesktop still
        // succeeds when locked (returning the Winlogon desktop), so the handle
        // being non-null is not enough - the desktop name is what matters.
        IntPtr desktop = OpenInputDesktop(0, false, DESKTOP_READOBJECTS);
        if (desktop == IntPtr.Zero)
        {
            return true;
        }

        try
        {
            StringBuilder name = new StringBuilder(64);
            int needed;
            if (GetUserObjectInformation(desktop, UOI_NAME, name, name.Capacity, out needed))
            {
                return !string.Equals(name.ToString(), "Default", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        finally
        {
            CloseDesktop(desktop);
        }
    }

    public static IntPtr RegisterDisplayStateNotification(IntPtr window)
    {
        Guid setting = ConsoleDisplayState;
        return RegisterPowerSettingNotification(window, ref setting, DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    public static void UnregisterNotification(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            UnregisterPowerSettingNotification(handle);
        }
    }

    public static bool Lock()
    {
        return LockWorkStation();
    }
}
