namespace PhotoProcessor.Core.Abstractions;

using System;

public interface ILog
{
    public void LogMessage(string message);

    public void LogError(Exception ex);
}
