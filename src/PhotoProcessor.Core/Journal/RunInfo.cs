namespace PhotoProcessor.Core.Journal;

using System;

/// <summary>A recorded run and what it changed.</summary>
public sealed record RunInfo(string JournalPath, DateTime Started, int Moved, int Copied, bool Undone);
