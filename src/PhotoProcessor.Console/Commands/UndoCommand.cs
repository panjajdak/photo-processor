namespace PhotoProcessor.Console.Commands;

using System.CommandLine;
using PhotoProcessor.Console.Cli;
using PhotoProcessor.Core.Journal;

internal static class UndoCommand
{
    public static Command Create(CancellationToken cancellationToken)
    {
        var target = new Argument<FileSystemInfo>("target")
        {
            Description = "Output folder whose latest run to revert, or a specific journal file (*.jsonl). "
                + "Default: OutputDirectory from the config.",
            Arity = ArgumentArity.ZeroOrOne,
        }.AcceptExistingOnly();

        var dryRun = CommonOptions.DryRun("Show what would be restored without changing any file.");

        var command = new Command("undo", "Revert a previous run: move renamed files back, or remove copies.")
        {
            target,
            dryRun,
        };

        command.SetAction(parse => Run(parse.GetValue(target), parse.GetValue(dryRun), parse, cancellationToken));

        var app = Program.AppName;
        HelpSections.Add(command, $"""
            {HelpSections.Examples(
                ($"{app} undo", "Revert the latest run in OutputDirectory from the config"),
                ($@"{app} undo D:\Photos", @"Revert the latest run that wrote to D:\Photos"),
                ($@"{app} undo D:\Photos --dry-run", "Preview what would be restored"),
                ($@"{app} undo ""D:\Photos\.photoprocessor\runs\20261015-215318-344.jsonl""", $"Revert one specific run (see \"{app} history\")"))}

            Safety:
              - A file is only moved back if it is unchanged since the run and nothing else took its old name.
              - A copy is only removed while the original is still there with identical content.
              - Anything skipped is listed; the run stays undoable, so undo can be repeated after fixing it.
              - Runs are undone newest first; repeat "{app} undo" to step further back.

            {HelpSections.ExitCodeList([
                (ExitCodes.Success, "Everything was restored."),
                (ExitCodes.PartialFailure, "Some files were skipped (listed above the summary)."),
                (ExitCodes.InvalidUsage, "No run to undo, or invalid arguments."),
                (ExitCodes.Error, "An unexpected error stopped the undo."),
                (ExitCodes.Cancelled, "Stopped with Ctrl+C; run undo again to continue.")])}
            """);

        return command;
    }

    private static int Run(FileSystemInfo? target, bool dryRun, ParseResult parse, CancellationToken cancellationToken)
    {
        var reporter = new ConsoleReporter(CommonOptions.GetVerbosity(parse));
        try
        {
            var journal = ResolveJournal(target, parse);
            var summary = new Undoer(reporter).Undo(journal, dryRun, cancellationToken);

            if (summary.Cancelled)
            {
                reporter.Result("Stopped before all files were restored; run undo again to continue.");
            }

            reporter.Result(
                $"{(dryRun ? "Dry run, nothing was changed. Would restore" : "Restored")}: {summary.Restored}, " +
                $"already restored: {summary.AlreadyRestored}, skipped: {summary.Skipped}");

            if (summary.Skipped > 0 && !dryRun && !summary.Cancelled)
            {
                reporter.Result("The run was kept, so undo can be repeated after fixing the skipped files.");
            }

            return summary.Cancelled ? ExitCodes.Cancelled
                : summary.Skipped > 0 ? ExitCodes.PartialFailure
                : ExitCodes.Success;
        }
        catch (Exception ex)
        {
            return Errors.Report(ex, reporter);
        }
    }

    private static string ResolveJournal(FileSystemInfo? target, ParseResult parse)
    {
        if (target is FileInfo file)
        {
            return file.FullName;
        }

        string directory;
        if (target is DirectoryInfo folder)
        {
            directory = folder.FullName;
        }
        else
        {
            var config = ConfigLoader.Load(parse.GetValue(CommonOptions.Config));
            directory = config.OutputDirectory ?? config.InputDirectory
                ?? throw new UsageException("No folder given and no OutputDirectory in the config.");
        }

        return Undoer.FindLatestJournal(directory)
            ?? throw new UsageException($"No run left to undo in {directory}");
    }
}
