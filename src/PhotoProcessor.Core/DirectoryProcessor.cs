namespace PhotoProcessor.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PhotoProcessor.Core.Abstractions;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Dating;
using PhotoProcessor.Core.Journal;
using PhotoProcessor.Core.Media;
using PhotoProcessor.Core.Metadata;
using PhotoProcessor.Core.Naming;
using PhotoProcessor.Core.Resolvers;

/// <summary>
/// Counts of files (not groups) by outcome.
/// </summary>
public sealed record ProcessingSummary(int Renamed, int AlreadyNamed, int Duplicates, int Unsupported, int Failed);

/// <param name="JournalPath">Journal to undo this run with; null when nothing was moved or copied.</param>
/// <param name="Cancelled">The run was stopped early; files not reached yet were left alone.</param>
public sealed record ProcessingResult(ProcessingSummary Summary, string? JournalPath, bool DryRun, bool Cancelled = false);

public sealed record ProcessingProgress(string Stage, int Done, int Total);

public class DirectoryProcessor
{
    private readonly Configuration config;
    private readonly ILog log;
    private readonly IMetadataReader metadataReader;
    private readonly DateResolver dateResolver;
    private readonly IDeviceNameResolver deviceResolver;
    private readonly FileNameTemplate template;

    /// <exception cref="InvalidConfigurationException"><paramref name="config"/> has errors.</exception>
    public DirectoryProcessor(Configuration config, ILog log)
        : this(config, log, new MetadataExtractorReader())
    { }

    internal DirectoryProcessor(Configuration config, ILog log, IMetadataReader metadataReader)
    {
        ConfigurationCheck.EnsureValid(config);
        this.config = config;
        this.log = log;
        this.metadataReader = metadataReader;
        dateResolver = new DateResolver(config);
        deviceResolver = new DeviceNameResolver(config);
        template = FileNameTemplate.Parse(config.FileNameTemplate);
    }

    /// <param name="dryRun">Report what would happen without touching any file.</param>
    /// <param name="cancellationToken">Stops the run between files; the file being moved is always finished first.</param>
    public ProcessingResult Process(
        string inputDirectory,
        string outputDirectory,
        bool dryRun = false,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        inputDirectory = Path.GetFullPath(inputDirectory);
        outputDirectory = Path.GetFullPath(outputDirectory);
        if (!dryRun)
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var counts = new Counts();
        var fileSystem = new FileSystemView();
        using var journal = new RunJournal(outputDirectory);
        var run = new Run(dryRun, outputDirectory, counts, fileSystem, journal, new DuplicateIndex(fileSystem));

        var groups = Plan(ListSupportedFiles(inputDirectory, counts), counts, progress, cancellationToken);

        // Groups already carrying their name stay put; they are recognized first so incoming copies of them count as duplicates.
        var pending = new List<PlannedGroup>();
        foreach (var group in groups)
        {
            if (FileComparer.PathsEqual(Path.GetDirectoryName(group.Members[0].SourcePath)!, outputDirectory) && template.Matches(group.Stem, group.Context))
            {
                counts.AlreadyNamed += group.Members.Count;
            }
            else
            {
                pending.Add(group);
            }
        }

        IndexOutputDirectory(run, pending);

        // Groups that end up with the same name are numbered in shooting order, so results don't depend on directory listing order.
        var ordered = pending
            .OrderBy(g => g.Context.Date)
            .ThenBy(g => g.SubSecondSortKey, StringComparer.Ordinal)
            .ThenBy(g => g.Stem, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = ordered.Sum(g => g.Members.Count);
        var done = 0;
        foreach (var group in ordered)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                Execute(group, run);
            }
            catch (Exception ex)
            {
                log.LogWarning($"Failed: {string.Join(", ", group.Members.Select(m => m.SourcePath))}");
                log.LogError(ex);
                counts.Failed += group.Members.Count(m => File.Exists(m.SourcePath));
            }

            done += group.Members.Count;
            progress?.Report(new ProcessingProgress(dryRun ? "Planning" : "Renaming", done, total));
        }

        var summary = new ProcessingSummary(counts.Renamed, counts.AlreadyNamed, counts.Duplicates, counts.Unsupported, counts.Failed);
        return new ProcessingResult(summary, journal.Path, dryRun, cancellationToken.IsCancellationRequested);
    }

    private List<string> ListSupportedFiles(string inputDirectory, Counts counts)
    {
        var supported = new List<string>();
        foreach (var path in Directory.GetFiles(inputDirectory).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (MediaTypes.GetKind(path) != null || MediaTypes.IsSidecar(path))
            {
                supported.Add(path);
                continue;
            }

            log.LogWarning($"Skipped (unsupported type): {path}");
            counts.Unsupported++;
        }

        return supported;
    }

    private List<PlannedGroup> Plan(List<string> files, Counts counts, IProgress<ProcessingProgress>? progress, CancellationToken cancellationToken)
    {
        var planned = new List<PlannedGroup>();
        var done = 0;

        // A photo, its RAW/Live Photo companions and its sidecars share a stem and are renamed together.
        foreach (var group in files.GroupBy(MediaTypes.GetStem, StringComparer.OrdinalIgnoreCase))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return [];
            }

            var paths = group.ToList();
            if (!paths.Any(path => MediaTypes.GetKind(path) != null))
            {
                foreach (var path in paths)
                {
                    log.LogWarning($"Skipped (sidecar without media file): {path}");
                    counts.Unsupported++;
                }
            }
            else
            {
                try
                {
                    planned.Add(PlanGroup(group.Key, paths));
                }
                catch (Exception ex)
                {
                    log.LogWarning($"Failed: {string.Join(", ", paths)}");
                    log.LogError(ex);
                    counts.Failed += paths.Count;
                }
            }

            done += paths.Count;
            progress?.Report(new ProcessingProgress("Reading metadata", done, files.Count));
        }

        return planned;
    }

    private PlannedGroup PlanGroup(string stem, IReadOnlyList<string> paths)
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
        var withModel = media.FirstOrDefault(m => m.Metadata.Model != null);
        var model = primary.Metadata.Model ?? withModel.Metadata?.Model;
        var make = primary.Metadata.Model != null ? primary.Metadata.Make : withModel.Metadata?.Make;

        var context = new NameContext(
            kind == MediaKind.Video ? config.VideoPrefix : config.ImagePrefix,
            primary.Date.Value,
            deviceResolver.Resolve(model),
            make,
            model,
            stem);

        var members = paths
            .Select(path => new PlannedMember(path, MediaTypes.GetOutputExtension(path)))
            .ToList();

        if (members.GroupBy(m => m.Extension).Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException("Several files in the group would get the same name.");
        }

        return new PlannedGroup(stem, members, context, primary.Date.Source, SubSecondSortKey(primary.Metadata.SubSecondOriginal));
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
            log.LogWarning($"Could not read metadata of {path}: {ex.Message}");
            return MediaMetadata.Empty;
        }
    }

    /// <summary>
    /// Indexes files already in the output directory, except those this run is about to rename.
    /// </summary>
    private static void IndexOutputDirectory(Run run, List<PlannedGroup> pending)
    {
        if (!Directory.Exists(run.OutputDirectory))
        {
            return;
        }

        var moving = pending
            .SelectMany(g => g.Members)
            .Select(m => m.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.GetFiles(run.OutputDirectory).Where(p => !moving.Contains(p)))
        {
            run.Duplicates.Add(path, path);
        }
    }

    private void Execute(PlannedGroup group, Run run)
    {
        var toPlace = new List<PlannedMember>();
        foreach (var member in group.Members)
        {
            if (run.Duplicates.FindIdentical(member.SourcePath) is { } existing)
            {
                log.LogMessage($"Duplicate of {existing}, left in place: {member.SourcePath}");
                run.Counts.Duplicates++;
            }
            else
            {
                toPlace.Add(member);
            }
        }

        if (toPlace.Count == 0)
        {
            return;
        }

        for (var counter = 0; ; counter++)
        {
            var stem = template.Render(group.Context, counter);
            var targets = toPlace
                .Select(m => (Member: m, Target: Path.Combine(run.OutputDirectory, stem + m.Extension)))
                .ToList();

            if (targets.Any(t => !FileComparer.PathsEqual(t.Member.SourcePath, t.Target) && run.FileSystem.Exists(t.Target)))
            {
                continue;
            }

            foreach (var (member, target) in targets)
            {
                if (FileComparer.PathsEqual(member.SourcePath, target))
                {
                    run.Counts.AlreadyNamed++;
                    continue;
                }

                Transfer(member.SourcePath, target, run);
                log.LogMessage($"{(run.DryRun ? "Would rename " : "")}{Path.GetFileName(member.SourcePath)} -> {Path.GetFileName(target)} [{group.DateSource}]");
                run.Counts.Renamed++;
            }

            return;
        }
    }

    private void Transfer(string source, string target, Run run)
    {
        var copy = config.Options.CopyingEnabled;

        if (run.DryRun)
        {
            run.FileSystem.RecordPlanned(source, target, move: !copy);
            run.Duplicates.Add(target, source);
            return;
        }

        if (copy)
        {
            File.Copy(source, target);
        }
        else
        {
            File.Move(source, target);
        }

        run.Journal.Record(copy ? JournalOperation.Copy : JournalOperation.Move, source, target);
        run.Duplicates.Add(target, target);
    }

    /// <summary>
    /// EXIF sub-seconds are a decimal fraction ("5" means .5), so right-pad to compare them as strings.
    /// </summary>
    private static string SubSecondSortKey(string? subSecond)
        => subSecond != null && subSecond.All(char.IsAsciiDigit) ? subSecond.PadRight(9, '0') : "";

    private sealed class Counts
    {
        public int Renamed;
        public int AlreadyNamed;
        public int Duplicates;
        public int Unsupported;
        public int Failed;
    }

    private sealed record Run(bool DryRun, string OutputDirectory, Counts Counts, FileSystemView FileSystem, RunJournal Journal, DuplicateIndex Duplicates);

    private sealed record PlannedMember(string SourcePath, string Extension);

    private sealed record PlannedGroup(string Stem, IReadOnlyList<PlannedMember> Members, NameContext Context, DateSource DateSource, string SubSecondSortKey);
}
