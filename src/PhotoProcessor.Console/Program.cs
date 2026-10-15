namespace PhotoProcessor.Console;

using System.CommandLine;
using System.Text.Json;
using PhotoProcessor.Core;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Journal;

internal static class Program
{
    private static class ExitCode
    {
        public const int Success = 0;
        public const int CompletedWithFailures = 1;
        public const int InvalidUsage = 2;
        public const int Crashed = 3;
    }

    private static readonly Option<string?> ConfigOption = new("--config", "-c")
    {
        Description = "Path to config.json. Default: config.json next to the executable.",
        Recursive = true,
    };

    private static readonly Option<bool> DryRunOption = new("--dry-run", "-n")
    {
        Description = "Show what would happen without changing any file.",
        Recursive = true,
    };

    private static readonly Option<bool> VerboseOption = new("--verbose", "-v")
    {
        Description = "List every file that is renamed, restored or skipped as a duplicate.",
        Recursive = true,
    };

    private static int Main(string[] args)
    {
        var input = new Argument<string?>("input")
        {
            Description = "Folder with photos and videos. Default: InputDirectory from config.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var output = new Argument<string?>("output")
        {
            Description = "Folder to move renamed files to. Default: the input folder when input is given, otherwise OutputDirectory from config.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var copy = new Option<bool>("--copy")
        {
            Description = "Copy files instead of moving them (overrides Options.CopyingEnabled).",
        };

        var root = new RootCommand("Renames photos and videos to \"<type> <date taken> <device>\" using their metadata.")
        {
            input, output, copy, ConfigOption, DryRunOption, VerboseOption,
        };
        root.SetAction(parse => Rename(parse.GetValue(input), parse.GetValue(output), parse.GetValue(copy), parse));

        var target = new Argument<string?>("target")
        {
            Description = "Journal file, or output folder to undo its latest run. Default: OutputDirectory from config.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var undo = new Command("undo", "Reverts a previous run using its journal.") { target };
        undo.SetAction(parse => Undo(parse.GetValue(target), parse));
        root.Add(undo);

        return root.Parse(args).Invoke();
    }

    private static int Rename(string? input, string? output, bool copy, ParseResult parse)
    {
        var reporter = new ConsoleReporter(parse.GetValue(VerboseOption));
        try
        {
            var config = ReadConfig(parse.GetValue(ConfigOption));
            if (copy)
            {
                config.Options.CopyingEnabled = true;
            }

            var inputDirectory = input ?? config.InputDirectory;
            var outputDirectory = output ?? (input != null ? input : config.OutputDirectory ?? config.InputDirectory);
            if (string.IsNullOrWhiteSpace(inputDirectory) || string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new UsageException("No input folder given and no InputDirectory in config.");
            }

            if (!Directory.Exists(inputDirectory))
            {
                throw new UsageException($"Input folder does not exist: {inputDirectory}");
            }

            var dryRun = parse.GetValue(DryRunOption);
            var processor = new DirectoryProcessor(config, reporter);
            var result = processor.Process(inputDirectory, outputDirectory, dryRun, reporter);
            reporter.ClearProgress();

            var s = result.Summary;
            System.Console.WriteLine(
                $"{(dryRun ? "Dry run, nothing was changed. Would rename" : "Renamed")}: {s.Renamed}, already named: {s.AlreadyNamed}, " +
                $"duplicates: {s.Duplicates}, unsupported: {s.Unsupported}, failed: {s.Failed}");

            if (result.JournalPath != null)
            {
                System.Console.WriteLine($"Undo this run with: {AppName} undo \"{result.JournalPath}\"");
            }

            return s.Failed > 0 ? ExitCode.CompletedWithFailures : ExitCode.Success;
        }
        catch (Exception ex) when (ex is UsageException or FormatException or JsonException or TimeZoneNotFoundException)
        {
            reporter.ClearProgress();
            System.Console.Error.WriteLine(ex.Message);
            return ExitCode.InvalidUsage;
        }
        catch (Exception ex)
        {
            reporter.ClearProgress();
            System.Console.Error.WriteLine($"Stopped: {ex.Message}");
            return ExitCode.Crashed;
        }
    }

    private static int Undo(string? target, ParseResult parse)
    {
        var reporter = new ConsoleReporter(parse.GetValue(VerboseOption));
        try
        {
            var journal = ResolveJournal(target, parse.GetValue(ConfigOption));
            var dryRun = parse.GetValue(DryRunOption);

            var summary = new Undoer(reporter).Undo(journal, dryRun);

            System.Console.WriteLine(
                $"{(dryRun ? "Dry run, nothing was changed. Would restore" : "Restored")}: {summary.Restored}, " +
                $"already restored: {summary.AlreadyRestored}, skipped: {summary.Skipped}");
            if (summary.Skipped > 0 && !dryRun)
            {
                System.Console.WriteLine("Some files were skipped; the journal was kept so undo can be retried after fixing them.");
            }

            return summary.Skipped > 0 ? ExitCode.CompletedWithFailures : ExitCode.Success;
        }
        catch (Exception ex) when (ex is UsageException or JsonException)
        {
            System.Console.Error.WriteLine(ex.Message);
            return ExitCode.InvalidUsage;
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine($"Stopped: {ex.Message}");
            return ExitCode.Crashed;
        }
    }

    private static string ResolveJournal(string? target, string? configPath)
    {
        if (target != null && File.Exists(target))
        {
            return target;
        }

        var directory = target;
        if (directory == null)
        {
            var config = ReadConfig(configPath);
            directory = config.OutputDirectory ?? config.InputDirectory
                ?? throw new UsageException("No folder given and no OutputDirectory in config.");
        }

        if (!Directory.Exists(directory))
        {
            throw new UsageException($"Not a journal file or folder: {directory}");
        }

        return Undoer.FindLatestJournal(directory)
            ?? throw new UsageException($"No run left to undo in {directory}");
    }

    private static Configuration ReadConfig(string? path)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "config.json");
        if (!File.Exists(path))
        {
            throw new UsageException($"Config does not exist: {path}");
        }

        try
        {
            return JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path))
                ?? throw new UsageException($"Config is empty: {path}");
        }
        catch (JsonException ex)
        {
            throw new UsageException($"Config {path} is not valid JSON: {ex.Message}");
        }
    }

    private static string AppName => Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "PhotoProcessor.Console";

    private sealed class UsageException(string message) : Exception(message);
}
