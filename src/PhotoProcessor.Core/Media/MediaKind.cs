namespace PhotoProcessor.Core.Media;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal enum MediaKind
{
    Image,
    Video,
}

internal static class MediaTypes
{
    private static readonly Dictionary<string, MediaKind> KindsByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = MediaKind.Image,
        [".jpeg"] = MediaKind.Image,
        [".png"] = MediaKind.Image,
        [".heic"] = MediaKind.Image,
        [".heif"] = MediaKind.Image,
        [".avif"] = MediaKind.Image,
        [".webp"] = MediaKind.Image,
        [".tif"] = MediaKind.Image,
        [".tiff"] = MediaKind.Image,

        // RAW
        [".dng"] = MediaKind.Image,
        [".cr2"] = MediaKind.Image,
        [".cr3"] = MediaKind.Image,
        [".nef"] = MediaKind.Image,
        [".nrw"] = MediaKind.Image,
        [".arw"] = MediaKind.Image,
        [".orf"] = MediaKind.Image,
        [".rw2"] = MediaKind.Image,
        [".raf"] = MediaKind.Image,
        [".pef"] = MediaKind.Image,
        [".srw"] = MediaKind.Image,

        [".mp4"] = MediaKind.Video,
        [".mov"] = MediaKind.Video,
        [".m4v"] = MediaKind.Video,
        [".3gp"] = MediaKind.Video,
    };

    /// <summary>
    /// Files that describe a media file with the same name and must be renamed with it:
    /// XMP edits (Lightroom "photo.xmp", darktable "photo.dng.xmp"), iPhone edits (.aae), camera thumbnails (.thm).
    /// </summary>
    private static readonly HashSet<string> SidecarExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xmp",
        ".aae",
        ".thm",
    };

    public static IEnumerable<string> ExtensionsOf(MediaKind kind)
        => KindsByExtension.Where(e => e.Value == kind).Select(e => e.Key);

    public static IEnumerable<string> SidecarExtensionList => SidecarExtensions;

    public static MediaKind? GetKind(string path)
        => KindsByExtension.TryGetValue(Path.GetExtension(path), out var kind) ? kind : null;

    public static bool IsSidecar(string path)
        => SidecarExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Name shared by a media file and its companions: "IMG_1234" for IMG_1234.HEIC, IMG_1234.MOV,
    /// IMG_1234.AAE, IMG_1234.xmp and IMG_1234.HEIC.xmp.
    /// </summary>
    public static string GetStem(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return IsSidecar(path) && GetKind(name) != null
            ? Path.GetFileNameWithoutExtension(name)
            : name;
    }

    /// <summary>
    /// Everything after the stem, lower-cased, with ".jpeg" normalized to ".jpg": ".jpg", ".xmp" or ".dng.xmp".
    /// </summary>
    public static string GetOutputExtension(string path)
    {
        var name = Path.GetFileName(path);
        var extension = name[GetStem(path).Length..].ToLowerInvariant();
        return extension.StartsWith(".jpeg", StringComparison.Ordinal)
            ? ".jpg" + extension[".jpeg".Length..]
            : extension;
    }
}
