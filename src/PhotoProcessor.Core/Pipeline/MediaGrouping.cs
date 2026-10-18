namespace PhotoProcessor.Core.Pipeline;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.Media;

internal sealed class MediaGrouping(IProcessingObserver observer)
{
    public IReadOnlyList<MediaGroup> Group(IReadOnlyList<string> files)
    {
        var groups = new List<MediaGroup>();
        foreach (var group in files.GroupBy(StemInFolder, StringComparer.OrdinalIgnoreCase))
        {
            var paths = group.ToList();
            if (paths.Any(path => MediaTypes.GetKind(path) != null))
            {
                groups.Add(new MediaGroup(MediaTypes.GetStem(paths[0]), paths));
                continue;
            }

            foreach (var path in paths)
            {
                observer.On(new OrphanSidecarSkipped(path));
            }
        }

        return groups;
    }

    private static string StemInFolder(string path) => Path.Combine(Path.GetDirectoryName(path)!, MediaTypes.GetStem(path));
}
