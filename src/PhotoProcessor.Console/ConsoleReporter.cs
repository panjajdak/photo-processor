namespace PhotoProcessor.Console;

using PhotoProcessor.Core;
using PhotoProcessor.Core.Abstractions;

/// <summary>
/// Writes log lines to stdout and a single, updating progress line to stderr when it is a terminal.
/// </summary>
internal sealed class ConsoleReporter : ILog, IProgress<ProcessingProgress>
{
    private readonly bool verbose;
    private readonly bool showProgress = !System.Console.IsErrorRedirected;
    private readonly object sync = new();
    private int progressLength;
    private string? lastProgress;

    public ConsoleReporter(bool verbose)
    {
        this.verbose = verbose;
    }

    public void LogMessage(string message)
    {
        if (verbose)
        {
            WriteLine(System.Console.Out, message);
        }
    }

    public void LogWarning(string message) => WriteLine(System.Console.Out, "Warning: " + message);

    public void LogError(Exception ex) => WriteLine(System.Console.Error, (verbose ? ex.ToString() : ex.Message));

    public void Report(ProcessingProgress value)
    {
        if (!showProgress || value.Total == 0)
        {
            return;
        }

        var percent = value.Done * 100 / value.Total;
        var text = $"{value.Stage}: {value.Done}/{value.Total} ({percent}%)";
        lock (sync)
        {
            if (text == lastProgress)
            {
                return;
            }

            lastProgress = text;
            System.Console.Error.Write("\r" + text.PadRight(progressLength));
            progressLength = text.Length;
        }
    }

    /// <summary>
    /// Removes the progress line so the next output starts on a clean line.
    /// </summary>
    public void ClearProgress()
    {
        lock (sync)
        {
            if (progressLength > 0)
            {
                System.Console.Error.Write("\r" + new string(' ', progressLength) + "\r");
                progressLength = 0;
                lastProgress = null;
            }
        }
    }

    private void WriteLine(TextWriter writer, string message)
    {
        lock (sync)
        {
            var progress = lastProgress;
            ClearProgress();
            writer.WriteLine(message);
            if (progress != null && showProgress)
            {
                System.Console.Error.Write(progress);
                progressLength = progress.Length;
                lastProgress = progress;
            }
        }
    }
}
