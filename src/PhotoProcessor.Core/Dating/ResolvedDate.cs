namespace PhotoProcessor.Core.Dating;

using System;

internal readonly record struct ResolvedDate(DateTime Value, DateSource Source)
{
    private static readonly DateSource[] SourcesMostTrustworthyFirst =
    [
        DateSource.ExifOriginal,
        DateSource.QuickTimeCreationDate,
        DateSource.ExifDigitized,
        DateSource.QuickTimeUtc,
        DateSource.QuickTimeLocal,
        DateSource.FileName,
        DateSource.ExifModified,
        DateSource.FileNameDateOnly,
        DateSource.FileModified,
    ];

    public int TrustRank => Array.IndexOf(SourcesMostTrustworthyFirst, Source);
}
