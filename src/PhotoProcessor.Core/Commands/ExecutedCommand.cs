namespace PhotoProcessor.Core.Commands;

using System;
using PhotoProcessor.Core.IO;

internal sealed record ExecutedCommand(IFileCommand Command, long Size, DateTime ModifiedUtc)
{
    public static ExecutedCommand Capture(IFileCommand command, IFileSystem fileSystem)
    {
        var facts = fileSystem.GetFacts(command.To);
        return new ExecutedCommand(command, facts.Length, facts.LastWriteTimeUtc);
    }

    public UndoOutcome Undo(IFileSystem fileSystem, bool dryRun) => Command.Undo(this, fileSystem, dryRun);

    public bool TargetIsUnchanged(IFileSystem fileSystem)
    {
        var facts = fileSystem.GetFacts(Command.To);
        return facts.Length == Size && facts.LastWriteTimeUtc == ModifiedUtc;
    }
}
