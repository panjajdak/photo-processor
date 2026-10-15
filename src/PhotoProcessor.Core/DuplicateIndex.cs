namespace PhotoProcessor.Core;

using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Files in the output directory grouped by size, to find byte-identical copies of an incoming file whatever their name.
/// </summary>
internal sealed class DuplicateIndex
{
    private readonly Dictionary<long, List<Entry>> bySize = new();
    private readonly FileSystemView fileSystem;

    public DuplicateIndex(FileSystemView fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    /// <param name="path">Where the file is, or will be after a dry run.</param>
    /// <param name="contentPath">Where its bytes can be read now; differs from <paramref name="path"/> in a dry run.</param>
    public void Add(string path, string contentPath)
    {
        var size = new FileInfo(contentPath).Length;
        if (!bySize.TryGetValue(size, out var entries))
        {
            bySize[size] = entries = [];
        }

        entries.Add(new Entry(path, contentPath));
    }

    /// <summary>
    /// Returns the indexed path of a file identical to <paramref name="source"/>, ignoring the source itself.
    /// </summary>
    public string? FindIdentical(string source)
    {
        var size = new FileInfo(source).Length;
        if (!bySize.TryGetValue(size, out var entries))
        {
            return null;
        }

        return entries
            .Where(e => !FileComparer.PathsEqual(e.ContentPath, source) && fileSystem.Exists(e.Path))
            .FirstOrDefault(e => FileComparer.AreIdentical(source, e.ContentPath))
            ?.Path;
    }

    private sealed record Entry(string Path, string ContentPath);
}
