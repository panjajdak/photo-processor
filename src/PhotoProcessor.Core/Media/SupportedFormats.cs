namespace PhotoProcessor.Core.Media;

using System.Collections.Generic;
using System.Linq;

/// <summary>File types the processor handles.</summary>
public static class SupportedFormats
{
    /// <summary>Photo extensions, including RAW formats.</summary>
    public static IReadOnlyList<string> Images { get; } = Format(MediaTypes.ExtensionsOf(MediaKind.Image));

    /// <summary>Video extensions.</summary>
    public static IReadOnlyList<string> Videos { get; } = Format(MediaTypes.ExtensionsOf(MediaKind.Video));

    /// <summary>Extensions renamed together with the photo or video of the same name.</summary>
    public static IReadOnlyList<string> Sidecars { get; } = Format(MediaTypes.SidecarExtensionList);

    private static IReadOnlyList<string> Format(IEnumerable<string> extensions)
        => extensions.Select(e => e.TrimStart('.').ToUpperInvariant()).ToList();
}
