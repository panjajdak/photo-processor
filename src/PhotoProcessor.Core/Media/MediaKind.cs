namespace PhotoProcessor.Core.Media;

using System;
using System.Collections.Generic;
using System.IO;

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
        [".mp4"] = MediaKind.Video,
    };

    public static MediaKind? GetKind(string path)
        => KindsByExtension.TryGetValue(Path.GetExtension(path), out var kind) ? kind : null;

    /// <summary>
    /// Lower-cased extension of the output file, with ".jpeg" normalized to ".jpg".
    /// </summary>
    public static string GetOutputExtension(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension == ".jpeg" ? ".jpg" : extension;
    }
}
