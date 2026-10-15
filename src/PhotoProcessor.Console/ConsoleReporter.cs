namespace PhotoProcessor.Console;

using PhotoProcessor.Core;
using PhotoProcessor.Core.Abstractions;

internal enum Verbosity
{
    Quiet,
    Normal,
    Verbose,
}

/// <summary>
/// Results go to stdout; warnings, errors and the updating progress line go to stderr, so stdout can be piped or
/// redirected cleanly. Colors are used only on a terminal and never when NO_COLOR is set.
/// </summary>
internal sealed class ConsoleReporter : ILog, IProgress<ProcessingProgress>
{
    private readonly Verbosity verbosity;
    private readonly bool showProgress;
    private readonly bool useColor;
    private readonly object sync = new();
    private int progressLength;
    private string? lastProgress;

    public ConsoleReporter(Verbosity verbosity)
    {
        this.verbosity = verbosity;
        showProgress = verbosity != Verbosity.Quiet && !System.Console.IsErrorRedirected;
        useColor = !System.Console.IsErrorRedirected && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }

    public void LogMessage(string message)
    {
        if (verbosity == Verbosity.Verbose)
        {
            Write(System.Console.Out, message, color: null);
        }
    }

    public void LogWarning(string message)
    {
        if (verbosity != Verbosity.Quiet)
        {
            Write(System.Console.Error, "warning: " + message, ConsoleColor.Yellow);
        }
    }

    public void LogError(Exception ex)
        => Write(System.Console.Error, "error: " + (verbosity == Verbosity.Verbose ? ex.ToString() : ex.Message), ConsoleColor.Red);

    /// <summary>
    /// Message that is shown even in quiet mode, e.g. the final summary.
    /// </summary>
    public void Result(string message) => Write(System.Console.Out, message, color: null);

    public void Report(ProcessingProgress value)
    {
        if (!showProgress || value.Total == 0)
        {
            return;
        }

        var text = $"{value.Stage}: {value.Done}/{value.Total} ({value.Done * 100 / value.Total}%)";
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
            }

            lastProgress = null;
        }
    }

    private void Write(TextWriter writer, string message, ConsoleColor? color)
    {
        lock (sync)
        {
            var progress = lastProgress;
            ClearProgress();

            if (color != null && useColor)
            {
                System.Console.ForegroundColor = color.Value;
                writer.WriteLine(message);
                System.Console.ResetColor();
            }
            else
            {
                writer.WriteLine(message);
            }

            if (progress != null)
            {
                System.Console.Error.Write(progress);
                progressLength = progress.Length;
                lastProgress = progress;
            }
        }
    }
}
