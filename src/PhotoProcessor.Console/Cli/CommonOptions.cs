namespace PhotoProcessor.Console.Cli;

using System.CommandLine;

internal static class CommonOptions
{
    public static readonly Option<FileInfo> Config = new Option<FileInfo>("--config", "-c")
    {
        Description = $"Config file to use. Default: ${ConfigLoader.EnvironmentVariable}, else config.json next to the executable.",
        HelpName = "file",
        Recursive = true,
    }.AcceptExistingOnly();

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

        foreach (var command in root.Subcommands)
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

    public static Verbosity GetVerbosity(ParseResult parse)
        => parse.GetValue(Quiet) ? Verbosity.Quiet
            : parse.GetValue(Verbose) ? Verbosity.Verbose
            : Verbosity.Normal;

    public static Option<bool> DryRun(string description) => new("--dry-run", "-n")
    {
        Description = description,
    };
}
