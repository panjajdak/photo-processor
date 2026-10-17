namespace PhotoProcessor.Core.Media;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

    public static string GetStem(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return IsSidecar(path) && GetKind(name) != null
            ? Path.GetFileNameWithoutExtension(name)
            : name;
    }

    public static string GetOutputExtension(string path)
    {
        var name = Path.GetFileName(path);
        var extension = name[GetStem(path).Length..].ToLowerInvariant();
        return extension.StartsWith(".jpeg", StringComparison.Ordinal)
            ? ".jpg" + extension[".jpeg".Length..]
            : extension;
    }
}
