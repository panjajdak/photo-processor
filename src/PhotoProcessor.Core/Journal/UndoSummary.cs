namespace PhotoProcessor.Core.Journal;

/// <summary>Number of journal entries per outcome of an undo.</summary>
public sealed record UndoSummary(int Restored, int AlreadyRestored, int Skipped, bool Cancelled = false);
