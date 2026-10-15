namespace PhotoProcessor.Core.Metadata;

using System;

/// <summary>
/// Raw values read from a file's metadata. Every value is optional; interpretation happens in <see cref="Dating.DateResolver"/>.
/// </summary>
internal sealed record MediaMetadata
{
    public static readonly MediaMetadata Empty = new();

    /// <summary>EXIF DateTimeOriginal: when the shutter was pressed, camera-local time.</summary>
    public DateTime? DateTimeOriginal { get; init; }

    /// <summary>EXIF DateTimeDigitized, camera-local time.</summary>
    public DateTime? DateTimeDigitized { get; init; }

    /// <summary>EXIF (IFD0) DateTime: when the file was last changed, e.g. by an editor.</summary>
    public DateTime? DateTimeModified { get; init; }

    /// <summary>EXIF SubSecTimeOriginal, used to order photos taken within the same second.</summary>
    public string? SubSecondOriginal { get; init; }

    /// <summary>QuickTime creation date written with its UTC offset (iPhone "com.apple.quicktime.creationdate").</summary>
    public DateTimeOffset? QuickTimeCreationDate { get; init; }

    /// <summary>QuickTime movie header creation time. UTC by spec, but some cameras (GoPro) write local time.</summary>
    public DateTime? QuickTimeCreated { get; init; }

    public string? Make { get; init; }
    public string? Model { get; init; }
}
