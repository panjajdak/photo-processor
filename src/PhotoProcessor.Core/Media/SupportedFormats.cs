namespace PhotoProcessor.Core.Media;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// File extensions the processor handles, for help text and documentation.
/// </summary>
public static class SupportedFormats
{
    public static IReadOnlyList<string> Images { get; } = Format(MediaTypes.ExtensionsOf(MediaKind.Image));

    public static IReadOnlyList<string> Videos { get; } = Format(MediaTypes.ExtensionsOf(MediaKind.Video));

    /// <summary>Files renamed together with the photo or video of the same name.</summary>
    public static IReadOnlyList<string> Sidecars { get; } = Format(MediaTypes.SidecarExtensionList);

    private static IReadOnlyList<string> Format(IEnumerable<string> extensions)
        => extensions.Select(e => e.TrimStart('.').ToUpperInvariant()).ToList();
}
