namespace PhotoProcessor.Core.Commands;

using System.IO;
using PhotoProcessor.Core.Dating;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.IO;
using PhotoProcessor.Core.Journal;

internal sealed record MoveFileCommand(string From, string To) : IFileCommand
{
    public JournalOperation Operation => JournalOperation.Move;

    public ExecutedCommand Execute(IFileSystem fileSystem)
    {
        fileSystem.Move(From, To);
        return ExecutedCommand.Capture(this, fileSystem);
    }

    public UndoOutcome Undo(ExecutedCommand executed, IFileSystem fileSystem, bool dryRun)
    {
        if (!fileSystem.FileExists(To))
        {
            return fileSystem.FileExists(From) && fileSystem.GetFacts(From).Length == executed.Size
                ? UndoOutcome.AlreadyRestored
                : UndoOutcome.Skipped("file is no longer there");
        }

        if (!executed.TargetIsUnchanged(fileSystem))
        {
            return UndoOutcome.Skipped("file was modified after the run");
        }

        if (fileSystem.FileExists(From))
        {
            return UndoOutcome.Skipped($"another file now exists at {From}");
        }

        if (!dryRun)
        {
            fileSystem.CreateDirectory(Path.GetDirectoryName(From)!);
            fileSystem.Move(To, From);
        }

        return UndoOutcome.Restored;
    }

    public ProcessingEvent ToEvent(DateSource dateSource, bool dryRun) => new FileRenamed(From, To, dateSource, dryRun);
}
