namespace PhotoProcessor.Core.Commands;

using PhotoProcessor.Core.Dating;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.IO;
using PhotoProcessor.Core.Journal;

internal sealed record CopyFileCommand(string From, string To) : IFileCommand
{
    public JournalOperation Operation => JournalOperation.Copy;

    public ExecutedCommand Execute(IFileSystem fileSystem)
    {
        fileSystem.Copy(From, To);
        return ExecutedCommand.Capture(this, fileSystem);
    }

    public UndoOutcome Undo(ExecutedCommand executed, IFileSystem fileSystem, bool dryRun)
    {
        if (!fileSystem.FileExists(To))
        {
            return UndoOutcome.AlreadyRestored;
        }

        if (!fileSystem.FileExists(From))
        {
            return UndoOutcome.Skipped($"original {From} is gone, keeping the copy");
        }

        if (!executed.TargetIsUnchanged(fileSystem) || !FileComparer.AreIdentical(fileSystem, From, To))
        {
            return UndoOutcome.Skipped("copy differs from the original");
        }

        if (!dryRun)
        {
            fileSystem.Delete(To);
        }

        return UndoOutcome.Restored;
    }

    public ProcessingEvent ToEvent(DateSource dateSource, bool dryRun) => new FileCopied(From, To, dateSource, dryRun);
}
