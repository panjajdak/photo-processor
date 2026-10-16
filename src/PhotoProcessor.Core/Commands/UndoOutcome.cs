namespace PhotoProcessor.Core.Commands;

internal sealed record UndoOutcome(UndoResult Result, string? Reason = null)
{
    public static readonly UndoOutcome Restored = new(UndoResult.Restored);

    public static readonly UndoOutcome AlreadyRestored = new(UndoResult.AlreadyRestored);

    public static UndoOutcome Skipped(string reason) => new(UndoResult.Skipped, reason);
}
