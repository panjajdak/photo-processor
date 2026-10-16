namespace PhotoProcessor.Core.Naming;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhotoProcessor.Core.Config;

internal sealed class DeviceNameResolver
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly Dictionary<string, string> deviceMapping;

    public DeviceNameResolver(Configuration config)
    {
        deviceMapping = new Dictionary<string, string>(config.DeviceMapping, StringComparer.OrdinalIgnoreCase);
    }

    public string? Resolve(string? deviceName)
    {
        var key = deviceName?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        var name = deviceMapping.TryGetValue(key, out var mapping) ? mapping : key;
        return Sanitize(name);
    }

    private static string? Sanitize(string name)
    {
        var cleaned = new string(name.Select(c => InvalidFileNameChars.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
        return cleaned.Length == 0 ? null : cleaned;
    }
}
