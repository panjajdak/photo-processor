namespace PhotoProcessor.Core.Journal;

using System;

internal enum JournalOperation
{
    Move,
    Copy,
}

/// <summary>
/// One file operation of a run, as stored in the run journal.
/// </summary>
/// <param name="Size">Size of the file at <paramref name="To"/> right after the operation.</param>
/// <param name="ModifiedUtc">Last write time of the file at <paramref name="To"/> right after the operation.</param>
internal sealed record JournalEntry(JournalOperation Op, string From, string To, long Size, DateTime ModifiedUtc);
