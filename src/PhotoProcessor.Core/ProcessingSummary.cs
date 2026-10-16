namespace PhotoProcessor.Core;

/// <summary>Number of files per outcome of a run.</summary>
public sealed record ProcessingSummary(int Renamed, int AlreadyNamed, int Duplicates, int Unsupported, int Failed);
