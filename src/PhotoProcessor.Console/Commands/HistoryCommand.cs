namespace PhotoProcessor.Console.Commands;

using System.CommandLine;
using System.Globalization;
using PhotoProcessor.Console.Cli;
using PhotoProcessor.Core.Journal;

internal static class HistoryCommand
{
    public static Command Create()
    {
        var folder = new Argument<DirectoryInfo>("folder")
        {
            Description = "Output folder to list the runs of. Default: OutputDirectory from the config.",
            Arity = ArgumentArity.ZeroOrOne,
        }.AcceptExistingOnly();

        var command = new Command("history", "List previous runs that wrote to a folder and whether they were undone.")
        {
            folder,
        };

        command.SetAction(parse => Run(parse.GetValue(folder), parse));

        var app = Program.AppName;
        HelpSections.Add(command, $"""
            {HelpSections.Examples(
                ($"{app} history", "Runs in OutputDirectory from the config"),
                ($@"{app} history D:\Photos", @"Runs that wrote to D:\Photos"))}

            Runs are stored in "<folder>\.photoprocessor\runs". A run that only skipped files leaves no entry.
            """);

        return command;
    }

    private static int Run(DirectoryInfo? folder, ParseResult parse)
    {
        var reporter = new ConsoleReporter(CommonOptions.GetVerbosity(parse));
        try
        {
            string directory;
            if (folder != null)
            {
                directory = folder.FullName;
            }
            else
            {
                var config = ConfigLoader.Load(parse.GetValue(CommonOptions.Config));
                directory = config.OutputDirectory ?? config.InputDirectory
                    ?? throw new UsageException("No folder given and no OutputDirectory in the config.");
            }

            var runs = RunHistory.List(directory);
            if (runs.Count == 0)
            {
                reporter.Result($"No runs recorded in {directory}");
                return ExitCodes.Success;
            }

            reporter.Result($"Runs in {directory}, oldest first:");
            reporter.Result("");
            reporter.Result($"  {"Started",-19}  {"Moved",6}  {"Copied",6}  {"Status",-8}  Journal");
            foreach (var run in runs)
            {
                reporter.Result(
                    $"  {run.Started.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),-19}  {run.Moved,6}  {run.Copied,6}  " +
                    $"{(run.Undone ? "undone" : "active"),-8}  {Path.GetFileName(run.JournalPath)}");
            }

            var latest = runs.LastOrDefault(r => !r.Undone);
            if (latest != null)
            {
                reporter.Result("");
                reporter.Result($"Revert the latest active run with: {Program.AppName} undo \"{directory}\"");
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            return Errors.Report(ex, reporter);
        }
    }
}
