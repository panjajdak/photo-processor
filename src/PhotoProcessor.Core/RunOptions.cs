namespace PhotoProcessor.Core;

/// <summary>How a rename run behaves.</summary>
/// <param name="DryRun">Report what would happen without changing any file.</param>
/// <param name="Recursive">Also take files from subfolders of the input folder; they all end up in the flat output folder.</param>
public sealed record RunOptions(bool DryRun = false, bool Recursive = false);
