namespace PhotoProcessor.Core.Journal;

using System;
using System.Threading;
using PhotoProcessor.Core.Commands;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.IO;

/// <summary>Reverts a run from its journal without overwriting or losing files.</summary>
public sealed class Undoer
{
    private readonly IProcessingObserver observer;
    private readonly IFileSystem fileSystem;

    /// <summary>Creates an undoer that reports to the observer.</summary>
    public Undoer(IProcessingObserver observer)
        : this(observer, PhysicalFileSystem.Instance)
    { }

    internal Undoer(IProcessingObserver observer, IFileSystem fileSystem)
    {
        this.observer = observer;
        this.fileSystem = fileSystem;
    }

    /// <summary>Latest journal in the output folder that has not been undone.</summary>
    public static string? FindLatestJournal(string outputDirectory)
        => RunJournal.FindLatest(PhysicalFileSystem.Instance, outputDirectory);

    /// <summary>Reverts the run recorded in the journal, newest operation first.</summary>
    public UndoSummary Undo(string journalPath, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        var executed = RunJournal.Read(fileSystem, journalPath);
        int restored = 0, alreadyRestored = 0, skipped = 0;

        for (var i = executed.Count - 1; i >= 0; i--)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new UndoSummary(restored, alreadyRestored, skipped, Cancelled: true);
            }

            var command = executed[i].Command;
            UndoOutcome outcome;
            try
            {
                outcome = executed[i].Undo(fileSystem, dryRun);
            }
            catch (Exception ex)
            {
                outcome = UndoOutcome.Skipped(ex.Message);
            }

            switch (outcome.Result)
            {
                case UndoResult.Restored:
                    restored++;
                    observer.On(new FileRestored(command.From, command.To, command is CopyFileCommand, dryRun));
                    break;
                case UndoResult.AlreadyRestored:
                    alreadyRestored++;
                    break;
                case UndoResult.Skipped:
                    skipped++;
                    observer.On(new UndoSkipped(command.To, outcome.Reason!));
                    break;
            }
        }

        if (!dryRun && skipped == 0)
        {
            RunJournal.MarkUndone(fileSystem, journalPath);
        }

        return new UndoSummary(restored, alreadyRestored, skipped);
    }
}
