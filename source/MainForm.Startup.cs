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
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed partial class MainForm
{
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

    // The task is current when it exists, its action still points at this
    // executable (so a moved or reinstalled copy repairs itself), and it starts
    // the app minimized as the current version does.
    private static bool IsStartupTaskCurrent()
    {
        string output;
        if (RunSchTasks("/Query /TN \"" + TaskName + "\" /XML", out output) != 0 ||
            string.IsNullOrEmpty(output))
        {
            return false;
        }

        if (output.IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return output.IndexOf(StartMinimizedArgument, StringComparison.OrdinalIgnoreCase) >= 0;
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
        // (one minute apart) if the app exits with a failure. It launches the
        // app with --minimized so signing in does not pop up a window.
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
            "<Arguments>" + StartMinimizedArgument + "</Arguments>" +
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

                // Offer to also begin enforcing as soon as the app opens, since
                // that is usually what someone wants when it starts with Windows.
                if (!_autoStartCheck.Checked)
                {
                    OfferAutoStartEnforcement();
                }

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

    // Asks whether enforcement should also begin immediately on start. Called
    // when the user turns on start-at-sign-in, since enforcement starting at
    // sign-in is the usual companion choice. Checking the box saves the setting
    // through the normal SettingsChanged path.
    private void OfferAutoStartEnforcement()
    {
        DialogResult answer = MessageBox.Show(this,
            "Also enable \"Enable enforcement immediately on start\", so " +
            "Display Sleep Enforcer begins enforcing as soon as it opens " +
            "(including when it starts with Windows)?",
            "Display Sleep Enforcer",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        _autoStartCheck.Checked = true;
        AppendLog("Will also start enforcing immediately on start.");
    }

    private void MainFormShown(object sender, EventArgs e)
    {
        // Started with Windows: don't pop a window up at sign-in. Setting the
        // state now that the handle exists triggers FormResize, which hides the
        // window to the tray when minimize-to-tray is on; otherwise it just
        // shows minimized in the taskbar.
        if (_startMinimized)
        {
            _startMinimized = false;
            WindowState = FormWindowState.Minimized;
        }

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
}
