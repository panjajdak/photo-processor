namespace PhotoProcessor.Core.Metadata;

using System;

internal sealed record MediaMetadata
{
    public static readonly MediaMetadata Empty = new();

    public DateTime? DateTimeOriginal { get; init; }

    public DateTime? DateTimeDigitized { get; init; }

    public DateTime? DateTimeModified { get; init; }

    public string? SubSecondOriginal { get; init; }

    public DateTimeOffset? QuickTimeCreationDate { get; init; }

    public DateTime? QuickTimeCreated { get; init; }

    public string? Make { get; init; }
    public string? Model { get; init; }
}
