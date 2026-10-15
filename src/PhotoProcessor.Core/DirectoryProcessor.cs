namespace PhotoProcessor.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhotoProcessor.Core.Abstractions;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Dating;
using PhotoProcessor.Core.Media;
using PhotoProcessor.Core.Metadata;
using PhotoProcessor.Core.Naming;
using PhotoProcessor.Core.Resolvers;

/// <summary>
/// Counts of files (not groups) by outcome.
/// </summary>
public sealed record ProcessingSummary(int Renamed, int AlreadyNamed, int Duplicates, int Unsupported, int Failed);

public class DirectoryProcessor
{
    private readonly Configuration config;
    private readonly ILog log;
    private readonly IMetadataReader metadataReader;
    private readonly DateResolver dateResolver;
    private readonly IDeviceNameResolver deviceResolver;
    private readonly FileNamer namer;

    public DirectoryProcessor(Configuration config, ILog log)
        : this(config, log, new MetadataExtractorReader())
    { }

    internal DirectoryProcessor(Configuration config, ILog log, IMetadataReader metadataReader)
    {
        this.config = config;
        this.log = log;
        this.metadataReader = metadataReader;
        dateResolver = new DateResolver(config);
        deviceResolver = new DeviceNameResolver(config);
        namer = new FileNamer(config);
    }

    public ProcessingSummary Process(string inputDirectory, string outputDirectory)
    {
        inputDirectory = Path.GetFullPath(inputDirectory);
        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var counts = new Counts();
        var planned = new List<PlannedGroup>();
        var supported = new List<string>();

        foreach (var path in Directory.GetFiles(inputDirectory).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (MediaTypes.GetKind(path) != null || MediaTypes.IsSidecar(path))
            {
                supported.Add(path);
                continue;
            }

            log.LogMessage($"Skipped (unsupported type): {path}");
            counts.Unsupported++;
        }

        // A photo, its RAW/Live Photo companions and its sidecars share a stem and are renamed together.
        foreach (var group in supported.GroupBy(MediaTypes.GetStem, StringComparer.OrdinalIgnoreCase))
        {
            if (!group.Any(path => MediaTypes.GetKind(path) != null))
            {
                foreach (var path in group)
                {
                    log.LogMessage($"Skipped (sidecar without media file): {path}");
                    counts.Unsupported++;
                }

                continue;
            }

            try
            {
                planned.Add(Plan(group.ToList()));
            }
            catch (Exception ex)
            {
                log.LogMessage($"Failed: {string.Join(", ", group)}");
                log.LogError(ex);
                counts.Failed += group.Count();
            }
        }

        // Groups that end up with the same name are numbered in shooting order, so results don't depend on directory listing order.
        var ordered = planned
            .OrderBy(g => g.Date.Value)
            .ThenBy(g => g.SubSecondSortKey, StringComparer.Ordinal)
            .ThenBy(g => g.Stem, StringComparer.OrdinalIgnoreCase);

        foreach (var group in ordered)
        {
            try
            {
                Execute(group, outputDirectory, counts);
            }
            catch (Exception ex)
            {
                log.LogMessage($"Failed: {string.Join(", ", group.Members.Select(m => m.SourcePath))}");
                log.LogError(ex);
                counts.Failed += group.Members.Count(m => File.Exists(m.SourcePath));
            }
        }

        return new ProcessingSummary(counts.Renamed, counts.AlreadyNamed, counts.Duplicates, counts.Unsupported, counts.Failed);
    }

    private PlannedGroup Plan(IReadOnlyList<string> paths)
    {
        var media = paths
            .Where(path => MediaTypes.GetKind(path) != null)
            .Select(path => (Path: path, Kind: MediaTypes.GetKind(path)!.Value, Metadata: ReadMetadata(path)))
            .Select(m => (m.Path, m.Kind, m.Metadata, Date: dateResolver.Resolve(m.Kind, m.Metadata, Path.GetFileName(m.Path), File.GetLastWriteTime(m.Path))))
            .ToList();

        // The best-dated file names the group; a Live Photo or RAW+JPEG pair is named as an image.
        var primary = media
            .OrderBy(m => m.Date.Source)
            .ThenBy(m => m.Kind)
            .ThenBy(m => m.Path, StringComparer.OrdinalIgnoreCase)
            .First();
        var kind = media.Any(m => m.Kind == MediaKind.Image) ? MediaKind.Image : MediaKind.Video;
        var model = primary.Metadata.Model ?? media.Select(m => m.Metadata.Model).FirstOrDefault(m => m != null);
        var baseName = namer.BuildBaseName(kind, primary.Date.Value, deviceResolver.Resolve(model));

        var members = paths
            .Select(path => new PlannedMember(path, MediaTypes.GetOutputExtension(path)))
            .ToList();

        if (members.GroupBy(m => m.Extension).Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException("Several files in the group would get the same name.");
        }

        return new PlannedGroup(MediaTypes.GetStem(paths[0]), members, baseName, primary.Date, SubSecondSortKey(primary.Metadata.SubSecondOriginal));
    }

    private MediaMetadata ReadMetadata(string path)
    {
        try
        {
            return metadataReader.Read(path);
        }
        catch (Exception ex)
        {
            // Unreadable metadata still leaves the file name and file time to date the file.
            log.LogMessage($"Warning: could not read metadata of {path}: {ex.Message}");
            return MediaMetadata.Empty;
        }
    }

    private void Execute(PlannedGroup group, string outputDirectory, Counts counts)
    {
        var sourceDirectory = Path.GetDirectoryName(group.Members[0].SourcePath)!;
        if (PathsEqual(sourceDirectory, outputDirectory) && HasTargetName(group.Stem, group.BaseName))
        {
            counts.AlreadyNamed += group.Members.Count;
            return;
        }

        for (var index = 0; ; index++)
        {
            var targets = group.Members
                .Select(m => (Member: m, Target: Path.Combine(outputDirectory, namer.BuildFileName(group.BaseName, index, m.Extension))))
                .ToList();

            var states = targets.Select(t => GetTargetState(t.Member.SourcePath, t.Target)).ToList();

            if (states.Contains(TargetState.Taken))
            {
                continue;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var (member, target) = targets[i];
                switch (states[i])
                {
                    case TargetState.Self:
                        counts.AlreadyNamed++;
                        break;
                    case TargetState.Identical:
                        log.LogMessage($"Duplicate of {target}, left in place: {member.SourcePath}");
                        counts.Duplicates++;
                        break;
                    case TargetState.Free:
                        Transfer(member.SourcePath, target);
                        log.LogMessage($"{Path.GetFileName(member.SourcePath)} -> {Path.GetFileName(target)} [{group.Date.Source}]");
                        counts.Renamed++;
                        break;
                }
            }

            return;
        }
    }

    private static TargetState GetTargetState(string source, string target)
    {
        if (PathsEqual(source, target))
        {
            return TargetState.Self;
        }

        if (!File.Exists(target))
        {
            return TargetState.Free;
        }

        return FilesAreIdentical(source, target) ? TargetState.Identical : TargetState.Taken;
    }

    private void Transfer(string source, string target)
    {
        if (config.Options.CopyingEnabled)
        {
            File.Copy(source, target);
        }
        else
        {
            File.Move(source, target);
        }
    }

    /// <summary>
    /// True when the stem already is the name this run would give the group, with or without a counter.
    /// </summary>
    private bool HasTargetName(string stem, string baseName)
    {
        if (stem.Equals(baseName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var counterPrefix = baseName + config.Delimiter;
        return stem.StartsWith(counterPrefix, StringComparison.OrdinalIgnoreCase)
            && stem.Length > counterPrefix.Length
            && stem[counterPrefix.Length..].All(char.IsAsciiDigit);
    }

    private static bool PathsEqual(string a, string b)
        => string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    private static bool FilesAreIdentical(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length)
        {
            return false;
        }

        const int BufferSize = 1 << 16;
        using var streamA = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        using var streamB = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        var bufferA = new byte[BufferSize];
        var bufferB = new byte[BufferSize];

        while (true)
        {
            var readA = streamA.ReadAtLeast(bufferA, BufferSize, throwOnEndOfStream: false);
            var readB = streamB.ReadAtLeast(bufferB, BufferSize, throwOnEndOfStream: false);
            if (readA != readB || !bufferA.AsSpan(0, readA).SequenceEqual(bufferB.AsSpan(0, readB)))
            {
                return false;
            }

            if (readA == 0)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// EXIF sub-seconds are a decimal fraction ("5" means .5), so right-pad to compare them as strings.
    /// </summary>
    private static string SubSecondSortKey(string? subSecond)
        => subSecond != null && subSecond.All(char.IsAsciiDigit) ? subSecond.PadRight(9, '0') : "";

    private enum TargetState
    {
        /// <summary>The file already is at the target.</summary>
        Self,
        Free,
        /// <summary>A byte-identical copy is at the target.</summary>
        Identical,
        /// <summary>A different file is at the target.</summary>
        Taken,
    }

    private sealed class Counts
    {
        public int Renamed;
        public int AlreadyNamed;
        public int Duplicates;
        public int Unsupported;
        public int Failed;
    }

    private sealed record PlannedMember(string SourcePath, string Extension);

    private sealed record PlannedGroup(string Stem, IReadOnlyList<PlannedMember> Members, string BaseName, ResolvedDate Date, string SubSecondSortKey);
}
