namespace PhotoProcessor.Core.Resolvers;

using System.Collections.Generic;
using PhotoProcessor.Core.Abstractions;
using PhotoProcessor.Core.Config;

internal class DeviceNameResolver : IDeviceNameResolver
{
    private readonly Dictionary<string, string> deviceMapping;

    public DeviceNameResolver(Configuration config)
    {
        deviceMapping = config.DeviceMapping;
    }

    /// <inheritdoc/>
    public string Resolve(string key)
    {
        if (deviceMapping.TryGetValue(key, out var mapping))
        {
            return mapping;
        }
        else
        {
            return key;
        }
    }
}
