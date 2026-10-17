namespace PhotoProcessor.Core.Commands;

internal enum UndoResult
{
    Restored,
    AlreadyRestored,
    Skipped,
}
