namespace PhotoProcessor.Core.Journal;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>A recorded run and what it changed.</summary>
public sealed record RunInfo(string JournalPath, DateTime Started, int Moved, int Copied, bool Undone);

/// <summary>Lists the runs recorded in an output folder.</summary>
public static class RunHistory
{
    /// <summary>Runs recorded in the output folder, oldest first.</summary>
    public static IReadOnlyList<RunInfo> List(string outputDirectory)
        => RunJournal.ListAll(outputDirectory)
            .Select(path =>
            {
                var entries = RunJournal.Read(path);
                return new RunInfo(
                    path,
                    ParseStarted(path),
                    entries.Count(e => e.Op == JournalOperation.Move),
                    entries.Count(e => e.Op == JournalOperation.Copy),
                    RunJournal.IsUndone(path));
            })
            .ToList();

    private static DateTime ParseStarted(string path)
        => DateTime.TryParseExact(Path.GetFileName(path)[..RunJournal.TimestampFormat.Length], RunJournal.TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var started)
            ? started
            : File.GetCreationTime(path);
}
