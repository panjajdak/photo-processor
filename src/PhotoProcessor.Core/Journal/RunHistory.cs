namespace PhotoProcessor.Core.Journal;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PhotoProcessor.Core.IO;

/// <summary>Lists the runs recorded in an output folder.</summary>
public static class RunHistory
{
    /// <summary>Runs recorded in the output folder, oldest first.</summary>
    public static IReadOnlyList<RunInfo> List(string outputDirectory) => List(PhysicalFileSystem.Instance, outputDirectory);

    internal static IReadOnlyList<RunInfo> List(IFileSystem fileSystem, string outputDirectory)
        => RunJournal.ListAll(fileSystem, outputDirectory)
            .Select(path =>
            {
                var operations = RunJournal.Read(fileSystem, path).Select(e => e.Command.Operation).ToList();
                return new RunInfo(
                    path,
                    ParseStarted(path, fileSystem),
                    operations.Count(op => op == JournalOperation.Move),
                    operations.Count(op => op == JournalOperation.Copy),
                    RunJournal.IsUndone(path));
            })
            .ToList();

    private static DateTime ParseStarted(string path, IFileSystem fileSystem)
        => DateTime.TryParseExact(
            Path.GetFileName(path)[..RunJournal.TimestampFormat.Length],
            RunJournal.TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var started)
            ? started
            : fileSystem.GetFacts(path).LastWriteTime;
}
