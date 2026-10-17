namespace PhotoProcessor.Core.Commands;

using PhotoProcessor.Core.Dating;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.IO;

internal interface IFileCommand
{
    string From { get; }

    string To { get; }

    ExecutedCommand Execute(IFileSystem fileSystem);

    UndoOutcome Undo(ExecutedCommand executed, IFileSystem fileSystem, bool dryRun);

    ProcessingEvent ToEvent(DateSource dateSource, bool dryRun);
}
