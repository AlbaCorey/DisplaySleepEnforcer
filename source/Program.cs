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
using System.Threading;

internal static class Program
{
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
