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

        int renamed = 0, alreadyNamed = 0, duplicates = 0, unsupported = 0, failed = 0;
        var planned = new List<PlannedFile>();

        foreach (var path in Directory.GetFiles(inputDirectory).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (MediaTypes.GetKind(path) is not { } kind)
            {
                log.LogMessage($"Skipped (unsupported type): {path}");
                unsupported++;
                continue;
            }

            try
            {
                planned.Add(Plan(path, kind));
            }
            catch (Exception ex)
            {
                log.LogMessage($"Failed: {path}");
                log.LogError(ex);
                failed++;
            }
        }

        // Files that end up with the same name are numbered in shooting order, so results don't depend on directory listing order.
        var ordered = planned
            .OrderBy(f => f.Date.Value)
            .ThenBy(f => f.SubSecondSortKey, StringComparer.Ordinal)
            .ThenBy(f => Path.GetFileName(f.SourcePath), StringComparer.OrdinalIgnoreCase);

        foreach (var file in ordered)
        {
            try
            {
                switch (Execute(file, outputDirectory))
                {
                    case Outcome.Renamed: renamed++; break;
                    case Outcome.AlreadyNamed: alreadyNamed++; break;
                    case Outcome.Duplicate: duplicates++; break;
                }
            }
            catch (Exception ex)
            {
                log.LogMessage($"Failed: {file.SourcePath}");
                log.LogError(ex);
                failed++;
            }
        }

        return new ProcessingSummary(renamed, alreadyNamed, duplicates, unsupported, failed);
    }

    private PlannedFile Plan(string path, MediaKind kind)
    {
        var fileName = Path.GetFileName(path);
        MediaMetadata metadata;

        try
        {
            metadata = metadataReader.Read(path);
        }
        catch (Exception ex)
        {
            // Unreadable metadata still leaves the file name and file time to date the file.
            log.LogMessage($"Warning: could not read metadata of {path}: {ex.Message}");
            metadata = MediaMetadata.Empty;
        }

        var date = dateResolver.Resolve(kind, metadata, fileName, File.GetLastWriteTime(path));
        var device = deviceResolver.Resolve(metadata.Model);
        var baseName = namer.BuildBaseName(kind, date.Value, device);

        return new PlannedFile(path, baseName, MediaTypes.GetOutputExtension(path), date, SubSecondSortKey(metadata.SubSecondOriginal));
    }

    private Outcome Execute(PlannedFile file, string outputDirectory)
    {
        var sourceDirectory = Path.GetDirectoryName(file.SourcePath)!;
        if (PathsEqual(sourceDirectory, outputDirectory) && HasTargetName(Path.GetFileName(file.SourcePath), file))
        {
            return Outcome.AlreadyNamed;
        }

        for (var index = 0; ; index++)
        {
            var target = Path.Combine(outputDirectory, namer.BuildFileName(file.BaseName, index, file.Extension));

            if (PathsEqual(target, file.SourcePath))
            {
                return Outcome.AlreadyNamed;
            }

            if (File.Exists(target))
            {
                if (FilesAreIdentical(file.SourcePath, target))
                {
                    log.LogMessage($"Duplicate of {target}, left in place: {file.SourcePath}");
                    return Outcome.Duplicate;
                }

                continue;
            }

            if (config.Options.CopyingEnabled)
            {
                File.Copy(file.SourcePath, target);
            }
            else
            {
                File.Move(file.SourcePath, target);
            }

            log.LogMessage($"{Path.GetFileName(file.SourcePath)} -> {Path.GetFileName(target)} [{file.Date.Source}]");
            return Outcome.Renamed;
        }
    }

    /// <summary>
    /// True when the file already carries the name this run would give it, with or without a counter.
    /// </summary>
    private bool HasTargetName(string fileName, PlannedFile file)
    {
        if (!fileName.EndsWith(file.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = fileName[..^file.Extension.Length];
        if (stem.Equals(file.BaseName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var counterPrefix = file.BaseName + config.Delimiter;
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

    private enum Outcome
    {
        Renamed,
        AlreadyNamed,
        Duplicate,
    }

    private sealed record PlannedFile(string SourcePath, string BaseName, string Extension, ResolvedDate Date, string SubSecondSortKey);
}
