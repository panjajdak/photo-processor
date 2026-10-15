namespace PhotoProcessor.Core.Abstractions;

internal interface IDeviceNameResolver
{
    /// <summary>
    /// Resolves friendly device name based on one from metadata
    /// </summary>
    string Resolve(string deviceName);
}