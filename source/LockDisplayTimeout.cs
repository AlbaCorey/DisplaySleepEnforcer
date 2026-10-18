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
