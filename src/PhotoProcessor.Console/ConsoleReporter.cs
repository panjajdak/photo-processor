namespace PhotoProcessor.Console;

using PhotoProcessor.Core.Events;

internal enum Verbosity
{
    Quiet,
    Normal,
    Verbose,
}

internal sealed class ConsoleReporter : IProcessingObserver
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

    public void On(ProcessingEvent processingEvent)
    {
        switch (processingEvent)
        {
            case FileRenamed e:
                Detail($"{(e.DryRun ? "Would rename " : "")}{Path.GetFileName(e.From)} -> {Path.GetFileName(e.To)} [{e.DateSource}]");
                break;
            case FileCopied e:
                Detail($"{(e.DryRun ? "Would copy " : "")}{Path.GetFileName(e.From)} -> {Path.GetFileName(e.To)} [{e.DateSource}]");
                break;
            case DuplicateSkipped e:
                Detail($"Duplicate of {e.ExistingCopy}, left in place: {e.Path}");
                break;
            case FileRestored e:
                var action = e.WasCopy ? $"remove copy {e.To}" : $"move back {Path.GetFileName(e.To)} -> {e.From}";
                Detail(e.DryRun ? $"Would {action}" : char.ToUpperInvariant(action[0]) + action[1..]);
                break;
            case UnsupportedSkipped e:
                Warning($"Skipped (unsupported type): {e.Path}");
                break;
            case OrphanSidecarSkipped e:
                Warning($"Skipped (sidecar without media file): {e.Path}");
                break;
            case MetadataUnreadable e:
                Warning($"Could not read metadata of {e.Path}: {e.Reason}");
                break;
            case UndoSkipped e:
                Warning($"Skipped {e.Path}: {e.Reason}");
                break;
            case FileFailed e:
                Warning($"Failed: {string.Join(", ", e.Paths)}");
                Error(e.Error);
                break;
            case ProgressChanged e:
                ShowProgress(e);
                break;
        }
    }

    public void Error(Exception ex)
        => Write(System.Console.Error, "error: " + (verbosity == Verbosity.Verbose ? ex.ToString() : ex.Message), ConsoleColor.Red);

    public void Error(string message) => Write(System.Console.Error, "error: " + message, ConsoleColor.Red);

    public void Result(string message) => Write(System.Console.Out, message, color: null);

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

    private void Detail(string message)
    {
        if (verbosity == Verbosity.Verbose)
        {
            Write(System.Console.Out, message, color: null);
        }
    }

    private void Warning(string message)
    {
        if (verbosity != Verbosity.Quiet)
        {
            Write(System.Console.Error, "warning: " + message, ConsoleColor.Yellow);
        }
    }

    private void ShowProgress(ProgressChanged progress)
    {
        if (!showProgress || progress.Total == 0)
        {
            return;
        }

        var text = $"{progress.Stage}: {progress.Done}/{progress.Total} ({progress.Done * 100 / progress.Total}%)";
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
