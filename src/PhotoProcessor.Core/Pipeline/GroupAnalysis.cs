namespace PhotoProcessor.Core.Pipeline;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Dating;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.IO;
using PhotoProcessor.Core.Media;
using PhotoProcessor.Core.Metadata;
using PhotoProcessor.Core.Naming;

internal sealed class GroupAnalysis(
    Configuration config,
    IMetadataReader metadataReader,
    IFileSystem fileSystem,
    IProcessingObserver observer)
{
    private readonly DateResolver dateResolver = new(config);
    private readonly DeviceNameResolver deviceNameResolver = new(config);

    public IReadOnlyList<AnalyzedGroup> Analyze(IReadOnlyList<MediaGroup> groups, CancellationToken cancellationToken)
    {
        var analyzed = new List<AnalyzedGroup>();
        var total = groups.Sum(g => g.Paths.Count);
        var done = 0;

        foreach (var group in groups)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return [];
            }

            try
            {
                analyzed.Add(Analyze(group));
            }
            catch (Exception ex)
            {
                observer.On(new FileFailed(group.Paths, ex));
            }

            done += group.Paths.Count;
            observer.On(new ProgressChanged("Reading metadata", done, total));
        }

        return analyzed;
    }

    private AnalyzedGroup Analyze(MediaGroup group)
    {
        var media = group.Paths
            .Where(path => MediaTypes.GetKind(path) != null)
            .Select(path => new AnalyzedFile(path, MediaTypes.GetKind(path)!.Value, ReadMetadata(path)))
            .Select(file => file with { Date = ResolveDate(file) })
            .ToList();

        var primary = media
            .OrderBy(m => m.Date.TrustRank)
            .ThenBy(m => m.Kind)
            .ThenBy(m => m.Path, StringComparer.OrdinalIgnoreCase)
            .First();
        var kind = media.Any(m => m.Kind == MediaKind.Image) ? MediaKind.Image : MediaKind.Video;
        var camera = primary.Metadata.Model != null ? primary.Metadata : media.FirstOrDefault(m => m.Metadata.Model != null)?.Metadata;

        var context = new NameContext(
            kind == MediaKind.Video ? config.VideoPrefix : config.ImagePrefix,
            primary.Date.Value,
            deviceNameResolver.Resolve(camera?.Model),
            camera?.Make,
            camera?.Model,
            group.Stem);

        var members = group.Paths
            .Select(path => new GroupMember(path, MediaTypes.GetOutputExtension(path)))
            .ToList();

        if (members.GroupBy(m => m.Extension).Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException("Several files in the group would get the same name.");
        }

        return new AnalyzedGroup(
            group.Stem,
            Path.GetDirectoryName(group.Paths[0])!,
            members,
            context,
            primary.Date.Source,
            SubSecondSortKey(primary.Metadata.SubSecondOriginal));
    }

    private ResolvedDate ResolveDate(AnalyzedFile file)
        => dateResolver.Resolve(file.Kind, file.Metadata, Path.GetFileName(file.Path), fileSystem.GetFacts(file.Path).LastWriteTime);

    private MediaMetadata ReadMetadata(string path)
    {
        try
        {
            return metadataReader.Read(path);
        }
        catch (Exception ex)
        {
            observer.On(new MetadataUnreadable(path, ex.Message));
            return MediaMetadata.Empty;
        }
    }

    private static string SubSecondSortKey(string? subSecond)
        => subSecond != null && subSecond.All(char.IsAsciiDigit) ? subSecond.PadRight(9, '0') : "";

    private sealed record AnalyzedFile(string Path, MediaKind Kind, MediaMetadata Metadata)
    {
        public ResolvedDate Date { get; init; }
    }
}
