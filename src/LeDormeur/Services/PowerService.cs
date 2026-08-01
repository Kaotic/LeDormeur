using System.Runtime.InteropServices;

namespace LeDormeur.Services;

/// <summary>
/// Puts the Windows PC to sleep.
/// </summary>
public static class PowerService
{
    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    /// <summary>
    /// Forces sleep (not hibernation).
    /// </summary>
    public static bool ForceSleep()
    {
        // hibernate=false → sleep (S3)
        // forceCritical=true → force even if apps try to block
        // disableWakeEvent=false → allow normal wake
        return SetSuspendState(hibernate: false, forceCritical: true, disableWakeEvent: false);
    }
}
