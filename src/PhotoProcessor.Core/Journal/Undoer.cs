namespace PhotoProcessor.Core.Journal;

using System;
using System.IO;
using PhotoProcessor.Core.Abstractions;

/// <param name="Restored">Files moved back or copies removed.</param>
/// <param name="AlreadyRestored">Entries an earlier undo already reverted.</param>
/// <param name="Skipped">Entries left alone because the files changed since the run.</param>
public sealed record UndoSummary(int Restored, int AlreadyRestored, int Skipped);

/// <summary>
/// Reverts a run from its journal, newest operation first. Never overwrites a file and never deletes
/// a copy unless the original is still there with identical content.
/// </summary>
public sealed class Undoer
{
    private readonly ILog log;

    public Undoer(ILog log)
    {
        this.log = log;
    }

    /// <summary>
    /// Finds the latest journal that has not been undone in an output directory.
    /// </summary>
    public static string? FindLatestJournal(string outputDirectory) => RunJournal.FindLatest(outputDirectory);

    public UndoSummary Undo(string journalPath, bool dryRun = false)
    {
        var entries = RunJournal.Read(journalPath);
        int restored = 0, alreadyRestored = 0, skipped = 0;

        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            try
            {
                var (outcome, reason) = entry.Op == JournalOperation.Move ? UndoMove(entry, dryRun) : UndoCopy(entry, dryRun);
                switch (outcome)
                {
                    case Outcome.Restored:
                        restored++;
                        log.LogMessage(dryRun ? $"Would {Describe(entry)}" : Capitalize(Describe(entry)));
                        break;
                    case Outcome.AlreadyRestored:
                        alreadyRestored++;
                        break;
                    case Outcome.Skipped:
                        skipped++;
                        log.LogWarning($"Skipped {entry.To}: {reason}");
                        break;
                }
            }
            catch (Exception ex)
            {
                skipped++;
                log.LogWarning($"Skipped {entry.To}: {ex.Message}");
            }
        }

        if (!dryRun && skipped == 0)
        {
            RunJournal.MarkUndone(journalPath);
        }

        return new UndoSummary(restored, alreadyRestored, skipped);
    }

    private static (Outcome, string?) UndoMove(JournalEntry entry, bool dryRun)
    {
        if (!File.Exists(entry.To))
        {
            return File.Exists(entry.From) && new FileInfo(entry.From).Length == entry.Size
                ? (Outcome.AlreadyRestored, null)
                : (Outcome.Skipped, "file is no longer there");
        }

        if (!IsUnchanged(entry))
        {
            return (Outcome.Skipped, "file was modified after the run");
        }

        if (File.Exists(entry.From))
        {
            return (Outcome.Skipped, $"another file now exists at {entry.From}");
        }

        if (!dryRun)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(entry.From)!);
            File.Move(entry.To, entry.From);
        }

        return (Outcome.Restored, null);
    }

    private static (Outcome, string?) UndoCopy(JournalEntry entry, bool dryRun)
    {
        if (!File.Exists(entry.To))
        {
            return (Outcome.AlreadyRestored, null);
        }

        // The copy may be the only one left; delete it only while the original is provably intact.
        if (!File.Exists(entry.From))
        {
            return (Outcome.Skipped, $"original {entry.From} is gone, keeping the copy");
        }

        if (!IsUnchanged(entry) || !FileComparer.AreIdentical(entry.From, entry.To))
        {
            return (Outcome.Skipped, "copy differs from the original");
        }

        if (!dryRun)
        {
            File.Delete(entry.To);
        }

        return (Outcome.Restored, null);
    }

    private static bool IsUnchanged(JournalEntry entry)
    {
        var info = new FileInfo(entry.To);
        return info.Length == entry.Size && info.LastWriteTimeUtc == entry.ModifiedUtc;
    }

    private static string Describe(JournalEntry entry) => entry.Op == JournalOperation.Move
        ? $"move back {Path.GetFileName(entry.To)} -> {entry.From}"
        : $"remove copy {entry.To}";

    private static string Capitalize(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    private enum Outcome
    {
        Restored,
        AlreadyRestored,
        Skipped,
    }
}
