namespace PhotoProcessor.Core.Pipeline;

using System;
using System.Collections.Generic;
using System.Linq;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.IO;
using PhotoProcessor.Core.Media;

internal sealed class FileDiscovery(IFileSystem fileSystem, IProcessingObserver observer)
{
    public IReadOnlyList<string> Discover(string inputDirectory)
    {
        var supported = new List<string>();
        foreach (var path in fileSystem.GetFiles(inputDirectory).Order(StringComparer.OrdinalIgnoreCase))
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
}
