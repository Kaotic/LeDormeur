using System.Runtime.InteropServices;

namespace LeDormeur.Services;

/// <summary>
/// Reads and writes Windows power-plan settings for the active scheme
/// (equivalent to <c>powercfg /setacvalueindex SCHEME_CURRENT ...</c>).
/// </summary>
public static class PowerSchemeService
{
    /// <summary>SUB_SLEEP — Sleep subgroup.</summary>
    private static readonly Guid SleepSubgroup = new("238C9FA8-0AAD-41ED-83F4-97BE242C8F20");

    /// <summary>RTCWAKE — Allow wake timers.</summary>
    private static readonly Guid RtcWake = new("BD3B718A-0680-4D9D-8AB2-E1D2B4AC806D");

    private const uint ErrorSuccess = 0;
    private const uint ErrorAccessDenied = 5;

    public sealed record WakeTimersInfo(
        bool AcAllowed,
        bool DcAllowed,
        string SchemeName);

    /// <summary>
    /// Current AC (plugged-in) value is the one shown in the UI.
    /// Values: 0 = disable, 1 = enable, 2 = important timers only (treated as allowed).
    /// </summary>
    public static bool TryGetWakeTimers(out WakeTimersInfo info)
    {
        info = new WakeTimersInfo(false, false, string.Empty);

        var error = PowerGetActiveScheme(IntPtr.Zero, out var guidPtr);
        if (error != ErrorSuccess || guidPtr == IntPtr.Zero)
            return false;

        try
        {
            var scheme = Marshal.PtrToStructure<Guid>(guidPtr);
            var sleep = SleepSubgroup;
            var rtc = RtcWake;

            error = PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sleep, ref rtc, out var ac);
            if (error != ErrorSuccess)
                return false;

            var dcAllowed = ac != 0;
            error = PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sleep, ref rtc, out var dc);
            if (error == ErrorSuccess)
                dcAllowed = dc != 0;

            info = new WakeTimersInfo(
                AcAllowed: ac != 0,
                DcAllowed: dcAllowed,
                SchemeName: ReadFriendlyName(ref scheme) ?? string.Empty);
            return true;
        }
        finally
        {
            LocalFree(guidPtr);
        }
    }

    /// <summary>
    /// Sets RTCWAKE on AC and DC for the active scheme, then re-activates it.
    /// <paramref name="allowed"/> true → 1 (enable), false → 0 (disable).
    /// </summary>
    public static bool TrySetWakeTimers(bool allowed, out bool accessDenied)
    {
        accessDenied = false;
        uint value = allowed ? 1u : 0u;

        var error = PowerGetActiveScheme(IntPtr.Zero, out var guidPtr);
        if (error != ErrorSuccess || guidPtr == IntPtr.Zero)
            return TrySetViaPowerCfg(value, out accessDenied);

        try
        {
            var scheme = Marshal.PtrToStructure<Guid>(guidPtr);
            var sleep = SleepSubgroup;
            var rtc = RtcWake;

            error = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sleep, ref rtc, value);
            if (error != ErrorSuccess)
            {
                accessDenied = error == ErrorAccessDenied;
                return TrySetViaPowerCfg(value, out accessDenied);
            }

            // Desktops without a battery may reject DC; AC is the important one.
            _ = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sleep, ref rtc, value);

            error = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
            if (error != ErrorSuccess)
            {
                accessDenied = error == ErrorAccessDenied;
                return TrySetViaPowerCfg(value, out accessDenied);
            }

            return true;
        }
        finally
        {
            LocalFree(guidPtr);
        }
    }

    private static string? ReadFriendlyName(ref Guid scheme)
    {
        uint size = 0;
        PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref size);
        if (size == 0)
            return null;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var error = PowerReadFriendlyName(
                IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, buffer, ref size);
            if (error != ErrorSuccess)
                return null;

            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Fallback matching the documented CLI:
    /// <c>powercfg /setacvalueindex SCHEME_CURRENT SUB_SLEEP RTCWAKE n</c>
    /// </summary>
    private static bool TrySetViaPowerCfg(uint value, out bool accessDenied)
    {
        accessDenied = false;

        if (!PowerCfg.TryRun($"/setacvalueindex SCHEME_CURRENT SUB_SLEEP RTCWAKE {value}", out var exitCode, out _))
        {
            accessDenied = exitCode == 1;
            return false;
        }

        PowerCfg.TryRun($"/setdcvalueindex SCHEME_CURRENT SUB_SLEEP RTCWAKE {value}", out _, out _);

        if (!PowerCfg.TryRun("/setactive SCHEME_CURRENT", out exitCode, out _))
        {
            accessDenied = exitCode == 1;
            return false;
        }

        return true;
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        out uint AcValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        out uint DcValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        uint AcValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        ref Guid SubGroupOfPowerSettingsGuid,
        ref Guid PowerSettingGuid,
        uint DcValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadFriendlyName(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        IntPtr SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid,
        IntPtr Buffer,
        ref uint BufferSize);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
