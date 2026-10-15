namespace PhotoProcessor.Core.Abstractions;

using System;

public interface ILog
{
    /// <summary>
    /// Per-file detail such as renames and duplicates.
    /// </summary>
    public void LogMessage(string message);

    /// <summary>
    /// Something the user should look at: skipped files, unreadable metadata.
    /// </summary>
    public void LogWarning(string message);

    public void LogError(Exception ex);
}
