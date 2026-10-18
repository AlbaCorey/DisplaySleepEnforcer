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
