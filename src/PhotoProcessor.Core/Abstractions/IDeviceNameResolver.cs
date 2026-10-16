namespace PhotoProcessor.Core.Abstractions;

internal interface IDeviceNameResolver
{
    string? Resolve(string? deviceName);
}
