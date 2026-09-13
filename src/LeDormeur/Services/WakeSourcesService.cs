using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Management;
using Microsoft.Win32;

namespace LeDormeur.Services;

/// <summary>
/// Wake sources: devices that can wake the PC, last wake reason, Wake-on-LAN.
/// </summary>
public static class WakeSourcesService
{
    private const string NicClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    public sealed record WakeDevice(string Name, bool Armed);

    public sealed record LastWakeInfo(bool Found, string? Source, DateTime? WakeTime);

    public sealed record WakeOnLanInfo(bool Supported, bool Enabled);

    public static IReadOnlyList<WakeDevice> GetWakeDevices()
    {
        var programmable = PowerCfg.QueryLines("/devicequery wake_programmable");
        var armed = new HashSet<string>(
            PowerCfg.QueryLines("/devicequery wake_armed"),
            StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> names = programmable.Count > 0 ? programmable : armed;
        return names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(SortKey)
            .ThenBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .Select(n => new WakeDevice(n, armed.Contains(n)))
            .ToList();
    }

    public static bool TrySetDeviceWake(string name, bool armed, out bool accessDenied)
    {
        accessDenied = false;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var flag = armed ? "/deviceenablewake" : "/devicedisablewake";
        var ok = PowerCfg.TryRun($"{flag} \"{name.Replace("\"", "")}\"", out var exitCode, out _);
        if (!ok)
            accessDenied = exitCode == 1;
        return ok;
    }

    public static LastWakeInfo GetLastWake()
    {
        if (TryGetLastWakeFromEventLog(out var info))
            return info;

        return GetLastWakeFromPowerCfg();
    }

    public static WakeOnLanInfo GetWakeOnLan()
    {
        var nics = GetEthernetWakeTargets();
        if (nics.Count == 0)
            return new WakeOnLanInfo(Supported: false, Enabled: false);

        var enabled = nics.Any(n => n.MagicPacketEnabled || n.DeviceArmed);
        return new WakeOnLanInfo(Supported: true, Enabled: enabled);
    }

    public static bool TrySetWakeOnLan(bool enabled, out bool accessDenied)
    {
        accessDenied = false;
        var nics = GetEthernetWakeTargets();
        if (nics.Count == 0)
            return false;

        var ok = true;
        foreach (var nic in nics)
        {
            if (!TryWriteMagicPacket(nic.RegistrySubKey, enabled, out var denied))
            {
                if (denied)
                    accessDenied = true;
                ok = false;
            }

            if (!string.IsNullOrEmpty(nic.DeviceName)
                && !TrySetDeviceWake(nic.DeviceName, enabled, out denied))
            {
                if (denied)
                    accessDenied = true;
                ok = false;
            }
        }

        return ok;
    }

    private static int SortKey(string name)
    {
        var n = name.ToLowerInvariant();
        if (LooksLikeNetwork(n))
            return 0;
        if (n.Contains("keyboard") || n.Contains("clavier"))
            return 1;
        if (n.Contains("mouse") || n.Contains("souris"))
            return 2;
        return 3;
    }

    private static bool LooksLikeNetwork(string n)
    {
        if (IsVirtualAdapter(n))
            return false;
        return n.Contains("ethernet")
               || n.Contains("gigabit")
               || n.Contains("wifi")
               || n.Contains("wi-fi")
               || n.Contains("wireless")
               || n.Contains("réseau")
               || n.Contains("reseau");
    }

    private static bool IsVirtualAdapter(string n)
    {
        return n.Contains("virtual")
               || n.Contains("hyper-v")
               || n.Contains("vethernet")
               || n.Contains("vpn")
               || n.Contains("bluetooth")
               || n.Contains("wi-fi direct")
               || n.Contains("wifi direct");
    }

    private static bool IsEthernetName(string n)
    {
        n = n.ToLowerInvariant();
        if (IsVirtualAdapter(n))
            return false;
        if (n.Contains("wifi") || n.Contains("wi-fi") || n.Contains("wireless"))
            return false;
        return n.Contains("ethernet")
               || n.Contains("gigabit")
               || n.Contains("realtek")
               || n.Contains("i225")
               || n.Contains("i226")
               || n.Contains("i219")
               || n.Contains("i211")
               || n.Contains("i210")
               || n.Contains("82599")
               || n.Contains("network connection");
    }

    private sealed record EthernetTarget(
        string? RegistrySubKey,
        string? DeviceName,
        bool MagicPacketEnabled,
        bool DeviceArmed);

    private static List<EthernetTarget> GetEthernetWakeTargets()
    {
        var armed = new HashSet<string>(
            PowerCfg.QueryLines("/devicequery wake_armed"),
            StringComparer.OrdinalIgnoreCase);
        var programmable = PowerCfg.QueryLines("/devicequery wake_programmable");
        var deviceNames = programmable
            .Concat(armed)
            .Where(IsEthernetName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<EthernetTarget>();
        var usedDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(NicClassKey);
            if (root is not null)
            {
                foreach (var subName in root.GetSubKeyNames())
                {
                    if (!char.IsDigit(subName[0]))
                        continue;

                    using var sub = root.OpenSubKey(subName);
                    if (sub is null)
                        continue;

                    var desc = sub.GetValue("DriverDesc") as string;
                    if (string.IsNullOrWhiteSpace(desc) || !IsEthernetName(desc))
                        continue;

                    var magic = sub.GetValue("*WakeOnMagicPacket")
                                ?? sub.GetValue("WakeOnMagicPacket")
                                ?? sub.GetValue("EnablePME");
                    var magicOn = IsRegistryEnabled(magic);
                    var hasMagicKey = magic is not null;

                    var match = deviceNames.FirstOrDefault(d =>
                        d.Equals(desc, StringComparison.OrdinalIgnoreCase)
                        || d.Contains(desc, StringComparison.OrdinalIgnoreCase)
                        || desc.Contains(d, StringComparison.OrdinalIgnoreCase));

                    if (match is not null)
                        usedDevices.Add(match);

                    if (!hasMagicKey && match is null)
                        continue;

                    result.Add(new EthernetTarget(
                        hasMagicKey ? subName : null,
                        match,
                        magicOn,
                        match is not null && armed.Contains(match)));
                }
            }
        }
        catch
        {
            // Registry may be partially unreadable; fall through to device names.
        }

        foreach (var name in deviceNames)
        {
            if (usedDevices.Contains(name))
                continue;
            result.Add(new EthernetTarget(null, name, false, armed.Contains(name)));
        }

        if (result.Count == 0)
        {
            foreach (var desc in QueryPhysicalEthernetDescriptions())
            {
                var match = deviceNames.FirstOrDefault(d =>
                    d.Equals(desc, StringComparison.OrdinalIgnoreCase));
                result.Add(new EthernetTarget(
                    null,
                    match ?? desc,
                    false,
                    match is not null && armed.Contains(match)));
            }
        }

        return result;
    }

    private static List<string> QueryPhysicalEthernetDescriptions()
    {
        var list = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Description, PhysicalAdapter FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True");
            foreach (ManagementObject obj in searcher.Get())
            {
                using (obj)
                {
                    var desc = obj["Description"] as string;
                    if (string.IsNullOrWhiteSpace(desc) || !IsEthernetName(desc))
                        continue;
                    list.Add(desc);
                }
            }
        }
        catch
        {
            // WMI can be unavailable
        }

        return list;
    }

    private static bool IsRegistryEnabled(object? value)
    {
        if (value is null)
            return false;
        var s = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        return s is "1" or "2";
    }

    private static bool TryWriteMagicPacket(string? subKey, bool enabled, out bool accessDenied)
    {
        accessDenied = false;
        if (string.IsNullOrEmpty(subKey))
            return true;

        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(NicClassKey, writable: true);
            if (root is null)
            {
                accessDenied = true;
                return false;
            }

            using var sub = root.OpenSubKey(subKey, writable: true);
            if (sub is null)
            {
                accessDenied = true;
                return false;
            }

            foreach (var valueName in new[] { "*WakeOnMagicPacket", "WakeOnMagicPacket", "EnablePME" })
            {
                if (sub.GetValue(valueName) is null)
                    continue;
                var kind = sub.GetValueKind(valueName);
                object raw = kind == RegistryValueKind.DWord
                    ? (enabled ? 1 : 0)
                    : (enabled ? "1" : "0");
                sub.SetValue(valueName, raw, kind);
            }

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            accessDenied = true;
            return false;
        }
        catch (System.Security.SecurityException)
        {
            accessDenied = true;
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetLastWakeFromEventLog(out LastWakeInfo info)
    {
        info = new LastWakeInfo(false, null, null);
        try
        {
            var query = new EventLogQuery(
                "System",
                PathType.LogName,
                "*[System[Provider[@Name='Microsoft-Windows-Power-Troubleshooter'] and EventID=1]]")
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(query);
            using var ev = reader.ReadEvent();
            if (ev is null || ev.Properties.Count < 15)
                return false;

            var wakeTime = ev.Properties[1].Value is DateTime dt
                ? dt.ToLocalTime()
                : ev.TimeCreated;

            var source = ev.Properties[14].Value as string;
            if (string.IsNullOrWhiteSpace(source))
                return false;

            info = new LastWakeInfo(true, source.Trim(), wakeTime);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static LastWakeInfo GetLastWakeFromPowerCfg()
    {
        if (!PowerCfg.TryRun("/lastwake", out _, out var output) || string.IsNullOrWhiteSpace(output))
            return new LastWakeInfo(false, null, null);

        string? source = null;
        string? type = null;
        string? owner = null;

        foreach (var raw in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            var sep = line.IndexOf(':');
            if (sep <= 0)
                continue;

            var key = line[..sep].Trim().ToLowerInvariant();
            var value = line[(sep + 1)..].Trim();
            if (value.Length == 0)
                continue;

            if (key.Contains("convivial") || key.Contains("friendly") || key.Contains("anzeigename")
                || key.Contains("descriptivo") || key.Contains("descrittivo") || key.Contains("amigáv")
                || key.Contains("amigav") || key.Contains("vriendelijke") || key.Contains("понятн")
                || key.Contains("友好"))
            {
                source = value;
            }
            else if (key is "type" or "typ" or "tipo")
            {
                type = value;
            }
            else if (key.Contains("owner") || key.Contains("propriéta") || key.Contains("proprieta")
                     || key.Contains("propietario") || key.Contains("besitzer") || key.Contains("eigenaar"))
            {
                owner = value;
            }
        }

        var text = source ?? owner ?? type;
        if (string.IsNullOrWhiteSpace(text))
            return new LastWakeInfo(false, null, null);

        return new LastWakeInfo(true, text, null);
    }
}
