namespace PhotoProcessor.Console.Commands;

using System.CommandLine;
using PhotoProcessor.Console.Cli;
using PhotoProcessor.Core;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Media;

/// <summary>
/// Renames the files in a folder.
/// </summary>
internal static class RenameCommand
{
    public static Command Create(CancellationToken cancellationToken)
    {
        var input = new Argument<DirectoryInfo>("input")
        {
            Description = "Folder with the photos and videos to rename. Default: InputDirectory from the config.",
            Arity = ArgumentArity.ZeroOrOne,
        }.AcceptExistingOnly();

        var output = new Argument<DirectoryInfo>("output")
        {
            Description = "Folder to put the renamed files in; created if missing. "
                + "Default: the input folder when <input> is given (rename in place), otherwise OutputDirectory from the config.",
            Arity = ArgumentArity.ZeroOrOne,
        }.AcceptLegalFilePathsOnly();

        var copy = new Option<bool>("--copy")
        {
            Description = "Copy files instead of moving them, leaving the input folder untouched. Overrides Options.CopyingEnabled.",
        };

        var dryRun = CommonOptions.DryRun("Show what would be renamed without changing any file.");

        var command = new Command("rename", "Rename the photos and videos in a folder, optionally moving or copying them to another folder.")
        {
            input,
            output,
            copy,
            dryRun,
        };

        command.SetAction(parse => Run(
            parse.GetValue(input),
            parse.GetValue(output),
            parse.GetValue(copy),
            parse.GetValue(dryRun),
            parse,
            cancellationToken));

        var app = Program.AppName;
        var defaultTemplate = new Configuration().FileNameTemplate;
        HelpSections.Add(command, $"""
            {HelpSections.Examples(
                ($"{app} rename", "Use InputDirectory and OutputDirectory from the config"),
                ($@"{app} rename D:\Camera", @"Rename the files in D:\Camera in place"),
                ($@"{app} rename D:\Camera D:\Photos", @"Move the renamed files to D:\Photos"),
                ($@"{app} rename D:\Camera --dry-run", "Preview the new names without changing anything"),
                ($@"{app} rename D:\Camera D:\Photos --copy", @"Copy instead of move, keeping D:\Camera as it is"))}

            How files are named:
              The date taken comes from EXIF/video metadata, else from the file name (IMG_20210408_194108.jpg),
              else from the file's modified time. The device comes from the camera model, mapped to a short
              name through DeviceMapping. The name is built from FileNameTemplate, by default
                {defaultTemplate}
                -> IMG 2021-04-08 19.41.08 A41.jpg
              Run "{app} config" to see the active template with example names.

            What else happens:
              - Files with the same name (RAW+JPEG, Live Photo HEIC+MOV) and their sidecars stay together.
              - Byte-identical copies of files already in the output folder are skipped and left in place.
              - Files that already have their final name are left alone, so running twice is safe.
              - Every move or copy is recorded so the run can be reverted with "{app} undo".
              - Ctrl+C stops after the current file; everything done so far can still be undone.

            Supported files:
              Images:   {string.Join(", ", SupportedFormats.Images)}
              Videos:   {string.Join(", ", SupportedFormats.Videos)}
              Sidecars: {string.Join(", ", SupportedFormats.Sidecars)} (renamed together with their photo or video)
              Subfolders are not processed.

            {HelpSections.ExitCodeList(ExitCodes.All)}
            """);

        return command;
    }

    private static int Run(DirectoryInfo? input, DirectoryInfo? output, bool copy, bool dryRun, ParseResult parse, CancellationToken cancellationToken)
    {
        var reporter = new ConsoleReporter(CommonOptions.GetVerbosity(parse));
        try
        {
            var config = ConfigLoader.Load(parse.GetValue(CommonOptions.Config));
            if (copy)
            {
                config.Options.CopyingEnabled = true;
            }

            var inputDirectory = input?.FullName ?? config.InputDirectory;
            var outputDirectory = output?.FullName ?? (input != null ? input.FullName : config.OutputDirectory ?? config.InputDirectory);
            if (string.IsNullOrWhiteSpace(inputDirectory) || string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new UsageException($"No input folder: pass one, or set InputDirectory in the config. See \"{Program.AppName} rename --help\".");
            }

            if (!Directory.Exists(inputDirectory))
            {
                throw new UsageException($"Input folder does not exist or cannot be reached: {inputDirectory}");
            }

            if (File.Exists(outputDirectory))
            {
                throw new UsageException($"Output path is a file, not a folder: {outputDirectory}");
            }

            var processor = new DirectoryProcessor(config, reporter);
            var result = processor.Process(inputDirectory, outputDirectory, dryRun, reporter, cancellationToken);
            reporter.ClearProgress();

            PrintSummary(reporter, result, copy: config.Options.CopyingEnabled);

            return result.Cancelled ? ExitCodes.Cancelled
                : result.Summary.Failed > 0 ? ExitCodes.PartialFailure
                : ExitCodes.Success;
        }
        catch (Exception ex)
        {
            reporter.ClearProgress();
            return Errors.Report(ex, reporter);
        }
    }

    private static void PrintSummary(ConsoleReporter reporter, ProcessingResult result, bool copy)
    {
        var s = result.Summary;
        var verb = copy ? "Copied" : "Renamed";
        var prefix = result.DryRun ? $"Dry run, nothing was changed. Would be {verb.ToLowerInvariant()}" : verb;

        if (result.Cancelled)
        {
            reporter.Result("Stopped before all files were processed.");
        }

        reporter.Result(
            $"{prefix}: {s.Renamed}, already named: {s.AlreadyNamed}, duplicates skipped: {s.Duplicates}, " +
            $"unsupported: {s.Unsupported}, failed: {s.Failed}");

        if (result.JournalPath != null)
        {
            reporter.Result($"Undo with: {Program.AppName} undo \"{result.JournalPath}\"");
        }
    }
}
