namespace PhotoProcessor.Core.Abstractions;

using System;

/// <summary>Receives what happens during a run.</summary>
public interface ILog
{
    /// <summary>Per-file detail, such as a rename or a skipped duplicate.</summary>
    public void LogMessage(string message);

    /// <summary>Something the user should look at, such as an unsupported or unreadable file.</summary>
    public void LogWarning(string message);

    /// <summary>An error that made a file or the run fail.</summary>
    public void LogError(Exception ex);
}
