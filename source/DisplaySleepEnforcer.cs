/*
 * Display Sleep Enforcer - force display sleep, including while locked.
 * Copyright (C) 2026 ferdef150
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
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Native
{
    public const uint WM_SYSCOMMAND = 0x0112;
    public const uint SMTO_ABORTIFHUNG = 0x0002;
    private const uint DESKTOP_READOBJECTS = 0x0001;
    private const int UOI_NAME = 2;

    public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
    public static readonly IntPtr SC_MONITORPOWER = new IntPtr(0xF170);
    public static readonly IntPtr MONITOR_OFF = new IntPtr(2);

    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
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

    private volatile bool _running;
    private Thread _thread;
    private double _idleSeconds = 60.0;
    private double _enforceSeconds = 2.0;
    private bool _lockedOnly;
    private bool _wasLocked;
    private readonly LockDisplayTimeout _lockTimeout = new LockDisplayTimeout();
    private readonly DisplayRequestPolicy _requestPolicy = new DisplayRequestPolicy();
    private readonly LockWatcher _lockWatch = new LockWatcher();

    public event Action<string> Message;

    public bool IsRunning
    {
        get { return _running; }
    }

    public void Start(double idleSeconds, double enforceSeconds, bool lockedOnly)
    {
        if (_running)
        {
            return;
        }

        _idleSeconds = idleSeconds;
        _enforceSeconds = enforceSeconds;
        _lockedOnly = lockedOnly;
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
        uint threshold = (uint)(_idleSeconds * 1000.0);
        int delay = (int)(_enforceSeconds * 1000.0);
        bool forced = false;
        int sinceReassert = 0;

        if (_lockedOnly)
        {
            uint lockedSeconds = (uint)Math.Round(_idleSeconds);
            if (_lockTimeout.Apply(lockedSeconds))
            {
                Raise("Enforcement active (locked-only): the display will turn off " +
                    lockedSeconds + "s after the PC is locked.");
            }
            else
            {
                Raise("Warning: could not set the lock-screen display timeout; " +
                    "enforcement while locked may rely on the Windows default.");
            }
        }
        else
        {
            Raise("Enforcement active - displays off after " + _idleSeconds + "s idle.");
        }

        try
        {
            while (_running)
            {
                uint idle = Native.GetIdleMilliseconds();

                if (_lockedOnly)
                {
                    _lockWatch.Sample();
                    bool lockedNow = _lockWatch.Locked;
                    if (lockedNow != _wasLocked)
                    {
                        _wasLocked = lockedNow;
                        Raise(lockedNow ? "PC locked." : "PC unlocked.");

                        if (lockedNow)
                        {
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

                    if (_wasLocked)
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
                }

                bool enforceNow = !_lockedOnly && idle >= threshold;
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
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "DisplaySleepEnforcer";

    private readonly NumericUpDown _idleHours;
    private readonly NumericUpDown _idleMinutes;
    private readonly NumericUpDown _idleSeconds;
    private readonly NumericUpDown _enforceNum;
    private readonly Button _startButton;
    private readonly Button _forceButton;
    private readonly CheckBox _trayCheck;
    private readonly CheckBox _lockedOnlyCheck;
    private readonly CheckBox _startupCheck;
    private readonly Label _statusLabel;
    private readonly TextBox _logBox;
    private readonly NotifyIcon _tray;
    private readonly Enforcer _enforcer = new Enforcer();
    private readonly Icon _appIcon;
    private bool _closing;

    public MainForm()
    {
        Text = "Display Sleep Enforcer";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 420);
        Font = new Font("Segoe UI", 9F);

        _appIcon = LoadAppIcon();
        if (_appIcon != null)
        {
            Icon = _appIcon;
        }

        Label idleLabel = new Label();
        idleLabel.Text = "Turn displays off after (hh:mm:ss) idle:";
        idleLabel.Location = new Point(12, 16);
        idleLabel.AutoSize = true;

        _idleHours = CreateTimeSpinner(new Point(244, 12), 99, 0);
        _idleMinutes = CreateTimeSpinner(new Point(304, 12), 59, 1);
        _idleSeconds = CreateTimeSpinner(new Point(364, 12), 59, 0);

        Label idleColon1 = new Label();
        idleColon1.Text = ":";
        idleColon1.Location = new Point(291, 16);
        idleColon1.AutoSize = true;

        Label idleColon2 = new Label();
        idleColon2.Text = ":";
        idleColon2.Location = new Point(351, 16);
        idleColon2.AutoSize = true;

        Label enforceLabel = new Label();
        enforceLabel.Text = "Re-check interval (seconds):";
        enforceLabel.Location = new Point(12, 52);
        enforceLabel.AutoSize = true;

        _enforceNum = new NumericUpDown();
        _enforceNum.Location = new Point(298, 48);
        _enforceNum.Size = new Size(110, 23);
        _enforceNum.DecimalPlaces = 1;
        _enforceNum.Minimum = 0.2m;
        _enforceNum.Maximum = 60m;
        _enforceNum.Increment = 0.5m;
        _enforceNum.Value = 2m;

        _trayCheck = new CheckBox();
        _trayCheck.Text = "Minimize to tray";
        _trayCheck.Location = new Point(12, 88);
        _trayCheck.AutoSize = true;
        _trayCheck.Checked = true;

        _lockedOnlyCheck = new CheckBox();
        _lockedOnlyCheck.Text = "Only enforce display sleep while the PC is locked";
        _lockedOnlyCheck.Location = new Point(12, 112);
        _lockedOnlyCheck.AutoSize = true;

        _startupCheck = new CheckBox();
        _startupCheck.Text = "Start automatically when I sign in";
        _startupCheck.Location = new Point(12, 136);
        _startupCheck.AutoSize = true;
        _startupCheck.Checked = IsRunAtStartupEnabled();
        _startupCheck.CheckedChanged += StartupCheckChanged;

        _startButton = new Button();
        _startButton.Text = "Start";
        _startButton.Location = new Point(12, 168);
        _startButton.Size = new Size(120, 32);
        _startButton.Click += StartButtonClick;

        _forceButton = new Button();
        _forceButton.Text = "Force off now";
        _forceButton.Location = new Point(140, 168);
        _forceButton.Size = new Size(120, 32);
        _forceButton.Click += ForceButtonClick;

        _statusLabel = new Label();
        _statusLabel.Text = "Stopped.";
        _statusLabel.Location = new Point(12, 214);
        _statusLabel.AutoSize = true;

        _logBox = new TextBox();
        _logBox.Location = new Point(12, 240);
        _logBox.Size = new Size(396, 168);
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.BackColor = Color.White;
        _logBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

        Controls.Add(idleLabel);
        Controls.Add(_idleHours);
        Controls.Add(idleColon1);
        Controls.Add(_idleMinutes);
        Controls.Add(idleColon2);
        Controls.Add(_idleSeconds);
        Controls.Add(enforceLabel);
        Controls.Add(_enforceNum);
        Controls.Add(_trayCheck);
        Controls.Add(_lockedOnlyCheck);
        Controls.Add(_startupCheck);
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
        menu.Items.Add("Start / Stop", null, delegate { ToggleEnforcement(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { Close(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += delegate { RestoreFromTray(); };

        Resize += FormResize;
        FormClosing += FormClosingHandler;
    }

    private void StartButtonClick(object sender, EventArgs e)
    {
        ToggleEnforcement();
    }

    private void ForceButtonClick(object sender, EventArgs e)
    {
        Native.TurnOffMonitors();
        AppendLog("Display-off command sent.");
    }

    private static bool IsRunAtStartupEnabled()
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

    private static bool SetRunAtStartup(bool enabled)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key == null)
                {
                    return false;
                }

                if (enabled)
                {
                    key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
                }
                else if (key.GetValue(RunValueName) != null)
                {
                    key.DeleteValue(RunValueName, false);
                }

                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private void StartupCheckChanged(object sender, EventArgs e)
    {
        bool enabled = _startupCheck.Checked;
        if (SetRunAtStartup(enabled))
        {
            AppendLog(enabled
                ? "Will start automatically when you sign in."
                : "Will no longer start automatically when you sign in.");
            return;
        }

        AppendLog("Could not update the start-on-sign-in setting.");

        // Revert the checkbox without re-raising this handler.
        _startupCheck.CheckedChanged -= StartupCheckChanged;
        _startupCheck.Checked = IsRunAtStartupEnabled();
        _startupCheck.CheckedChanged += StartupCheckChanged;
    }

    private void ToggleEnforcement()
    {
        if (_enforcer.IsRunning)
        {
            _enforcer.Stop();
            SetUiRunning(false);
        }
        else
        {
            double idleSeconds = GetIdleSeconds();
            if (idleSeconds < 1.0)
            {
                MessageBox.Show(this,
                    "Please set the idle time to at least 1 second.",
                    "Display Sleep Enforcer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _enforcer.Start(idleSeconds, (double)_enforceNum.Value, _lockedOnlyCheck.Checked);
            SetUiRunning(true);
        }
    }

    private double GetIdleSeconds()
    {
        return (double)(_idleHours.Value * 3600 + _idleMinutes.Value * 60 + _idleSeconds.Value);
    }

    private static NumericUpDown CreateTimeSpinner(Point location, int maximum, int value)
    {
        NumericUpDown spinner = new NumericUpDown();
        spinner.Location = location;
        spinner.Size = new Size(44, 23);
        spinner.Minimum = 0;
        spinner.Maximum = maximum;
        spinner.Value = value;
        return spinner;
    }

    private void SetUiRunning(bool running)
    {
        _startButton.Text = running ? "Stop" : "Start";
        _idleHours.Enabled = !running;
        _idleMinutes.Enabled = !running;
        _idleSeconds.Enabled = !running;
        _enforceNum.Enabled = !running;
        _lockedOnlyCheck.Enabled = !running;
        if (!running)
        {
            _statusLabel.Text = "Stopped.";
        }
        else if (_lockedOnlyCheck.Checked)
        {
            _statusLabel.Text = "Running - enforcing display sleep only while locked.";
        }
        else
        {
            _statusLabel.Text = "Running - enforcing display sleep when idle.";
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
        _closing = true;
        _enforcer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
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
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
