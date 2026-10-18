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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

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

internal static class PowerSettings
{
    private static readonly Guid SubVideo = new Guid("7516b95f-f776-4464-8c53-06167f40cc99");
    private static readonly Guid VideoConLock = new Guid("8ec4b3a5-6868-48c2-be75-4f3044be88a7");
    private static readonly Guid AllowDisplay = new Guid("a9ceb8da-cd46-44fb-a98b-02af69de4623");

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroup, ref Guid setting, out uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroup, ref Guid setting, out uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroup, ref Guid setting, uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroup, ref Guid setting, uint value);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private static bool TryGetActiveScheme(out Guid scheme)
    {
        scheme = Guid.Empty;
        IntPtr handle;
        if (PowerGetActiveScheme(IntPtr.Zero, out handle) != 0 || handle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            scheme = (Guid)Marshal.PtrToStructure(handle, typeof(Guid));
            return true;
        }
        finally
        {
            LocalFree(handle);
        }
    }

    public static bool TryReadVideoConLock(out uint ac, out uint dc)
    {
        return TryReadValue(VideoConLock, out ac, out dc);
    }

    public static bool TryWriteVideoConLock(uint ac, uint dc)
    {
        return TryWriteValue(VideoConLock, ac, dc);
    }

    public static bool TryReadAllowDisplay(out uint ac, out uint dc)
    {
        return TryReadValue(AllowDisplay, out ac, out dc);
    }

    public static bool TryWriteAllowDisplay(uint ac, uint dc)
    {
        return TryWriteValue(AllowDisplay, ac, dc);
    }

    public static bool TryGetActiveSchemeGuid(out Guid scheme)
    {
        return TryGetActiveScheme(out scheme);
    }

    public static bool TryRestore(
        Guid scheme, uint videoConLockAc, uint videoConLockDc, uint allowDisplayAc, uint allowDisplayDc)
    {
        Guid sub = SubVideo;
        Guid videoConLock = VideoConLock;
        Guid allowDisplay = AllowDisplay;
        uint vclAcResult = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref videoConLock, videoConLockAc);
        uint vclDcResult = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref videoConLock, videoConLockDc);
        uint allowAcResult = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref allowDisplay, allowDisplayAc);
        uint allowDcResult = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref allowDisplay, allowDisplayDc);
        uint activeResult = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
        return vclAcResult == 0 && vclDcResult == 0 && allowAcResult == 0 &&
            allowDcResult == 0 && activeResult == 0;
    }

    private static bool TryReadValue(Guid settingGuid, out uint ac, out uint dc)
    {
        ac = 0;
        dc = 0;

        Guid scheme;
        if (!TryGetActiveScheme(out scheme))
        {
            return false;
        }

        Guid sub = SubVideo;
        Guid setting = settingGuid;
        return PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out ac) == 0
            && PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out dc) == 0;
    }

    private static bool TryWriteValue(Guid settingGuid, uint ac, uint dc)
    {
        Guid scheme;
        if (!TryGetActiveScheme(out scheme))
        {
            return false;
        }

        Guid sub = SubVideo;
        Guid setting = settingGuid;
        uint acResult = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, ac);
        uint dcResult = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, dc);
        uint activeResult = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
        return acResult == 0 && dcResult == 0 && activeResult == 0;
    }
}

internal sealed class LockDisplayTimeout
{
    private uint _originalAc;
    private uint _originalDc;
    private bool _applied;

    public bool Apply(uint lockedDisplayOffSeconds)
    {
        if (!PowerSettings.TryReadVideoConLock(out _originalAc, out _originalDc))
        {
            return false;
        }

        if (!PowerSettings.TryWriteVideoConLock(lockedDisplayOffSeconds, lockedDisplayOffSeconds))
        {
            return false;
        }

        _applied = true;
        return true;
    }

    public void Restore()
    {
        if (!_applied)
        {
            return;
        }

        PowerSettings.TryWriteVideoConLock(_originalAc, _originalDc);
        _applied = false;
    }
}

internal sealed class DisplayRequestPolicy
{
    private uint _originalAc = 1;
    private uint _originalDc = 1;
    private bool _captured;
    private bool _suppressed;

    // Setting "Allow display required policy" to No (0) makes Windows ignore
    // display power requests from all applications, so the display can still
    // turn off while locked even when a media player is holding it awake.
    public bool Suppress()
    {
        if (_suppressed)
        {
            return true;
        }

        if (!_captured)
        {
            if (!PowerSettings.TryReadAllowDisplay(out _originalAc, out _originalDc))
            {
                _originalAc = 1;
                _originalDc = 1;
            }

            _captured = true;
        }

        if (!PowerSettings.TryWriteAllowDisplay(0, 0))
        {
            return false;
        }

        _suppressed = true;
        return true;
    }

    public void Restore()
    {
        if (!_suppressed)
        {
            return;
        }

        PowerSettings.TryWriteAllowDisplay(_originalAc, _originalDc);
        _suppressed = false;
    }

    // Re-writes the suppression in case another component changed the setting.
    public bool Reassert()
    {
        if (!_suppressed)
        {
            return false;
        }

        return PowerSettings.TryWriteAllowDisplay(0, 0);
    }
}

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
        if (Exists())
        {
            return false;
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

        Guid scheme;
        uint vclAc;
        uint vclDc;
        uint allowAc;
        uint allowDc;
        if (!Guid.TryParse(schemeText, out scheme) ||
            !TryParsePair(vclText, out vclAc, out vclDc) ||
            !TryParsePair(allowText, out allowAc, out allowDc))
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

internal static class Theme
{
    public const int SystemMode = 0;
    public const int LightMode = 1;
    public const int DarkMode = 2;

    public static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
    public static readonly Color DarkSurface = Color.FromArgb(45, 45, 48);
    public static readonly Color DarkText = Color.FromArgb(235, 235, 235);
    public static readonly Color DarkBorder = Color.FromArgb(82, 82, 82);
    public static readonly Color DarkButton = Color.FromArgb(51, 51, 55);
    public static readonly Color DarkHighlight = Color.FromArgb(62, 62, 66);

    public static bool IsSystemDark()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
            {
                if (key != null)
                {
                    object value = key.GetValue("AppsUseLightTheme");
                    if (value != null)
                    {
                        return Convert.ToInt32(value) == 0;
                    }
                }
            }
        }
        catch
        {
        }

        return false;
    }

    public static bool Resolve(int mode)
    {
        if (mode == DarkMode)
        {
            return true;
        }

        if (mode == LightMode)
        {
            return false;
        }

        return IsSystemDark();
    }

    public static void Apply(Form form, bool dark)
    {
        form.BackColor = dark ? DarkBackground : SystemColors.Control;
        form.ForeColor = dark ? DarkText : SystemColors.ControlText;

        foreach (Control control in form.Controls)
        {
            ApplyToControl(control, dark);
        }
    }

    private static void ApplyToControl(Control control, bool dark)
    {
        Button button = control as Button;
        if (button != null)
        {
            button.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
            button.UseVisualStyleBackColor = !dark;
            button.BackColor = dark ? DarkButton : SystemColors.Control;
            button.ForeColor = dark ? DarkText : SystemColors.ControlText;
            button.FlatAppearance.BorderColor = dark ? DarkBorder : SystemColors.Control;
            button.FlatAppearance.BorderSize = dark ? 1 : 0;
            button.FlatAppearance.MouseOverBackColor = dark ? DarkHighlight : SystemColors.Control;
            button.FlatAppearance.MouseDownBackColor = dark ? DarkBorder : SystemColors.Control;
        }
        else if (control is TextBox)
        {
            control.BackColor = dark ? DarkSurface : Color.White;
            control.ForeColor = dark ? DarkText : SystemColors.WindowText;
        }
        else if (control is NumericUpDown)
        {
            control.BackColor = dark ? DarkSurface : SystemColors.Window;
            control.ForeColor = dark ? DarkText : SystemColors.WindowText;
        }
        else if (control is Label)
        {
            control.BackColor = Color.Transparent;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }
        else if (control is CheckBox)
        {
            control.BackColor = dark ? DarkBackground : SystemColors.Control;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }
        else
        {
            control.BackColor = dark ? DarkBackground : SystemColors.Control;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }

        foreach (Control child in control.Controls)
        {
            ApplyToControl(child, dark);
        }
    }
}

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return Theme.DarkSurface; } }
    public override Color MenuItemSelected { get { return Theme.DarkHighlight; } }
    public override Color MenuItemSelectedGradientBegin { get { return Theme.DarkHighlight; } }
    public override Color MenuItemSelectedGradientEnd { get { return Theme.DarkHighlight; } }
    public override Color MenuItemPressedGradientBegin { get { return Theme.DarkHighlight; } }
    public override Color MenuItemPressedGradientMiddle { get { return Theme.DarkHighlight; } }
    public override Color MenuItemPressedGradientEnd { get { return Theme.DarkHighlight; } }
    public override Color MenuItemBorder { get { return Theme.DarkHighlight; } }
    public override Color MenuBorder { get { return Theme.DarkBorder; } }
    public override Color CheckBackground { get { return Theme.DarkSurface; } }
    public override Color CheckSelectedBackground { get { return Theme.DarkHighlight; } }
    public override Color CheckPressedBackground { get { return Theme.DarkHighlight; } }
    public override Color ImageMarginGradientBegin { get { return Theme.DarkSurface; } }
    public override Color ImageMarginGradientMiddle { get { return Theme.DarkSurface; } }
    public override Color ImageMarginGradientEnd { get { return Theme.DarkSurface; } }
    public override Color SeparatorDark { get { return Theme.DarkBorder; } }
    public override Color SeparatorLight { get { return Theme.DarkSurface; } }
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer()
        : base(new DarkColorTable())
    {
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        Rectangle bounds = e.ImageRectangle;
        using (Pen pen = new Pen(Theme.DarkText, 2f))
        {
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            Point[] points = new Point[]
            {
                new Point(bounds.Left + 2, bounds.Top + bounds.Height / 2),
                new Point(bounds.Left + bounds.Width / 2, bounds.Bottom - 4),
                new Point(bounds.Right - 2, bounds.Top + 3)
            };
            e.Graphics.DrawLines(pen, points);
        }
    }
}

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

internal sealed class LockWatcher
{
    // The input-desktop poll used to be the only signal, but it can report a
    // stale value (e.g. "Default") for the whole duration of a lock, which
    // caused the enforcer to think the PC had been unlocked and release the
    // display-request suppression. The reliable signal is the session-change
    // notification; the poll is kept only as a debounced fallback for the rare
    // case where notifications never arrive.
    private const int DebounceSamples = 4;

    private volatile bool _locked;
    private volatile bool _sawNotification;
    private int _disagree;

    public LockWatcher()
    {
        try
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }
        catch
        {
            // Notifications unavailable; the poll fallback still works.
        }
    }

    public bool Locked
    {
        get { return _locked; }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            _locked = true;
            _sawNotification = true;
            _disagree = 0;
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            _locked = false;
            _sawNotification = true;
            _disagree = 0;
        }
    }

    // Called periodically from the enforcer loop.
    public void Sample()
    {
        if (_sawNotification)
        {
            return;
        }

        bool polled = Native.IsWorkstationLocked();
        if (polled == _locked)
        {
            _disagree = 0;
            return;
        }

        _disagree++;
        if (_disagree >= DebounceSamples)
        {
            _locked = polled;
            _disagree = 0;
        }
    }
}

internal sealed class Enforcer
{
    // While locked the suppression is re-written every this many loop passes
    // (~5s at the 250 ms lock-check cadence) to defeat anything that resets it.
    private const int ReassertIntervalIterations = 20;

    private const double CountdownDelaySeconds = 5.0;

    private volatile bool _running;
    private Thread _thread;
    private double _unlockedSeconds = 60.0;
    private double _lockedSeconds = 60.0;
    private double _enforceSeconds = 2.0;
    private bool _unlockedEnabled;
    private bool _lockedEnabled;
    private bool _wasLocked;
    private bool _recoveryCaptured;
    private readonly LockDisplayTimeout _lockTimeout = new LockDisplayTimeout();
    private readonly DisplayRequestPolicy _requestPolicy = new DisplayRequestPolicy();
    private readonly LockWatcher _lockWatch = new LockWatcher();

    public event Action<string> Message;

    public bool IsRunning
    {
        get { return _running; }
    }

    public void Start(double unlockedSeconds, bool unlockedEnabled,
        double lockedSeconds, bool lockedEnabled, double enforceSeconds)
    {
        if (_running)
        {
            return;
        }

        _unlockedSeconds = unlockedSeconds;
        _unlockedEnabled = unlockedEnabled;
        _lockedSeconds = lockedSeconds;
        _lockedEnabled = lockedEnabled;
        _enforceSeconds = enforceSeconds;
        _running = true;
        _thread = new Thread(Loop);
        _thread.IsBackground = true;
        _thread.Name = "DisplaySleepEnforcer";
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        Thread thread = _thread;
        if (thread != null)
        {
            thread.Join(2000);
            _thread = null;
        }
    }

    private void Loop()
    {
        uint unlockedThreshold = (uint)((_unlockedSeconds + CountdownDelaySeconds) * 1000.0);
        int delay = (int)(_enforceSeconds * 1000.0);
        bool forced = false;
        int sinceReassert = 0;

        if (_lockedEnabled)
        {
            _recoveryCaptured = PowerRecovery.Capture();

            uint lockedSeconds = (uint)Math.Round(_lockedSeconds + CountdownDelaySeconds);
            if (_lockTimeout.Apply(lockedSeconds))
            {
                Raise("Enforcement active (locked): the display will turn off " +
                    _lockedSeconds + "s after the PC is locked.");
            }
            else
            {
                Raise("Warning: could not set the lock-screen display timeout; " +
                    "enforcement while locked may rely on the Windows default.");
            }
        }

        if (_unlockedEnabled)
        {
            Raise("Enforcement active (unlocked): displays off after " + _unlockedSeconds + "s idle.");
        }

        try
        {
            while (_running)
            {
                uint idle = Native.GetIdleMilliseconds();

                _lockWatch.Sample();
                bool lockedNow = _lockWatch.Locked;
                if (lockedNow != _wasLocked)
                {
                    _wasLocked = lockedNow;
                    Raise(lockedNow ? "PC locked." : "PC unlocked.");

                    if (_lockedEnabled)
                    {
                        if (lockedNow)
                        {
                            Raise("Locked countdown started.");

                            if (_requestPolicy.Suppress())
                            {
                                Raise("Overriding display power requests so the screen can turn off while locked.");
                            }
                            else
                            {
                                Raise("Warning: could not override display power requests.");
                            }
                        }
                        else
                        {
                            _requestPolicy.Restore();
                        }
                    }
                }

                if (_lockedEnabled && _wasLocked)
                {
                    sinceReassert++;
                    if (sinceReassert >= ReassertIntervalIterations)
                    {
                        sinceReassert = 0;
                        _requestPolicy.Reassert();
                    }
                }
                else
                {
                    sinceReassert = 0;
                }

                bool enforceNow = _unlockedEnabled && !lockedNow && idle >= unlockedThreshold;
                if (enforceNow)
                {
                    Native.TurnOffMonitors();
                }

                if (enforceNow != forced)
                {
                    forced = enforceNow;
                    Raise(enforceNow
                        ? "Idle detected - display sleep enforced."
                        : "User activity - enforcement paused.");
                }

                SleepRespectingStop(enforceNow ? delay : 250);
            }
        }
        finally
        {
            _requestPolicy.Restore();
            _lockTimeout.Restore();

            if (_recoveryCaptured)
            {
                _recoveryCaptured = false;
                PowerRecovery.Clear();
            }
        }

        Raise("Enforcement stopped.");
    }

    private void SleepRespectingStop(int milliseconds)
    {
        int slept = 0;
        while (_running && slept < milliseconds)
        {
            int slice = Math.Min(100, milliseconds - slept);
            Thread.Sleep(slice);
            slept += slice;
        }
    }

    private void Raise(string message)
    {
        Action<string> handler = Message;
        if (handler != null)
        {
            handler(message);
        }
    }
}

internal sealed class MainForm : Form
{
    private const string AppVersion = "1.0.0";
    private const double RecheckSeconds = 2.0;
    private const string TaskName = "DisplaySleepEnforcer";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "DisplaySleepEnforcer";

    private readonly NumericUpDown _unlockedHours;
    private readonly NumericUpDown _unlockedMinutes;
    private readonly NumericUpDown _unlockedSeconds;
    private readonly NumericUpDown _lockedHours;
    private readonly NumericUpDown _lockedMinutes;
    private readonly NumericUpDown _lockedSeconds;
    private readonly Button _startButton;
    private readonly Button _forceButton;
    private readonly CheckBox _trayCheck;
    private readonly CheckBox _unlockedCheck;
    private readonly CheckBox _lockedCheck;
    private readonly CheckBox _startupCheck;
    private readonly CheckBox _autoStartCheck;
    private readonly CheckBox _lockOnSleepCheck;
    private readonly CheckBox _confirmExitCheck;
    private readonly Label _statusLabel;
    private readonly TextBox _logBox;
    private readonly NotifyIcon _tray;
    private readonly ToolTip _toolTip = new ToolTip();
    private readonly Enforcer _enforcer = new Enforcer();
    private readonly Icon _appIcon;
    private bool _closing;
    private bool _loadingSettings;
    private bool _startupVerified;
    private bool _autoStarted;
    private int _themeMode = Theme.SystemMode;
    private bool _dark;
    private readonly DarkMenuRenderer _darkMenuRenderer = new DarkMenuRenderer();
    private readonly ToolStripMenuItem _appearanceSystem;
    private readonly ToolStripMenuItem _appearanceLight;
    private readonly ToolStripMenuItem _appearanceDark;
    private readonly ToolStripMenuItem _trayToggleItem;
    private IntPtr _displayNotify = IntPtr.Zero;

    public MainForm()
    {
        Text = "Display Sleep Enforcer v" + AppVersion;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 432);
        Font = new Font("Segoe UI", 9F);

        _appIcon = LoadAppIcon();
        if (_appIcon != null)
        {
            Icon = _appIcon;
        }

        _unlockedCheck = new CheckBox();
        _unlockedCheck.Text = "Enforce when unlocked:";
        _unlockedCheck.Location = new Point(12, 14);
        _unlockedCheck.AutoSize = true;
        _unlockedCheck.Checked = false;
        _toolTip.SetToolTip(_unlockedCheck,
            "Turn displays off after the PC has been idle this long while it is unlocked.");

        _unlockedHours = CreateTimeSpinner(new Point(244, 12), 99, 0, "Hours");
        _unlockedMinutes = CreateTimeSpinner(new Point(304, 12), 59, 15, "Minutes");
        _unlockedSeconds = CreateTimeSpinner(new Point(364, 12), 59, 0, "Seconds");

        Label unlockedColon1 = new Label();
        unlockedColon1.Text = ":";
        unlockedColon1.Location = new Point(291, 16);
        unlockedColon1.AutoSize = true;

        Label unlockedColon2 = new Label();
        unlockedColon2.Text = ":";
        unlockedColon2.Location = new Point(351, 16);
        unlockedColon2.AutoSize = true;

        _lockedCheck = new CheckBox();
        _lockedCheck.Text = "Enforce when locked:";
        _lockedCheck.Location = new Point(12, 50);
        _lockedCheck.AutoSize = true;
        _lockedCheck.Checked = true;
        _toolTip.SetToolTip(_lockedCheck,
            "Turn displays off this long after the PC is locked, overriding display power requests.");

        _lockedHours = CreateTimeSpinner(new Point(244, 48), 99, 0, "Hours");
        _lockedMinutes = CreateTimeSpinner(new Point(304, 48), 59, 1, "Minutes");
        _lockedSeconds = CreateTimeSpinner(new Point(364, 48), 59, 0, "Seconds");

        Label lockedColon1 = new Label();
        lockedColon1.Text = ":";
        lockedColon1.Location = new Point(291, 52);
        lockedColon1.AutoSize = true;

        Label lockedColon2 = new Label();
        lockedColon2.Text = ":";
        lockedColon2.Location = new Point(351, 52);
        lockedColon2.AutoSize = true;

        _autoStartCheck = new CheckBox();
        _autoStartCheck.Text = "Enable enforcement immediately on start";
        _autoStartCheck.Location = new Point(12, 88);
        _autoStartCheck.AutoSize = true;
        _toolTip.SetToolTip(_autoStartCheck,
            "Starts enforcement automatically as soon as the app opens, using the mode " +
            "checkboxes above. Otherwise the app opens stopped and you press Turn on yourself.");

        _startupCheck = new CheckBox();
        _startupCheck.Text = "Start DSE automatically when I sign in to Windows";
        _startupCheck.Location = new Point(12, 112);
        _startupCheck.AutoSize = true;
        _startupCheck.Checked = IsStartupEnabled();
        _startupCheck.CheckedChanged += StartupCheckChanged;

        _lockOnSleepCheck = new CheckBox();
        _lockOnSleepCheck.Text = "Lock the PC when the display turns off";
        _lockOnSleepCheck.Location = new Point(12, 136);
        _lockOnSleepCheck.AutoSize = true;
        _lockOnSleepCheck.CheckedChanged += LockOnSleepCheckChanged;
        _toolTip.SetToolTip(_lockOnSleepCheck,
            "Locks the PC whenever the display turns off (including when the power plan " +
            "turns it off). Active while this app is running, independent of the Start/Stop button.");

        _confirmExitCheck = new CheckBox();
        _confirmExitCheck.Text = "Ask before exiting";
        _confirmExitCheck.Location = new Point(12, 160);
        _confirmExitCheck.AutoSize = true;
        _toolTip.SetToolTip(_confirmExitCheck,
            "Show a confirmation prompt before the app closes, so the tray Exit item " +
            "cannot shut it down by accident.");

        _trayCheck = new CheckBox();
        _trayCheck.Text = "Minimize to tray";
        _trayCheck.Location = new Point(12, 184);
        _trayCheck.AutoSize = true;
        _trayCheck.Checked = true;

        _startButton = new Button();
        _startButton.Text = "Turn on";
        _startButton.Location = new Point(12, 216);
        _startButton.Size = new Size(194, 32);
        _startButton.Click += StartButtonClick;

        _forceButton = new Button();
        _forceButton.Text = "Force displays off now";
        _forceButton.Location = new Point(214, 216);
        _forceButton.Size = new Size(194, 32);
        _forceButton.Click += ForceButtonClick;

        _statusLabel = new Label();
        _statusLabel.Text = "Stopped.";
        _statusLabel.Location = new Point(12, 262);
        _statusLabel.AutoSize = true;

        _logBox = new TextBox();
        _logBox.Location = new Point(12, 288);
        _logBox.Size = new Size(396, 132);
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.BackColor = Color.White;
        _logBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

        Controls.Add(_unlockedCheck);
        Controls.Add(_unlockedHours);
        Controls.Add(unlockedColon1);
        Controls.Add(_unlockedMinutes);
        Controls.Add(unlockedColon2);
        Controls.Add(_unlockedSeconds);
        Controls.Add(_lockedCheck);
        Controls.Add(_lockedHours);
        Controls.Add(lockedColon1);
        Controls.Add(_lockedMinutes);
        Controls.Add(lockedColon2);
        Controls.Add(_lockedSeconds);
        Controls.Add(_autoStartCheck);
        Controls.Add(_startupCheck);
        Controls.Add(_lockOnSleepCheck);
        Controls.Add(_confirmExitCheck);
        Controls.Add(_trayCheck);
        Controls.Add(_startButton);
        Controls.Add(_forceButton);
        Controls.Add(_statusLabel);
        Controls.Add(_logBox);

        _enforcer.Message += OnEnforcerMessage;

        _tray = new NotifyIcon();
        _tray.Icon = _appIcon != null ? _appIcon : SystemIcons.Application;
        _tray.Text = "Display Sleep Enforcer";
        _tray.Visible = true;
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, delegate { RestoreFromTray(); });
        _trayToggleItem = new ToolStripMenuItem("Turn on");
        _trayToggleItem.Click += delegate { ToggleEnforcement(); };
        menu.Items.Add(_trayToggleItem);
        menu.Items.Add("Force displays off now", null, delegate { ForceDisplayOff(); });
        menu.Items.Add(new ToolStripSeparator());

        _appearanceSystem = new ToolStripMenuItem("Follow system theme");
        _appearanceSystem.Click += delegate { SetThemeMode(Theme.SystemMode); };
        _appearanceLight = new ToolStripMenuItem("Light");
        _appearanceLight.Click += delegate { SetThemeMode(Theme.LightMode); };
        _appearanceDark = new ToolStripMenuItem("Dark");
        _appearanceDark.Click += delegate { SetThemeMode(Theme.DarkMode); };

        ToolStripMenuItem appearance = new ToolStripMenuItem("Appearance");
        appearance.DropDownItems.Add(_appearanceSystem);
        appearance.DropDownItems.Add(_appearanceLight);
        appearance.DropDownItems.Add(_appearanceDark);
        menu.Items.Add(appearance);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { Close(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += delegate { RestoreFromTray(); };

        WireSettingsPersistence();

        RunCrashRecovery();
        ApplySavedSettings();

        Resize += FormResize;
        Shown += MainFormShown;
        FormClosing += FormClosingHandler;
    }

    private void RunCrashRecovery()
    {
        if (!PowerRecovery.Exists())
        {
            return;
        }

        string detail;
        if (PowerRecovery.TryRestore(out detail))
        {
            AppendLog("Recovered power settings left changed by a previous run (" + detail + ").");
        }
        else
        {
            AppendLog("A previous run left power settings changed but they could not be restored.");
        }
    }

    private void WireSettingsPersistence()
    {
        _unlockedCheck.CheckedChanged += SettingsChanged;
        _lockedCheck.CheckedChanged += SettingsChanged;
        _trayCheck.CheckedChanged += SettingsChanged;
        _autoStartCheck.CheckedChanged += SettingsChanged;
        _lockOnSleepCheck.CheckedChanged += SettingsChanged;
        _confirmExitCheck.CheckedChanged += SettingsChanged;
        _unlockedHours.ValueChanged += SettingsChanged;
        _unlockedMinutes.ValueChanged += SettingsChanged;
        _unlockedSeconds.ValueChanged += SettingsChanged;
        _lockedHours.ValueChanged += SettingsChanged;
        _lockedMinutes.ValueChanged += SettingsChanged;
        _lockedSeconds.ValueChanged += SettingsChanged;
    }

    private void ApplySavedSettings()
    {
        AppSettings settings = SettingsStore.Load();

        _themeMode = NormalizeTheme(settings.Theme);

        _loadingSettings = true;
        try
        {
            _unlockedCheck.Checked = settings.UnlockedEnabled;
            SetTimeSpinners(_unlockedHours, _unlockedMinutes, _unlockedSeconds, settings.UnlockedSeconds);
            _lockedCheck.Checked = settings.LockedEnabled;
            SetTimeSpinners(_lockedHours, _lockedMinutes, _lockedSeconds, settings.LockedSeconds);
            _trayCheck.Checked = settings.MinimizeToTray;
            _autoStartCheck.Checked = settings.AutoStartEnforcement;
            _lockOnSleepCheck.Checked = settings.LockOnSleep;
            _confirmExitCheck.Checked = settings.ConfirmExit;
        }
        finally
        {
            _loadingSettings = false;
        }

        ApplyTheme();
    }

    private static int NormalizeTheme(int mode)
    {
        if (mode == Theme.LightMode || mode == Theme.DarkMode)
        {
            return mode;
        }

        return Theme.SystemMode;
    }

    private void SetThemeMode(int mode)
    {
        _themeMode = NormalizeTheme(mode);
        ApplyTheme();
        SettingsChanged(this, EventArgs.Empty);
    }

    private void ApplyTheme()
    {
        _dark = Theme.Resolve(_themeMode);
        Theme.Apply(this, _dark);
        ApplyTrayTheme(_dark);
        ApplyTitleBarTheme(_dark);
        UpdateThemeMenuChecks();
    }

    private void ApplyTitleBarTheme(bool dark)
    {
        if (IsHandleCreated)
        {
            Native.SetImmersiveDarkMode(Handle, dark);
        }
    }

    private void UpdateThemeMenuChecks()
    {
        _appearanceSystem.Checked = _themeMode == Theme.SystemMode;
        _appearanceLight.Checked = _themeMode == Theme.LightMode;
        _appearanceDark.Checked = _themeMode == Theme.DarkMode;
    }

    private void ApplyTrayTheme(bool dark)
    {
        ContextMenuStrip menu = _tray.ContextMenuStrip;
        if (menu == null)
        {
            return;
        }

        ApplyMenuTheme(menu, dark);
    }

    private void ApplyMenuTheme(ToolStrip menu, bool dark)
    {
        menu.Renderer = dark ? (ToolStripRenderer)_darkMenuRenderer : null;

        foreach (ToolStripItem item in menu.Items)
        {
            item.ForeColor = dark ? Theme.DarkText : SystemColors.ControlText;

            ToolStripMenuItem menuItem = item as ToolStripMenuItem;
            if (menuItem != null)
            {
                ApplyMenuTheme(menuItem.DropDown, dark);
            }
        }
    }

    private void SettingsChanged(object sender, EventArgs e)
    {
        if (_loadingSettings)
        {
            return;
        }

        AppSettings settings = new AppSettings();
        settings.UnlockedEnabled = _unlockedCheck.Checked;
        settings.UnlockedSeconds = (int)Math.Round(GetUnlockedSeconds());
        settings.LockedEnabled = _lockedCheck.Checked;
        settings.LockedSeconds = (int)Math.Round(GetLockedSeconds());
        settings.MinimizeToTray = _trayCheck.Checked;
        settings.AutoStartEnforcement = _autoStartCheck.Checked;
        settings.LockOnSleep = _lockOnSleepCheck.Checked;
        settings.ConfirmExit = _confirmExitCheck.Checked;
        settings.Theme = _themeMode;

        SettingsStore.Save(settings);
    }

    private static void SetTimeSpinners(NumericUpDown hours, NumericUpDown minutes, NumericUpDown seconds, int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;

        if (h > (int)hours.Maximum)
        {
            h = (int)hours.Maximum;
        }

        hours.Value = h;
        minutes.Value = m;
        seconds.Value = s;
    }

    private void StartButtonClick(object sender, EventArgs e)
    {
        ToggleEnforcement();
    }

    private void ForceButtonClick(object sender, EventArgs e)
    {
        ForceDisplayOff();
    }

    private void ForceDisplayOff()
    {
        Native.TurnOffMonitors();
        AppendLog("Display-off command sent.");
    }

    // Legacy sign-in startup was a per-user HKCU Run value. It is still read so
    // an upgrade from that version keeps the option on, and it is removed once
    // the scheduled task below takes over.
    private static bool IsLegacyRunEntryPresent()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                return key != null && key.GetValue(RunValueName) != null;
            }
        }
        catch
        {
            return false;
        }
    }

    private static void RemoveLegacyRunEntry()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null && key.GetValue(RunValueName) != null)
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }
        catch
        {
        }
    }

    private static bool IsStartupTaskRegistered()
    {
        string output;
        return RunSchTasks("/Query /TN \"" + TaskName + "\"", out output) == 0;
    }

    // The task is current when it exists and its action still points at this
    // executable, so a moved or reinstalled copy repairs itself.
    private static bool IsStartupTaskCurrent()
    {
        string output;
        if (RunSchTasks("/Query /TN \"" + TaskName + "\" /XML", out output) != 0 ||
            string.IsNullOrEmpty(output))
        {
            return false;
        }

        return output.IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsStartupEnabled()
    {
        return IsStartupTaskRegistered() || IsLegacyRunEntryPresent();
    }

    private static bool IsStartupRegistrationHealthy()
    {
        return IsStartupTaskCurrent();
    }

    private static string BuildTaskXml()
    {
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        string exe = SecurityElement.Escape(Application.ExecutablePath);
        string dir = SecurityElement.Escape(Path.GetDirectoryName(Application.ExecutablePath));

        // A per-user logon task that ignores new instances, starts even on
        // battery, has no execution time limit, and restarts up to three times
        // (one minute apart) if the app exits with a failure.
        return
            "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
            "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
            "<RegistrationInfo><Description>Starts Display Sleep Enforcer when you sign in.</Description></RegistrationInfo>" +
            "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + user + "</UserId>" +
            "<Delay>PT10S</Delay></LogonTrigger></Triggers>" +
            "<Principals><Principal id=\"Author\"><UserId>" + user + "</UserId>" +
            "<LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>" +
            "<Settings>" +
            "<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>" +
            "<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
            "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
            "<AllowHardTerminate>true</AllowHardTerminate>" +
            "<StartWhenAvailable>true</StartWhenAvailable>" +
            "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>" +
            "<IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>" +
            "<AllowStartOnDemand>true</AllowStartOnDemand>" +
            "<Enabled>true</Enabled><Hidden>false</Hidden>" +
            "<RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun>" +
            "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>" +
            "<Priority>7</Priority>" +
            "<RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure>" +
            "</Settings>" +
            "<Actions Context=\"Author\"><Exec>" +
            "<Command>" + exe + "</Command>" +
            "<WorkingDirectory>" + dir + "</WorkingDirectory>" +
            "</Exec></Actions>" +
            "</Task>";
    }

    // Creates (or refreshes) the per-user logon task. The XML is written as
    // UTF-16 because schtasks requires a Unicode task definition file.
    private static bool RegisterStartupTask()
    {
        string temp = Path.Combine(
            Path.GetTempPath(), "dse-task-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            File.WriteAllText(temp, BuildTaskXml(), Encoding.Unicode);
        }
        catch
        {
            return false;
        }

        try
        {
            string output;
            return RunSchTasks("/Create /TN \"" + TaskName + "\" /XML \"" + temp + "\" /F", out output) == 0;
        }
        finally
        {
            try
            {
                File.Delete(temp);
            }
            catch
            {
            }
        }
    }

    private static bool UnregisterStartupTask()
    {
        string output;
        RunSchTasks("/Delete /TN \"" + TaskName + "\" /F", out output);
        return !IsStartupTaskRegistered();
    }

    private static bool SetStartupEnabled(bool enabled)
    {
        RemoveLegacyRunEntry();
        return enabled ? RegisterStartupTask() : UnregisterStartupTask();
    }

    private static int RunSchTasks(string arguments, out string output)
    {
        output = null;
        try
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "schtasks.exe";
            info.Arguments = arguments;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;

            using (Process process = Process.Start(info))
            {
                if (process == null)
                {
                    return -1;
                }

                output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(15000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    return -1;
                }

                return process.ExitCode;
            }
        }
        catch
        {
            return -1;
        }
    }

    // Rewrites the scheduled task when it is missing or points at the wrong
    // executable, then reports whether the entry is correct.
    private static bool TryRepairStartupRegistration()
    {
        if (!IsStartupRegistrationHealthy())
        {
            RegisterStartupTask();
        }

        if (IsStartupRegistrationHealthy())
        {
            // Once the task is in place the old Run entry is redundant and would
            // launch a second copy at sign-in.
            RemoveLegacyRunEntry();
            return true;
        }

        return false;
    }

    private void StartupCheckChanged(object sender, EventArgs e)
    {
        bool enabled = _startupCheck.Checked;

        if (enabled)
        {
            if (SetStartupEnabled(true) && IsStartupRegistrationHealthy())
            {
                AppendLog("Will start automatically when you sign in.");
                return;
            }
        }
        else if (SetStartupEnabled(false))
        {
            AppendLog("Will no longer start automatically when you sign in.");
            return;
        }

        AppendLog("Could not update the start-on-sign-in setting.");
        MessageBox.Show(this,
            "Display Sleep Enforcer could not set up start-at-sign-in. Check " +
            "Task Scheduler, or try running the app as administrator.",
            "Display Sleep Enforcer",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);

        // Revert the checkbox without re-raising this handler.
        _startupCheck.CheckedChanged -= StartupCheckChanged;
        _startupCheck.Checked = IsStartupEnabled();
        _startupCheck.CheckedChanged += StartupCheckChanged;
    }

    private void MainFormShown(object sender, EventArgs e)
    {
        if (!_autoStarted)
        {
            _autoStarted = true;

            if (_autoStartCheck.Checked)
            {
                StartEnforcement(false);
            }
        }

        if (_startupVerified)
        {
            return;
        }

        _startupVerified = true;

        // Only guard the sign-in entry when the user has the option enabled.
        if (!_startupCheck.Checked)
        {
            return;
        }

        if (IsStartupRegistrationHealthy())
        {
            return;
        }

        if (TryRepairStartupRegistration())
        {
            AppendLog("Repaired the start-at-sign-in task so DSE runs at the next sign-in.");
            return;
        }

        AppendLog("Warning: start-at-sign-in could not be confirmed.");
        MessageBox.Show(this,
            "Start-at-sign-in is enabled, but Display Sleep Enforcer could not confirm " +
            "that it will run after you sign in. Check Task Scheduler, or try running " +
            "the app as administrator.",
            "Display Sleep Enforcer",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void LockOnSleepCheckChanged(object sender, EventArgs e)
    {
        if (_lockOnSleepCheck.Checked)
        {
            RegisterDisplayStateNotification();
        }
        else
        {
            UnregisterDisplayStateNotification();
            AppendLog("Will no longer lock the PC when the display turns off.");
        }
    }

    private void RegisterDisplayStateNotification()
    {
        if (_displayNotify != IntPtr.Zero)
        {
            return;
        }

        _displayNotify = Native.RegisterDisplayStateNotification(Handle);
        if (_displayNotify != IntPtr.Zero)
        {
            AppendLog("Will lock the PC when the display turns off (independent of Start/Stop; active while the app runs).");
            return;
        }

        AppendLog("Could not enable lock-on-display-off.");

        // Revert the checkbox without re-raising this handler.
        _lockOnSleepCheck.CheckedChanged -= LockOnSleepCheckChanged;
        _lockOnSleepCheck.Checked = false;
        _lockOnSleepCheck.CheckedChanged += LockOnSleepCheckChanged;
    }

    private void UnregisterDisplayStateNotification()
    {
        if (_displayNotify == IntPtr.Zero)
        {
            return;
        }

        Native.UnregisterNotification(_displayNotify);
        _displayNotify = IntPtr.Zero;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == (int)Native.WM_SETTINGCHANGE &&
            _themeMode == Theme.SystemMode &&
            Theme.Resolve(_themeMode) != _dark)
        {
            ApplyTheme();
        }

        if (m.Msg == (int)Native.WM_POWERBROADCAST &&
            m.WParam.ToInt64() == Native.PBT_POWERSETTINGCHANGE)
        {
            HandlePowerSettingChange(m.LParam);
        }

        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTitleBarTheme(_dark);
    }

    private void HandlePowerSettingChange(IntPtr lParam)
    {
        Native.POWERBROADCAST_SETTING setting =
            (Native.POWERBROADCAST_SETTING)Marshal.PtrToStructure(
                lParam, typeof(Native.POWERBROADCAST_SETTING));

        if (setting.PowerSetting != Native.ConsoleDisplayState || setting.Data != 0)
        {
            return;
        }

        if (!_lockOnSleepCheck.Checked)
        {
            return;
        }

        if (Native.Lock())
        {
            AppendLog("DSE locked the PC because the display turned off.");
        }
        else
        {
            AppendLog("DSE could not lock the PC after the display turned off.");
        }
    }

    private void ToggleEnforcement()
    {
        if (_enforcer.IsRunning)
        {
            _enforcer.Stop();
            SetUiRunning(false);
            return;
        }

        StartEnforcement(true);
    }

    private void StartEnforcement(bool interactive)
    {
        bool unlockedEnabled = _unlockedCheck.Checked;
        bool lockedEnabled = _lockedCheck.Checked;
        double unlockedSeconds = GetUnlockedSeconds();
        double lockedSeconds = GetLockedSeconds();

        if (!unlockedEnabled && !lockedEnabled)
        {
            ReportStartProblem(interactive, "Select at least one mode: enforce when unlocked and/or when locked.");
            return;
        }

        if (unlockedEnabled && unlockedSeconds < 1.0)
        {
            ReportStartProblem(interactive, "Please set the unlocked idle time to at least 1 second.");
            return;
        }

        if (lockedEnabled && lockedSeconds < 1.0)
        {
            ReportStartProblem(interactive, "Please set the locked idle time to at least 1 second.");
            return;
        }

        string modes = "unlocked=" +
            (unlockedEnabled ? unlockedSeconds + "s" : "off") +
            ", locked=" + (lockedEnabled ? lockedSeconds + "s" : "off") + ".";

        AppendLog(interactive ? "Start pressed." : "Started enforcing automatically.");
        _enforcer.Start(unlockedSeconds, unlockedEnabled,
            lockedSeconds, lockedEnabled, RecheckSeconds);
        SetUiRunning(true);
    }

    private void ReportStartProblem(bool interactive, string message)
    {
        if (interactive)
        {
            MessageBox.Show(this, message, "Display Sleep Enforcer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AppendLog("Automatic start skipped: " + message);
    }

    private double GetUnlockedSeconds()
    {
        return (double)(_unlockedHours.Value * 3600 + _unlockedMinutes.Value * 60 + _unlockedSeconds.Value);
    }

    private double GetLockedSeconds()
    {
        return (double)(_lockedHours.Value * 3600 + _lockedMinutes.Value * 60 + _lockedSeconds.Value);
    }

    private NumericUpDown CreateTimeSpinner(Point location, int maximum, int value, string tooltip)
    {
        NumericUpDown spinner = new NumericUpDown();
        spinner.Location = location;
        spinner.Size = new Size(44, 23);
        spinner.Minimum = 0;
        spinner.Maximum = maximum;
        spinner.Value = value;
        _toolTip.SetToolTip(spinner, tooltip);
        return spinner;
    }

    private void SetUiRunning(bool running)
    {
        _startButton.Text = running ? "Turn off" : "Turn on";
        _trayToggleItem.Text = running ? "Turn off" : "Turn on";
        _unlockedHours.Enabled = !running;
        _unlockedMinutes.Enabled = !running;
        _unlockedSeconds.Enabled = !running;
        _lockedHours.Enabled = !running;
        _lockedMinutes.Enabled = !running;
        _lockedSeconds.Enabled = !running;
        _unlockedCheck.Enabled = !running;
        _lockedCheck.Enabled = !running;

        if (!running)
        {
            _statusLabel.Text = "Stopped.";
        }
        else if (_unlockedCheck.Checked && _lockedCheck.Checked)
        {
            _statusLabel.Text = "Running - enforcing display sleep when unlocked and when locked.";
        }
        else if (_lockedCheck.Checked)
        {
            _statusLabel.Text = "Running - enforcing display sleep only while locked.";
        }
        else
        {
            _statusLabel.Text = "Running - enforcing display sleep while unlocked.";
        }
    }

    private void FormResize(object sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized && _trayCheck.Checked)
        {
            Hide();
            _tray.Visible = true;
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void FormClosingHandler(object sender, FormClosingEventArgs e)
    {
        if (!_closing &&
            _confirmExitCheck.Checked &&
            e.CloseReason != CloseReason.WindowsShutDown &&
            e.CloseReason != CloseReason.TaskManagerClosing)
        {
            IWin32Window owner = Visible ? (IWin32Window)this : null;
            DialogResult result = MessageBox.Show(
                owner,
                "Exit Display Sleep Enforcer?",
                "Display Sleep Enforcer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        _closing = true;
        UnregisterDisplayStateNotification();
        _enforcer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _toolTip.Dispose();
    }

    private void OnEnforcerMessage(string message)
    {
        if (_closing || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(new Action<string>(AppendLog), message);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void AppendLog(string message)
    {
        _logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            return null;
        }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        if (WantsRestorePower(args))
        {
            RunRestorePower();
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        bool createdNew;
        using (Mutex mutex = new Mutex(true, @"Local\DisplaySleepEnforcer.SingleInstance", out createdNew))
        {
            if (!createdNew)
            {
                MessageBox.Show(
                    "Display Sleep Enforcer is already running. Look for its icon in the notification area.",
                    "Display Sleep Enforcer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.Run(new MainForm());
        }
    }

    private static bool WantsRestorePower(string[] args)
    {
        if (args == null)
        {
            return false;
        }

        foreach (string arg in args)
        {
            if (string.Equals(arg, PowerRecovery.RestorePowerArgument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void RunRestorePower()
    {
        try
        {
            if (PowerRecovery.Exists())
            {
                string detail;
                PowerRecovery.TryRestore(out detail);
            }
        }
        catch
        {
        }

        PowerRecovery.ClearRunOnce();
    }
}
