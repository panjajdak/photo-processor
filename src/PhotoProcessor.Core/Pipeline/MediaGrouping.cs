namespace PhotoProcessor.Core.Pipeline;

using System;
using System.Collections.Generic;
using System.Linq;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.Media;

internal sealed class MediaGrouping(IProcessingObserver observer)
{
    public IReadOnlyList<MediaGroup> Group(IReadOnlyList<string> files)
    {
        var groups = new List<MediaGroup>();
        foreach (var group in files.GroupBy(MediaTypes.GetStem, StringComparer.OrdinalIgnoreCase))
        {
            var paths = group.ToList();
            if (paths.Any(path => MediaTypes.GetKind(path) != null))
            {
                groups.Add(new MediaGroup(group.Key, paths));
                continue;
            }

            foreach (var path in paths)
            {
                observer.On(new OrphanSidecarSkipped(path));
            }
        }

        return groups;
    }
}
