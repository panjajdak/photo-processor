namespace PhotoProcessor.Core;

using System.Collections.Generic;
using System.IO;
using System.Linq;

internal sealed class DuplicateIndex
{
    private readonly Dictionary<long, List<Entry>> bySize = new();
    private readonly FileSystemView fileSystem;

    public DuplicateIndex(FileSystemView fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public void Add(string path, string contentPath)
    {
        var size = new FileInfo(contentPath).Length;
        if (!bySize.TryGetValue(size, out var entries))
        {
            bySize[size] = entries = [];
        }

        entries.Add(new Entry(path, contentPath));
    }

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
