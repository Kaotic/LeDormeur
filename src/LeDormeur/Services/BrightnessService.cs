using System.Management;
using System.Runtime.InteropServices;

namespace LeDormeur.Services;

/// <summary>
/// Controls Windows screen brightness (WMI for laptops, gamma as fallback).
/// </summary>
public sealed class BrightnessService : IDisposable
{
    private const float MinGammaFactor = 0.05f;
    private const int RampEntries = 256;
    private const int RampLength = RampEntries * 3;

    /// <summary>DISPLAY_DEVICE_ATTACHED_TO_DESKTOP</summary>
    private const uint DisplayAttachedToDesktop = 0x00000001;

    private readonly bool _useWmi;
    private bool? _gammaSupported;
    private byte _startBrightness = 100;
    private float _lastGammaFactor = float.NaN;
    private float _lastAppliedPercent = 100f;
    /// <summary>
    /// Lowest brightness % successfully applied this session (driver floor).
    /// Once set, darker requests are clamped instead of treated as hard failures.
    /// </summary>
    private float? _minAchievablePercent;
    private bool _disposed;

    public bool UsesWmi => _useWmi;
    public bool UsesGamma => !_useWmi;

    /// <summary>
    /// After a probe: whether software gamma can actually change the display.
    /// Null until the first gamma attempt.
    /// </summary>
    public bool? GammaSupported => _gammaSupported;

    public byte StartBrightness => _startBrightness;
    public byte CurrentBrightness { get; private set; } = 100;

    public BrightnessService()
    {
        _useWmi = TryGetWmiBrightness(out var current);
        if (_useWmi)
        {
            _startBrightness = current;
            CurrentBrightness = current;
        }
        else
        {
            _startBrightness = 100;
            CurrentBrightness = 100;
        }
    }

    /// <summary>
    /// Captures the starting brightness level before a timer run.
    /// For gamma mode, probes the OS/driver and returns false if dimming is unavailable.
    /// </summary>
    public bool CaptureStartLevel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _minAchievablePercent = null;
        _lastAppliedPercent = 100f;
        _lastGammaFactor = float.NaN;

        if (_useWmi)
        {
            if (TryGetWmiBrightness(out var current))
            {
                _startBrightness = current;
                CurrentBrightness = current;
                _lastAppliedPercent = current;
                return true;
            }

            // WMI was available at construction but not now — try gamma below.
        }

        _startBrightness = 100;
        CurrentBrightness = 100;
        _lastAppliedPercent = 100f;

        if (!EnsureGammaSupported())
            return false;

        return TrySetGammaFactor(1f, verify: false, updateCurrent: true, brightnessPercent: 100);
    }

    /// <summary>
    /// Applies a brightness percentage (0–100).
    /// Returns false only when dimming is completely unavailable (no successful level this session).
    /// If the driver rejects a darker level after some dimming already worked, holds the floor and returns true.
    /// </summary>
    public bool SetBrightnessPercent(float percent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        percent = Math.Clamp(percent, 0f, 100f);
        var rounded = (byte)Math.Clamp((int)Math.Round(percent), 0, 100);

        // Driver already refused darker levels — stay at the last good brightness.
        if (_minAchievablePercent.HasValue && percent < _minAchievablePercent.Value - 0.001f)
        {
            CurrentBrightness = (byte)Math.Clamp((int)Math.Round(_minAchievablePercent.Value), 0, 100);
            return true;
        }

        if (_useWmi)
        {
            if (TrySetWmiBrightness(rounded))
            {
                CurrentBrightness = rounded;
                _lastAppliedPercent = rounded;
                return true;
            }

            return HoldFloorOrFail();
        }

        if (TrySetGammaFactor(
                percent / 100f,
                verify: false,
                updateCurrent: true,
                brightnessPercent: rounded))
        {
            _lastAppliedPercent = CurrentBrightness;
            return true;
        }

        return HoldFloorOrFail();
    }

    /// <summary>
    /// After a rejected darker set: if we already dimmed successfully, lock that floor; otherwise fail.
    /// </summary>
    private bool HoldFloorOrFail()
    {
        // Consider "partial success" when we applied something darker than the session start.
        if (_lastAppliedPercent < _startBrightness - 0.5f)
        {
            _minAchievablePercent = _lastAppliedPercent;
            CurrentBrightness = (byte)Math.Clamp((int)Math.Round(_lastAppliedPercent), 0, 100);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Restores the starting brightness (or default gamma).
    /// </summary>
    public bool RestoreStartLevel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_useWmi)
        {
            if (!TrySetWmiBrightness(_startBrightness))
                return false;

            CurrentBrightness = _startBrightness;
            return true;
        }

        return TrySetGammaFactor(1f, verify: false, updateCurrent: true, brightnessPercent: 100);
    }

    private bool EnsureGammaSupported()
    {
        if (_gammaSupported.HasValue)
            return _gammaSupported.Value;

        // Brief probe: dim slightly, confirm the ramp actually changed, restore identity.
        const float probeFactor = 0.85f;
        var ok = TrySetGammaFactor(probeFactor, verify: true, updateCurrent: false, brightnessPercent: null);
        TrySetGammaFactor(1f, verify: false, updateCurrent: false, brightnessPercent: null);

        _gammaSupported = ok;
        return ok;
    }

    private bool TrySetGammaFactor(float factor, bool verify, bool updateCurrent, byte? brightnessPercent)
    {
        factor = Math.Clamp(factor, MinGammaFactor, 1f);

        // Avoid redundant OS calls when the factor did not meaningfully change.
        if (!verify
            && !float.IsNaN(_lastGammaFactor)
            && Math.Abs(factor - _lastGammaFactor) < 0.0005f)
        {
            if (updateCurrent && brightnessPercent.HasValue)
                CurrentBrightness = brightnessPercent.Value;
            return _gammaSupported != false;
        }

        var ramp = BuildRamp(factor);
        if (!SetRampOnDisplays(ramp))
        {
            if (verify)
                _gammaSupported = false;
            return false;
        }

        if (verify && factor < 0.999f && !VerifyRampDimmed())
        {
            _gammaSupported = false;
            return false;
        }

        _lastGammaFactor = factor;
        if (updateCurrent)
        {
            CurrentBrightness = brightnessPercent
                ?? (byte)Math.Clamp((int)Math.Round(factor * 100f), 0, 100);
            _lastAppliedPercent = CurrentBrightness;
        }

        return true;
    }

    private static ushort[] BuildRamp(float factor)
    {
        var data = new ushort[RampLength];
        for (var i = 0; i < RampEntries; i++)
        {
            // Identity ramp is i << 8 (i * 256). Scale by factor for dimming.
            var value = (ushort)Math.Clamp((int)Math.Round(i * 256.0 * factor), 0, 65535);
            data[i] = value;
            data[RampEntries + i] = value;
            data[RampEntries * 2 + i] = value;
        }

        return data;
    }

    private static bool SetRampOnDisplays(ushort[] ramp)
    {
        var handle = GCHandle.Alloc(ramp, GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            var anySuccess = false;

            // Primary / virtual-screen DC
            var screenDc = GetDC(IntPtr.Zero);
            if (screenDc != IntPtr.Zero)
            {
                try
                {
                    if (SetDeviceGammaRamp(screenDc, ptr))
                        anySuccess = true;
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
            }

            // Per-device DCs (multi-monitor)
            var device = new DisplayDevice
            {
                cb = Marshal.SizeOf<DisplayDevice>()
            };

            for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
            {
                if ((device.StateFlags & DisplayAttachedToDesktop) == 0)
                    continue;

                var hdc = CreateDC(device.DeviceName, device.DeviceName, null, IntPtr.Zero);
                if (hdc == IntPtr.Zero)
                    hdc = CreateDC("DISPLAY", device.DeviceName, null, IntPtr.Zero);
                if (hdc == IntPtr.Zero)
                    continue;

                try
                {
                    if (SetDeviceGammaRamp(hdc, ptr))
                        anySuccess = true;
                }
                finally
                {
                    DeleteDC(hdc);
                }
            }

            return anySuccess;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Detects the "returns TRUE but does nothing" case (common with HDR / some drivers).
    /// Checks that mid-ramp samples are clearly below the identity curve after a dim probe.
    /// </summary>
    private static bool VerifyRampDimmed()
    {
        var buffer = new ushort[RampLength];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero)
                return true; // cannot verify — trust SetDeviceGammaRamp

            try
            {
                if (!GetDeviceGammaRamp(hdc, handle.AddrOfPinnedObject()))
                    return true;

                // Identity midpoints: index i → i * 256
                int[] samples = [64, 128, 192];
                var dimmedSamples = 0;
                foreach (var i in samples)
                {
                    var identity = i * 256;
                    var actual = buffer[i];
                    // Require at least ~5% dimming vs identity (probe uses 0.85).
                    if (actual < identity - identity / 20)
                        dimmedSamples++;
                }

                return dimmedSamples >= 2;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }
        finally
        {
            handle.Free();
        }
    }

    private static bool TryGetWmiBrightness(out byte brightness)
    {
        brightness = 100;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject obj in searcher.Get())
            {
                brightness = Convert.ToByte(obj["CurrentBrightness"]);
                return true;
            }
        }
        catch
        {
            // WMI not supported (often external monitors / desktops)
        }

        return false;
    }

    private static bool TrySetWmiBrightness(byte brightness)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.InvokeMethod("WmiSetBrightness", new object[] { 1, brightness });
                return true;
            }
        }
        catch
        {
            // WMI not supported (often external monitors / desktops)
        }

        return false;
    }

    #region Native interop

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDeviceGammaRamp(IntPtr hDC, IntPtr lpRamp);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDeviceGammaRamp(IntPtr hDC, IntPtr lpRamp);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDC(string pwszDriver, string? pwszDevice, string? pszPort, IntPtr pdm);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DisplayDevice lpDisplayDevice,
        uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!_useWmi)
        {
            try
            {
                // Best-effort restore so a closed app does not leave a dim ramp.
                TrySetGammaFactor(1f, verify: false, updateCurrent: false, brightnessPercent: null);
            }
            catch
            {
                // Ignore restore failures during teardown.
            }
        }
    }
}
