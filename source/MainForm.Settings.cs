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

internal sealed partial class MainForm
{
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
}
