namespace PhotoProcessor.Console.Settings;

using System.Reflection;
using System.Text.Json;
using PhotoProcessor.Console.Cli;
using PhotoProcessor.Core.Config;

internal sealed class ConfigStore(string userConfigPath, string portableConfigPath, Func<string, string?> getEnvironmentVariable)
{
    public const string EnvironmentVariable = "PHOTOPROCESSOR_CONFIG";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static ConfigStore Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "photoprocessor", "config.json"),
        Path.Combine(AppContext.BaseDirectory, "config.json"),
        Environment.GetEnvironmentVariable);

    public static string DefaultJson { get; } = ReadDefaultJson();

    public string UserConfigPath => userConfigPath;

    public ConfigLocation Locate(FileInfo? option)
    {
        if (option != null)
        {
            return new ConfigLocation(option.FullName, ConfigSource.Option, File.Exists(option.FullName));
        }

        var fromEnvironment = getEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            var path = Path.GetFullPath(fromEnvironment);
            return new ConfigLocation(path, ConfigSource.EnvironmentVariable, File.Exists(path));
        }

        if (File.Exists(userConfigPath))
        {
            return new ConfigLocation(userConfigPath, ConfigSource.UserProfile, Exists: true);
        }

        return File.Exists(portableConfigPath)
            ? new ConfigLocation(portableConfigPath, ConfigSource.NextToExecutable, Exists: true)
            : new ConfigLocation(userConfigPath, ConfigSource.UserProfile, Exists: false);
    }

    public Configuration Load(FileInfo? option)
    {
        var location = Locate(option);
        if (location.Exists)
        {
            return Parse(File.ReadAllText(location.Path), location.Path);
        }

        return location.Source == ConfigSource.UserProfile
            ? Parse(DefaultJson, "built-in defaults")
            : throw new UsageException($"Config file not found: {location.Path} ({location.Describe()}). Create it with \"{Program.AppName} config init\".");
    }

    public Configuration LoadOrDefaults(FileInfo? option)
        => Locate(option).Exists ? Load(option) : Parse(DefaultJson, "built-in defaults");

    public void CreateDefault(string path, bool overwrite)
    {
        if (File.Exists(path) && !overwrite)
        {
            throw new UsageException($"Config file already exists: {path}. Use --force to replace it with the defaults.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, DefaultJson);
    }

    public static void Save(string path, Configuration config)
    {
        ConfigurationCheck.EnsureValid(config);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(config, WriteOptions) + Environment.NewLine);
    }

    public static string ToJson(Configuration config) => JsonSerializer.Serialize(config, WriteOptions);

    private static Configuration Parse(string json, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<Configuration>(json, ReadOptions)
                ?? throw new UsageException($"Config file is empty: {source}");
        }
        catch (JsonException ex)
        {
            throw new UsageException($"Config file {source} is not valid JSON: {ex.Message}");
        }
    }

    private static string ReadDefaultJson()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("config.default.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
