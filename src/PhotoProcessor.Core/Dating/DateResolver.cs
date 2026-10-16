namespace PhotoProcessor.Core.Dating;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Media;
using PhotoProcessor.Core.Metadata;

internal enum DateSource
{
    ExifOriginal,
    QuickTimeCreationDate,
    ExifDigitized,
    QuickTimeUtc,
    QuickTimeLocal,
    FileName,
    ExifModified,
    FileNameDateOnly,
    FileModified,
}

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

internal sealed class DateResolver
{
    private readonly TimeZoneInfo timeZone;
    private readonly IReadOnlyList<Regex> localTimeVideoPatterns;

    public DateResolver(Configuration config)
        : this(
            string.IsNullOrWhiteSpace(config.TimeZone) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(config.TimeZone),
            config.LocalTimeVideoPatterns)
    { }

    public DateResolver(TimeZoneInfo timeZone, IEnumerable<string> localTimeVideoPatterns)
    {
        this.timeZone = timeZone;
        this.localTimeVideoPatterns = localTimeVideoPatterns
            .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .ToList();
    }

    public ResolvedDate Resolve(MediaKind kind, MediaMetadata metadata, string fileName, DateTime lastWriteTime)
    {
        var candidates = kind == MediaKind.Video
            ? VideoCandidates(metadata, fileName)
            : ImageCandidates(metadata, fileName);

        foreach (var candidate in candidates)
        {
            if (candidate is { } resolved)
            {
                return resolved;
            }
        }

        if (FileNameDateParser.ParseDate(fileName) is { } day && DateOnly.FromDateTime(lastWriteTime) != day)
        {
            return new ResolvedDate(day.ToDateTime(TimeOnly.MinValue), DateSource.FileNameDateOnly);
        }

        return new ResolvedDate(lastWriteTime, DateSource.FileModified);
    }

    private static IEnumerable<ResolvedDate?> ImageCandidates(MediaMetadata metadata, string fileName)
    {
        yield return From(metadata.DateTimeOriginal, DateSource.ExifOriginal);
        yield return From(metadata.DateTimeDigitized, DateSource.ExifDigitized);
        yield return From(FileNameDateParser.ParseDateTime(fileName), DateSource.FileName);
        yield return From(metadata.DateTimeModified, DateSource.ExifModified);
    }

    private IEnumerable<ResolvedDate?> VideoCandidates(MediaMetadata metadata, string fileName)
    {
        yield return From(metadata.QuickTimeCreationDate?.DateTime, DateSource.QuickTimeCreationDate);

        if (metadata.QuickTimeCreated is { } created)
        {
            yield return IsLocalTimeVideo(fileName)
                ? new ResolvedDate(created, DateSource.QuickTimeLocal)
                : new ResolvedDate(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(created, DateTimeKind.Utc), timeZone), DateSource.QuickTimeUtc);
        }

        yield return From(FileNameDateParser.ParseDateTime(fileName), DateSource.FileName);
    }

    private bool IsLocalTimeVideo(string fileName)
        => localTimeVideoPatterns.Any(p => p.IsMatch(fileName));

    private static ResolvedDate? From(DateTime? date, DateSource source)
        => date is { } value ? new ResolvedDate(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), source) : null;
}
