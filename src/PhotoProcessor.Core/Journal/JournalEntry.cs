namespace PhotoProcessor.Core.Journal;

using System;

internal sealed record JournalEntry(JournalOperation Op, string From, string To, long Size, DateTime ModifiedUtc);
