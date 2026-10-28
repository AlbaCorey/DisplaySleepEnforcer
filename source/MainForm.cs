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
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed partial class MainForm : Form
{
    private const string AppVersion = "1.0.5";
    private const double RecheckSeconds = 2.0;
    private const string TaskName = "DisplaySleepEnforcer";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "DisplaySleepEnforcer";

    // Passed by the sign-in task (and the installer's Run seed) so the app
    // opens minimized instead of popping up a window at logon.
    internal const string StartMinimizedArgument = "--minimized";

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
    private bool _startMinimized;
    private int _themeMode = Theme.SystemMode;
    private bool _dark;
    private readonly DarkMenuRenderer _darkMenuRenderer = new DarkMenuRenderer();
    private readonly ToolStripMenuItem _appearanceSystem;
    private readonly ToolStripMenuItem _appearanceLight;
    private readonly ToolStripMenuItem _appearanceDark;
    private readonly ToolStripMenuItem _trayToggleItem;
    private IntPtr _displayNotify = IntPtr.Zero;

    public MainForm(bool startMinimized)
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
            "Turn displays off after the PC has been idle this long while it is unlocked, " +
            "overriding display power requests.");

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

        _startMinimized = startMinimized;
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
            return;
        }

        // The record could not be restored (unreadable, or the recorded scheme is
        // gone). Drop it so it cannot keep blocking recovery records from being
        // captured for new enforcement runs.
        PowerRecovery.Clear();
        AppendLog("Could not restore the power settings from a previous run; the stale recovery record was discarded.");
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
}
