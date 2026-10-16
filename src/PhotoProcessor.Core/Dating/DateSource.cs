namespace PhotoProcessor.Core.Dating;

/// <summary>Where the date in a file's new name came from.</summary>
public enum DateSource
{
    /// <summary>EXIF date the photo was taken.</summary>
    ExifOriginal,

    /// <summary>Video creation date written with its UTC offset (iPhone).</summary>
    QuickTimeCreationDate,

    /// <summary>EXIF date the photo was digitized.</summary>
    ExifDigitized,

    /// <summary>Video creation time stored in UTC, converted to local time.</summary>
    QuickTimeUtc,

    /// <summary>Video creation time already stored in local time (e.g. GoPro).</summary>
    QuickTimeLocal,

    /// <summary>Date and time in the original file name.</summary>
    FileName,

    /// <summary>EXIF date the file was last edited.</summary>
    ExifModified,

    /// <summary>Date without time in the original file name.</summary>
    FileNameDateOnly,

    /// <summary>File system modification time.</summary>
    FileModified,
}
