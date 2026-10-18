namespace PhotoProcessor.Core.Pipeline;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.IO;
using PhotoProcessor.Core.Media;

internal sealed class FileDiscovery(IFileSystem fileSystem, IProcessingObserver observer)
{
    public IReadOnlyList<string> Discover(string inputDirectory, string outputDirectory, bool recursive)
    {
        var files = recursive
            ? AllFiles(inputDirectory, outputDirectory)
            : SortedFiles(inputDirectory);

        if (!recursive)
        {
            var skipped = ScannableSubfolders(inputDirectory, outputDirectory)
                .Where(folder => ContainsFiles(folder, outputDirectory))
                .ToList();
            if (skipped.Count > 0)
            {
                observer.On(new SubfoldersSkipped(skipped));
            }
        }

        var supported = new List<string>();
        foreach (var path in files)
        {
            if (MediaTypes.GetKind(path) != null || MediaTypes.IsSidecar(path))
            {
                supported.Add(path);
            }
            else
            {
                observer.On(new UnsupportedSkipped(path));
            }
        }

        return supported;
    }

    private IEnumerable<string> AllFiles(string directory, string outputDirectory)
        => SortedFiles(directory)
            .Concat(ScannableSubfolders(directory, outputDirectory).SelectMany(folder => AllFiles(folder, outputDirectory)));

    private bool ContainsFiles(string directory, string outputDirectory)
        => fileSystem.GetFiles(directory).Count > 0
            || ScannableSubfolders(directory, outputDirectory).Any(folder => ContainsFiles(folder, outputDirectory));

    private IEnumerable<string> SortedFiles(string directory)
        => fileSystem.GetFiles(directory).Order(StringComparer.OrdinalIgnoreCase);

    private IEnumerable<string> ScannableSubfolders(string directory, string outputDirectory)
        => fileSystem.GetDirectories(directory)
            .Where(folder => !Path.GetFileName(folder).StartsWith('.')
                && !fileSystem.IsHidden(folder)
                && !FileComparer.PathsEqual(folder, outputDirectory))
            .Order(StringComparer.OrdinalIgnoreCase);
}
