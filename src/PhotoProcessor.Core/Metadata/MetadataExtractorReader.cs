namespace PhotoProcessor.Core.Metadata;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using Directory = MetadataExtractor.Directory;

internal sealed partial class MetadataExtractorReader : IMetadataReader
{
    public MediaMetadata Read(string path)
    {
        IReadOnlyList<Directory> directories = ImageMetadataReader.ReadMetadata(path);

        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var movieHeader = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
        var quickTimeMetadata = directories.OfType<QuickTimeMetadataHeaderDirectory>().FirstOrDefault();

        return new MediaMetadata
        {
            DateTimeOriginal = GetDate(subIfd, ExifDirectoryBase.TagDateTimeOriginal),
            DateTimeDigitized = GetDate(subIfd, ExifDirectoryBase.TagDateTimeDigitized),
            DateTimeModified = GetDate(ifd0, ExifDirectoryBase.TagDateTime),
            SubSecondOriginal = GetString(subIfd, ExifDirectoryBase.TagSubsecondTimeOriginal),
            QuickTimeCreationDate = ParseQuickTimeCreationDate(quickTimeMetadata?.GetObject(QuickTimeMetadataHeaderDirectory.TagCreationDate)),
            QuickTimeCreated = GetQuickTimeCreated(movieHeader),
            Make = GetString(ifd0, ExifDirectoryBase.TagMake)
                ?? GetString(quickTimeMetadata, QuickTimeMetadataHeaderDirectory.TagMake)
                ?? GetString(quickTimeMetadata, QuickTimeMetadataHeaderDirectory.TagAndroidManufacturer),
            Model = GetString(ifd0, ExifDirectoryBase.TagModel)
                ?? GetString(quickTimeMetadata, QuickTimeMetadataHeaderDirectory.TagModel)
                ?? GetString(quickTimeMetadata, QuickTimeMetadataHeaderDirectory.TagAndroidModel),
        };
    }

    internal static DateTimeOffset? ParseQuickTimeCreationDate(object? value)
    {
        switch (value)
        {
            case DateTimeOffset dto:
                return dto;
            case DateTime dt:
                return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), TimeSpan.Zero);
            case string s:
                var normalized = CompactOffset().Replace(s.Trim(), "$1:$2");
                return DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? parsed
                    : null;
            default:
                return null;
        }
    }

    private static readonly DateTime UnsetQuickTimeDate = new(1904, 1, 1);

    private static DateTime? GetQuickTimeCreated(QuickTimeMovieHeaderDirectory? directory)
    {
        return directory != null
            && directory.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var created)
            && created > UnsetQuickTimeDate
                ? created
                : null;
    }

    private static DateTime? GetDate(Directory? directory, int tag)
        => directory != null && directory.TryGetDateTime(tag, out var date) ? date : null;

    private static string? GetString(Directory? directory, int tag)
    {
        var value = directory?.GetString(tag)?.Trim('\0', ' ');
        return string.IsNullOrEmpty(value) ? null : value;
    }

    [GeneratedRegex(@"([+-]\d{2})(\d{2})$")]
    private static partial Regex CompactOffset();
}
