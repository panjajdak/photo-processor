namespace PhotoProcessor.Console.Cli;

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

/// <summary>
/// Adds extra sections (examples, exit codes, ...) after the generated help of a command.
/// </summary>
internal static class HelpSections
{
    private static readonly Dictionary<Command, string> Sections = [];

    public static void Add(Command command, string text) => Sections[command] = text.TrimEnd();

    /// <summary>
    /// "Examples:" section with the descriptions aligned in a second column; long commands put theirs on the next line.
    /// </summary>
    public static string Examples(params (string CommandLine, string Description)[] examples)
    {
        const int MaxColumn = 50;
        var column = Math.Min(examples.Max(e => e.CommandLine.Length), MaxColumn) + 2;
        var lines = new List<string> { "Examples:" };

        foreach (var (commandLine, description) in examples)
        {
            if (commandLine.Length <= MaxColumn)
            {
                lines.Add("  " + commandLine.PadRight(column) + description);
            }
            else
            {
                lines.Add("  " + commandLine);
                lines.Add("  " + new string(' ', column) + description);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string ExitCodeList(IEnumerable<(int Code, string Meaning)> codes)
        => "Exit codes:" + Environment.NewLine + string.Join(Environment.NewLine, codes.Select(c => $"  {c.Code,-4} {c.Meaning}"));

    /// <summary>
    /// Wraps the root's help action; call after all commands are added.
    /// </summary>
    public static void Install(RootCommand root)
    {
        var helpOption = root.Options.OfType<HelpOption>().Single();
        helpOption.Action = new ExtendedHelpAction((SynchronousCommandLineAction)helpOption.Action!);
    }

    private sealed class ExtendedHelpAction(SynchronousCommandLineAction inner) : SynchronousCommandLineAction
    {
        public override bool ClearsParseErrors => true;

        public override int Invoke(ParseResult parseResult)
        {
            var result = inner.Invoke(parseResult);
            if (Sections.TryGetValue(parseResult.CommandResult.Command, out var text))
            {
                System.Console.Out.WriteLine(text);
                System.Console.Out.WriteLine();
            }

            return result;
        }
    }
}
