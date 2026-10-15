namespace PhotoProcessor.Core.Journal;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/// <param name="JournalPath">Journal file, usable with <see cref="Undoer.Undo"/>.</param>
/// <param name="Started">When the run started.</param>
/// <param name="Moved">Files moved by the run.</param>
/// <param name="Copied">Files copied by the run.</param>
/// <param name="Undone">Whether the run has been undone.</param>
public sealed record RunInfo(string JournalPath, DateTime Started, int Moved, int Copied, bool Undone);

public static class RunHistory
{
    /// <summary>
    /// Runs recorded in an output directory, oldest first.
    /// </summary>
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
