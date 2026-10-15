namespace PhotoProcessor.Console;

using System.CommandLine;
using System.CommandLine.Invocation;
using PhotoProcessor.Console.Cli;
using PhotoProcessor.Console.Commands;

internal static class Program
{
    /// <summary>
    /// Name the program was started as, for help text and hints.
    /// </summary>
    public static string AppName { get; } = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "photoprocessor";

    private static int Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        System.Console.CancelKeyPress += (_, e) =>
        {
            // First Ctrl+C: finish the file in progress and stop cleanly. Second: terminate immediately.
            if (!cancellation.IsCancellationRequested)
            {
                e.Cancel = true;
                System.Console.Error.WriteLine();
                System.Console.Error.WriteLine("Stopping after the current file... (press Ctrl+C again to abort immediately)");
                cancellation.Cancel();
            }
        };

        var root = BuildCommandLine(cancellation.Token);

        // Without a command there is nothing to do; show the overview instead of an error.
        var parseResult = root.Parse(args.Length == 0 ? ["--help"] : args);

        // Ctrl+C is handled above, so the parser must not install its own handler that kills the process mid-move.
        var invocation = new InvocationConfiguration { ProcessTerminationTimeout = null };

        if (parseResult.Action is ParseErrorAction parseError)
        {
            // Show the error and a pointer to help rather than burying the error under the full help text.
            parseError.ShowHelp = false;
            parseResult.Invoke(invocation);

            var command = string.Join(' ', parseResult.CommandResult.Command.Name == root.Name ? [AppName] : [AppName, parseResult.CommandResult.Command.Name]);
            System.Console.Error.WriteLine($"Run \"{command} --help\" for usage.");
            return ExitCodes.InvalidUsage;
        }

        var exitCode = parseResult.Invoke(invocation);
        return args.Length == 0 ? ExitCodes.InvalidUsage : exitCode;
    }

    private static RootCommand BuildCommandLine(CancellationToken cancellationToken)
    {
        var root = new RootCommand("Renames photos and videos from different devices to consistent, sortable names based on "
            + "when they were taken and with which device, e.g. IMG_20210408_194108.jpg -> IMG 2021-04-08 19.41.08 A41.jpg.")
        {
            RenameCommand.Create(cancellationToken),
            UndoCommand.Create(cancellationToken),
            HistoryCommand.Create(),
            ConfigCommand.Create(),
        };
        CommonOptions.AddTo(root);

        HelpSections.Add(root, $"""
            {HelpSections.Examples(
                ($@"{AppName} rename D:\Camera --dry-run", "Preview the new names"),
                ($@"{AppName} rename D:\Camera D:\Photos", @"Rename and move the files to D:\Photos"),
                ($@"{AppName} undo D:\Photos", "Revert that run"),
                ($"{AppName} config", "Show and check the configuration"))}

            Run "{AppName} <command> --help" for details, e.g. "{AppName} rename --help".

            Configuration:
              Settings live in a JSON file: --config <file>, else ${ConfigLoader.EnvironmentVariable},
              else config.json next to the executable. "{AppName} config" shows which one is used.

            {HelpSections.ExitCodeList(ExitCodes.All)}
            """);

        HelpSections.Install(root);
        return root;
    }
}
