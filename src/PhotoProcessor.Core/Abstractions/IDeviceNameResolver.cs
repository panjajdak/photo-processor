namespace PhotoProcessor.Core.Abstractions;

internal interface IDeviceNameResolver
{
    /// <summary>
    /// Resolves friendly device name based on one from metadata, made safe for use in a file name.
    /// Returns null when there is no usable name.
    /// </summary>
    string? Resolve(string? deviceName);
}
