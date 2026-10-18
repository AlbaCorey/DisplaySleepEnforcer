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
using System.Threading;

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
