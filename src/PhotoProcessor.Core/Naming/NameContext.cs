namespace PhotoProcessor.Core.Naming;

using System;

/// <summary>
/// Values available to a <see cref="FileNameTemplate"/> for one file or group.
/// </summary>
/// <param name="Type">Image or video prefix, e.g. "IMG".</param>
/// <param name="Date">Date taken.</param>
/// <param name="Device">Mapped device name.</param>
/// <param name="Make">Raw camera make.</param>
/// <param name="Model">Raw camera model.</param>
/// <param name="Original">Original file name without extension.</param>
internal sealed record NameContext(string Type, DateTime Date, string? Device, string? Make, string? Model, string? Original);
