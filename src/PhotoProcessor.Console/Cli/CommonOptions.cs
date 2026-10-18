namespace PhotoProcessor.Console.Cli;

using System.CommandLine;
using PhotoProcessor.Console.Settings;

internal static class CommonOptions
{
    public static readonly Option<FileInfo> Config = new("--config", "-c")
    {
        Description = $"Config file to use instead of ${ConfigStore.EnvironmentVariable} or the user config (see \"config --help\").",
        HelpName = "file",
        Recursive = true,
    };

    public static readonly Option<bool> Verbose = new("--verbose", "-v")
    {
        Description = "List every file that is renamed, restored or skipped, and show full error details.",
        Recursive = true,
    };

    public static readonly Option<bool> Quiet = new("--quiet", "-q")
    {
        Description = "Print only the summary and errors; no warnings or progress.",
        Recursive = true,
    };

    public static void AddTo(RootCommand root)
    {
        root.Add(Config);
        root.Add(Verbose);
        root.Add(Quiet);

        foreach (var command in AllCommands(root))
        {
            command.Validators.Add(result =>
            {
                if (result.GetValue(Verbose) && result.GetValue(Quiet))
                {
                    result.AddError("--verbose and --quiet cannot be used together.");
                }
            });
        }
    }

    private static IEnumerable<Command> AllCommands(Command parent)
        => parent.Subcommands.SelectMany(child => AllCommands(child).Prepend(child));

    public static Verbosity GetVerbosity(ParseResult parse)
        => parse.GetValue(Quiet) ? Verbosity.Quiet
            : parse.GetValue(Verbose) ? Verbosity.Verbose
            : Verbosity.Normal;

    public static Option<bool> DryRun(string description) => new("--dry-run", "-n")
    {
        Description = description,
    };
}
