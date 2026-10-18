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

using Microsoft.Win32;

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
