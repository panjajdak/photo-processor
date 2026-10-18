namespace PhotoProcessor.Console.Commands;

using System.CommandLine;
using PhotoProcessor.Console.Cli;
using PhotoProcessor.Console.Settings;
using PhotoProcessor.Core.Config;

internal static class ConfigCommand
{
    public static Command Create()
    {
        var json = new Option<bool>("--json") { Description = "Print the effective settings as JSON, defaults included." };
        var show = new Command("show", "Show the config in use with example file names, and check it.") { json };
        show.SetAction(parse => Show(parse, parse.GetValue(json)));

        var command = new Command("config", "Show, check and change the configuration.")
        {
            show,
            CreatePath(),
            CreateEdit(),
            CreateInit(),
            CreateValidate(),
            CreateSet(),
            ConfigDeviceCommand.Create(),
        };
        command.SetAction(parse => Show(parse, asJson: false));

        var app = Program.AppName;
        HelpSections.Add(command, $$$"""
            {{{HelpSections.Examples(
                ($"{app} config", "Show the config in use and check it (same as \"config show\")"),
                ($"{app} config edit", "Open the config in your editor"),
                ($"{app} config set TimeZone Europe/Warsaw", "Change one setting"),
                ($"{app} config device add SM-S911B S23", "Name a camera model in file names"),
                ($"{app} config init", "Create the user config from the defaults"))}}}

            Config file lookup, first match wins:
              1. --config <file>
              2. the {{{ConfigStore.EnvironmentVariable}}} environment variable
              3. the user config: {{{ConfigStore.Default.UserConfigPath}}}
              4. config.json next to the executable (portable installs)
              Without any of these, built-in defaults are used; "config edit" or "config init" creates the user config.

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
                (ExitCodes.Success, "Done; the config is valid."),
                (ExitCodes.InvalidUsage, "The config is missing, not valid JSON, has errors (listed), or the change was rejected.")])}}}
            """);

        return command;
    }

    private static Command CreatePath()
    {
        var command = new Command("path", "Print the path of the config file in use.");
        command.SetAction(parse => Run(parse, reporter =>
        {
            var location = ConfigStore.Default.Locate(parse.GetValue(CommonOptions.Config));
            reporter.Result(location.Path);
            if (!location.Exists)
            {
                reporter.Note($"Not created yet; built-in defaults are used. Create it with \"{Program.AppName} config init\".");
            }

            return ExitCodes.Success;
        }));
        return command;
    }

    private static Command CreateEdit()
    {
        var command = new Command("edit", "Open the config file in your editor, creating it from the defaults if needed.");
        command.SetAction(parse => Run(parse, reporter =>
        {
            var configOption = parse.GetValue(CommonOptions.Config);
            var location = ConfigStore.Default.Locate(configOption);
            if (!location.Exists)
            {
                ConfigStore.Default.CreateDefault(location.Path, overwrite: false);
                reporter.Result($"Created {location.Path} from the defaults.");
            }

            if (EditorLauncher.ConfiguredEditor is { } editor)
            {
                EditorLauncher.OpenAndWait(editor, location.Path);
                return ReportValidity(reporter, ConfigStore.Default.Load(configOption));
            }

            EditorLauncher.OpenWithDefaultApp(location.Path);
            reporter.Result($"Opened {location.Path}");
            reporter.Result($"Run \"{Program.AppName} config validate\" after saving to check it.");
            return ExitCodes.Success;
        }));

        HelpSections.Add(command, $"""
            The editor is taken from the VISUAL or EDITOR environment variable, e.g. "code --wait" or "notepad";
            {Program.AppName} then waits for it to close and checks the config. Without either, the file opens in
            the default app for .json files (Notepad if there is none).
            """);
        return command;
    }

    private static Command CreateInit()
    {
        var force = new Option<bool>("--force") { Description = "Replace an existing config with the defaults." };
        var command = new Command("init", "Create the config file from the defaults.") { force };
        command.SetAction(parse => Run(parse, reporter =>
        {
            var configOption = parse.GetValue(CommonOptions.Config);
            var path = configOption?.FullName ?? ConfigStore.Default.UserConfigPath;
            ConfigStore.Default.CreateDefault(path, parse.GetValue(force));
            reporter.Result($"Created {path}");
            return ExitCodes.Success;
        }));
        return command;
    }

    private static Command CreateValidate()
    {
        var command = new Command("validate", "Check the config without printing it; the exit code tells whether it is valid.");
        command.SetAction(parse => Run(parse, reporter =>
        {
            ConfigurationCheck.EnsureValid(ConfigStore.Default.Load(parse.GetValue(CommonOptions.Config)));
            return ExitCodes.Success;
        }));
        return command;
    }

    private static Command CreateSet()
    {
        var key = new Argument<string>("key") { Description = $"Setting to change: {string.Join(", ", SettingKeys.Names)}." };
        var value = new Argument<string>("value") { Description = "New value; \"\" clears folders and the time zone." };
        var command = new Command("set", "Change one setting in the config file.") { key, value };
        command.SetAction(parse => Run(parse, reporter =>
        {
            var configOption = parse.GetValue(CommonOptions.Config);
            var location = ConfigStore.Default.Locate(configOption);
            var config = ConfigStore.Default.LoadOrDefaults(configOption);

            var name = SettingKeys.Set(config, parse.GetValue(key)!, parse.GetValue(value)!);
            ConfigStore.Save(location.Path, config);

            reporter.Result($"{name} = {SettingKeys.Get(config, name) ?? "(not set)"}  ({location.Path})");
            return ExitCodes.Success;
        }));

        var app = Program.AppName;
        HelpSections.Add(command, $"""
            {HelpSections.Examples(
                ($@"{app} config set InputDirectory \\NAS\photos\in", "Default folder for \"rename\""),
                ($"{app} config set TimeZone Europe/Warsaw", "Time zone for video times"),
                ($"{app} config set TimeZone \"\"", "Back to the system time zone"),
                ($"{app} config set FileNameTemplate \"{{date:yyyyMMdd_HHmmss}}[_{{device}}]\"", "New name pattern"),
                ($"{app} config set CopyingEnabled true", "Copy instead of move by default"))}

            The new value is checked before it is saved. Saving rewrites the file, so comments in it are lost;
            use "{app} config edit" to keep them.
            """);
        return command;
    }

    private static int Show(ParseResult parse, bool asJson) => Run(parse, reporter =>
    {
        var configOption = parse.GetValue(CommonOptions.Config);
        var location = ConfigStore.Default.Locate(configOption);
        var config = ConfigStore.Default.Load(configOption);

        if (asJson)
        {
            reporter.Result(ConfigStore.ToJson(config));
            return ConfigurationCheck.Validate(config).Count == 0 ? ExitCodes.Success : ExitCodes.InvalidUsage;
        }

        var defaults = new Configuration();
        reporter.Result($"Config file:      {location.Path} ({location.Describe()})");
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
        ConfigDeviceCommand.PrintMapping(reporter, config, heading: "Device mapping:   ");
        reporter.Result("");
        return ReportValidity(reporter, config);
    });

    private static int ReportValidity(ConsoleReporter reporter, Configuration config)
    {
        ConfigurationCheck.EnsureValid(config);
        reporter.Result("The config is valid.");
        return ExitCodes.Success;
    }

    internal static int Run(ParseResult parse, Func<ConsoleReporter, int> action)
    {
        var reporter = new ConsoleReporter(CommonOptions.GetVerbosity(parse));
        try
        {
            return action(reporter);
        }
        catch (Exception ex)
        {
            return Errors.Report(ex, reporter);
        }
    }
}
