namespace PhotoProcessor.Core;

/// <summary>Outcome of a run, with the journal to undo it.</summary>
public sealed record ProcessingResult(ProcessingSummary Summary, string? JournalPath, bool DryRun, bool Cancelled = false);
