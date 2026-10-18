namespace PhotoProcessor.Console.Commands;

using System.CommandLine;
using PhotoProcessor.Console.Cli;
using PhotoProcessor.Console.Settings;
using PhotoProcessor.Core.Config;

internal static class ConfigDeviceCommand
{
    public static Command Create()
    {
        var list = new Command("list", "List the camera models with a short name.");
        list.SetAction(parse => ConfigCommand.Run(parse, reporter =>
        {
            PrintMapping(reporter, ConfigStore.Default.Load(parse.GetValue(CommonOptions.Config)), heading: null);
            return ExitCodes.Success;
        }));

        var addModel = new Argument<string>("model") { Description = "Camera model exactly as the camera writes it into the metadata, e.g. SM-S911B." };
        var addName = new Argument<string>("name") { Description = "Short name to use in file names, e.g. S23." };
        var add = new Command("add", "Map a camera model to a short name, or change its name.") { addModel, addName };
        add.SetAction(parse => Change(parse, (config, reporter) =>
        {
            var model = parse.GetValue(addModel)!.Trim();
            var name = parse.GetValue(addName)!.Trim();
            var existing = config.DeviceMapping.Keys.FirstOrDefault(k => string.Equals(k, model, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                config.DeviceMapping.Remove(existing);
            }

            config.DeviceMapping[model] = name;
            reporter.Result($"{(existing != null ? "Changed" : "Added")}: {model} -> {name}");
        }));

        var removeModel = new Argument<string>("model") { Description = "Camera model to remove." };
        var remove = new Command("remove", "Remove a camera model's short name.") { removeModel };
        remove.SetAction(parse => Change(parse, (config, reporter) =>
        {
            var model = parse.GetValue(removeModel)!.Trim();
            var existing = config.DeviceMapping.Keys.FirstOrDefault(k => string.Equals(k, model, StringComparison.OrdinalIgnoreCase))
                ?? throw new UsageException($"\"{model}\" is not in the device mapping.");
            config.DeviceMapping.Remove(existing);
            reporter.Result($"Removed: {existing}");
        }));

        var command = new Command("device", "List or change the short names used for camera models.") { list, add, remove };
        command.Action = list.Action;

        var app = Program.AppName;
        HelpSections.Add(command, $"""
            {HelpSections.Examples(
                ($"{app} config device list", "Show the mapping"),
                ($"{app} config device add SM-S911B S23", "Galaxy S23 photos get \"S23\""),
                ($"{app} config device add \"HERO7 Black\" Hero7", "Quote models with spaces"),
                ($"{app} config device remove SM-S911B", "Use the raw model again"))}

            Models are matched case-insensitively. Unmapped models appear in file names as they are.
            Saving rewrites the config file, so comments in it are lost.
            """);

        return command;
    }

    public static void PrintMapping(ConsoleReporter reporter, Configuration config, string? heading)
    {
        if (config.DeviceMapping.Count == 0)
        {
            reporter.Result($"{heading}(none)");
            return;
        }

        if (heading != null)
        {
            reporter.Result(heading.TrimEnd());
        }

        foreach (var (model, name) in config.DeviceMapping.OrderBy(m => m.Key, StringComparer.OrdinalIgnoreCase))
        {
            reporter.Result($"  {model,-32} -> {name}");
        }
    }

    private static int Change(ParseResult parse, Action<Configuration, ConsoleReporter> change)
        => ConfigCommand.Run(parse, reporter =>
        {
            var configOption = parse.GetValue(CommonOptions.Config);
            var location = ConfigStore.Default.Locate(configOption);
            var config = ConfigStore.Default.LoadOrDefaults(configOption);

            change(config, reporter);
            ConfigStore.Save(location.Path, config);
            return ExitCodes.Success;
        });
}
