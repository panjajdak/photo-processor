namespace PhotoProcessor.Core.Tests.Support;

using PhotoProcessor.Core.Abstractions;

internal sealed class TestLog : ILog
{
    public List<string> Messages { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<Exception> Errors { get; } = [];

    public void LogMessage(string message) => Messages.Add(message);

    public void LogWarning(string message) => Warnings.Add(message);

    public void LogError(Exception ex) => Errors.Add(ex);
}
