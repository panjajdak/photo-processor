namespace PhotoProcessor.Core.Pipeline;

using System;
using System.Collections.Generic;
using System.Linq;
using PhotoProcessor.Core.IO;

internal sealed class OutputFolderState
{
    private readonly IFileSystem fileSystem;
    private readonly HashSet<string> reserved = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> vacated = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<long, List<IndexedFile>> filesBySize = new();

    public OutputFolderState(IFileSystem fileSystem, string outputDirectory, IEnumerable<string> filesAboutToMove)
    {
        this.fileSystem = fileSystem;
        if (!fileSystem.DirectoryExists(outputDirectory))
        {
            return;
        }

        var moving = filesAboutToMove.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in fileSystem.GetFiles(outputDirectory).Where(p => !moving.Contains(p)))
        {
            Index(path, path);
        }
    }

    public bool IsTaken(string path) => reserved.Contains(path) || (!vacated.Contains(path) && fileSystem.FileExists(path));

    public void Reserve(string source, string target, bool sourceIsMoved)
    {
        reserved.Add(target);
        if (sourceIsMoved)
        {
            vacated.Add(source);
            reserved.Remove(source);
        }

        Index(target, source);
    }

    public string? FindIdentical(string source)
    {
        var size = fileSystem.GetFacts(source).Length;
        if (!filesBySize.TryGetValue(size, out var candidates))
        {
            return null;
        }

        return candidates
            .Where(c => !FileComparer.PathsEqual(c.ContentPath, source) && IsTaken(c.Path))
            .FirstOrDefault(c => FileComparer.AreIdentical(fileSystem, source, c.ContentPath))
            ?.Path;
    }

    private void Index(string path, string contentPath)
    {
        var size = fileSystem.GetFacts(contentPath).Length;
        if (!filesBySize.TryGetValue(size, out var files))
        {
            filesBySize[size] = files = [];
        }

        files.Add(new IndexedFile(path, contentPath));
    }

    private sealed record IndexedFile(string Path, string ContentPath);
}
