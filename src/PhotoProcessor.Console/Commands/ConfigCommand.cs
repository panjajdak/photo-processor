namespace PhotoProcessor.Console.Commands;

using System.CommandLine;
using PhotoProcessor.Console.Cli;
using PhotoProcessor.Core.Config;

internal static class ConfigCommand
{
    public static Command Create()
    {
        var command = new Command("config", "Show which config file is used, what it contains and whether it is valid.");
        command.SetAction(Run);

        var app = Program.AppName;
        HelpSections.Add(command, $$$"""
            {{{HelpSections.Examples(
                ($"{app} config", "Show and check the config in use"),
                ($@"{app} config -c D:\other.json", "Show and check another config file"))}}}

            Config file lookup, first match wins:
              1. --config <file>
              2. the {{{ConfigLoader.EnvironmentVariable}}} environment variable
              3. config.json next to {{{app}}}.exe

            Template tokens for FileNameTemplate:
              {type}            ImagePrefix or VideoPrefix (IMG / VID)
              {date:FORMAT}     Date taken, .NET date format, e.g. {date:yyyy-MM-dd HH.mm.ss}
              {device}          Camera model mapped through DeviceMapping (A41), or the model itself
              {make} {model}    Raw camera make and model from the metadata
              {original}        Original file name without extension
              {counter:FORMAT}  Number for files that would get the same name; empty for the first one
              [ ... ]           Optional part, left out when any token inside has no value
              {{ }} [[ ]]       Literal braces and brackets
              If the template has no {counter}, " {counter}" is added at the end.

            {{{HelpSections.ExitCodeList([
                (ExitCodes.Success, "The config is valid."),
                (ExitCodes.InvalidUsage, "The config is missing, not valid JSON, or has errors (listed).")])}}}
            """);

        return command;
    }

    private static int Run(ParseResult parse)
    {
        var reporter = new ConsoleReporter(CommonOptions.GetVerbosity(parse));
        try
        {
            var configOption = parse.GetValue(CommonOptions.Config);
            var (path, source) = ConfigLoader.Locate(configOption);
            var config = ConfigLoader.Load(configOption);
            var defaults = new Configuration();

            reporter.Result($"Config file:      {path} ({source})");
            reporter.Result($"Input folder:     {config.InputDirectory ?? "(not set; pass one on the command line)"}");
            reporter.Result($"Output folder:    {config.OutputDirectory ?? "(not set; same as input)"}");
            reporter.Result($"Mode:             {(config.Options.CopyingEnabled ? "copy" : "move")}");
            reporter.Result($"Time zone:        {(string.IsNullOrWhiteSpace(config.TimeZone) ? $"system ({TimeZoneInfo.Local.Id})" : config.TimeZone)}");
            reporter.Result($"Prefixes:         images \"{config.ImagePrefix}\", videos \"{config.VideoPrefix}\"");
            reporter.Result($"File name:        {config.FileNameTemplate}{(config.FileNameTemplate == defaults.FileNameTemplate ? " (default)" : "")}");

            var problems = ConfigurationCheck.Validate(config);
            if (problems.Count == 0)
            {
                foreach (var (description, fileName) in ConfigurationCheck.ExampleNames(config))
                {
                    reporter.Result($"  {description + ":",-34} {fileName}");
                }
            }

            reporter.Result($"Local-time video: {(config.LocalTimeVideoPatterns.Count == 0 ? "(none)" : string.Join("  ", config.LocalTimeVideoPatterns))}");
            reporter.Result($"Device mapping:   {(config.DeviceMapping.Count == 0 ? "(none)" : "")}");
            foreach (var (model, name) in config.DeviceMapping.OrderBy(m => m.Key, StringComparer.OrdinalIgnoreCase))
            {
                reporter.Result($"  {model,-32} -> {name}");
            }

            if (problems.Count > 0)
            {
                reporter.Result("");
                throw new InvalidConfigurationException(problems);
            }

            reporter.Result("");
            reporter.Result("The config is valid.");
            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            return Errors.Report(ex, reporter);
        }
    }
}
