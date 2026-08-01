using System.Management;
using System.Runtime.InteropServices;

namespace LeDormeur.Services;

/// <summary>
/// Controls Windows screen brightness (WMI for laptops, gamma as fallback).
/// </summary>
public sealed class BrightnessService : IDisposable
{
    private readonly bool _useWmi;
    private byte _startBrightness = 100;
    private bool _disposed;

    public bool UsesWmi => _useWmi;
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
    /// </summary>
    public void CaptureStartLevel()
    {
        if (_useWmi && TryGetWmiBrightness(out var current))
        {
            _startBrightness = current;
            CurrentBrightness = current;
        }
        else
        {
            _startBrightness = 100;
            CurrentBrightness = 100;
            ResetGamma();
        }
    }

    /// <summary>
    /// Applies a brightness percentage (0–100) relative to the maximum.
    /// </summary>
    public void SetBrightnessPercent(byte percent)
    {
        percent = Math.Clamp(percent, (byte)0, (byte)100);
        CurrentBrightness = percent;

        if (_useWmi)
        {
            TrySetWmiBrightness(percent);
        }
        else
        {
            ApplyGamma(percent / 100f);
        }
    }

    /// <summary>
    /// Restores the starting brightness (or default gamma).
    /// </summary>
    public void RestoreStartLevel()
    {
        if (_useWmi)
        {
            TrySetWmiBrightness(_startBrightness);
            CurrentBrightness = _startBrightness;
        }
        else
        {
            ResetGamma();
            CurrentBrightness = 100;
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
        catch {
            // WMI not supported (often external monitors / desktops)
        }

        return false;
    }

    #region Gamma fallback (dims the screen when WMI is unavailable)

    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

    [DllImport("gdi32.dll")]
    private static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct RAMP
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    private void ApplyGamma(float factor)
    {
        factor = Math.Clamp(factor, 0.05f, 1f);
        var ramp = new RAMP
        {
            Red = new ushort[256],
            Green = new ushort[256],
            Blue = new ushort[256]
        };

        for (int i = 0; i < 256; i++)
        {
            var value = (ushort)Math.Clamp((int)(i * 256 * factor), 0, 65535);
            ramp.Red[i] = value;
            ramp.Green[i] = value;
            ramp.Blue[i] = value;
        }

        IntPtr hdc = GetDC(IntPtr.Zero);
        try
        {
            SetDeviceGammaRamp(hdc, ref ramp);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    private void ResetGamma()
    {
        ApplyGamma(1f);
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
