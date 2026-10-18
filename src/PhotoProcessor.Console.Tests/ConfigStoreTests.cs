namespace PhotoProcessor.Console.Tests;

using PhotoProcessor.Console.Cli;
using PhotoProcessor.Console.Settings;
using PhotoProcessor.Core.Config;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PhotoProcessorConsoleTests", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> environment = [];
    private readonly ConfigStore store;

    public ConfigStoreTests()
    {
        Directory.CreateDirectory(root);
        store = new ConfigStore(UserPath, PortablePath, name => environment.GetValueOrDefault(name));
    }

    private string UserPath => Path.Combine(root, "appdata", "photoprocessor", "config.json");

    private string PortablePath => Path.Combine(root, "app", "config.json");

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void BuiltInDefault_IsValidAndNeutral()
    {
        var config = store.Load(null);

        Assert.Empty(ConfigurationCheck.Validate(config));
        Assert.Empty(config.DeviceMapping);
        Assert.Equal(new Configuration().FileNameTemplate, config.FileNameTemplate);
    }

    [Fact]
    public void WithoutAnyFile_PointsAtTheUserConfigToCreate()
    {
        var location = store.Locate(null);

        Assert.Equal(new ConfigLocation(UserPath, ConfigSource.UserProfile, Exists: false), location);
    }

    [Fact]
    public void LookupOrder_OptionThenEnvironmentThenUserThenPortable()
    {
        Write(PortablePath, "{}");
        Assert.Equal(ConfigSource.NextToExecutable, store.Locate(null).Source);

        Write(UserPath, "{}");
        Assert.Equal(ConfigSource.UserProfile, store.Locate(null).Source);

        var fromEnvironment = Path.Combine(root, "env.json");
        environment[ConfigStore.EnvironmentVariable] = fromEnvironment;
        Assert.Equal(new ConfigLocation(fromEnvironment, ConfigSource.EnvironmentVariable, Exists: false), store.Locate(null));

        var fromOption = new FileInfo(Path.Combine(root, "option.json"));
        Assert.Equal(new ConfigLocation(fromOption.FullName, ConfigSource.Option, Exists: false), store.Locate(fromOption));
    }

    [Fact]
    public void ExplicitConfigThatDoesNotExist_IsAnError()
    {
        var ex = Assert.Throws<UsageException>(() => store.Load(new FileInfo(Path.Combine(root, "missing.json"))));

        Assert.Contains("config init", ex.Message);
    }

    [Fact]
    public void Load_AcceptsCommentsAndTrailingCommas()
    {
        Write(UserPath, """
            {
                // personal settings
                "TimeZone": "UTC",
            }
            """);

        Assert.Equal("UTC", store.Load(null).TimeZone);
    }

    [Fact]
    public void Load_ReportsInvalidJson()
    {
        Write(UserPath, "{ \"TimeZone\": ");

        var ex = Assert.Throws<UsageException>(() => store.Load(null));

        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public void CreateDefault_WritesTheDefaultsButNeverOverwritesUnlessForced()
    {
        store.CreateDefault(UserPath, overwrite: false);
        Assert.Equal(ConfigStore.DefaultJson, File.ReadAllText(UserPath));

        File.WriteAllText(UserPath, "{ \"TimeZone\": \"UTC\" }");
        Assert.Throws<UsageException>(() => store.CreateDefault(UserPath, overwrite: false));
        Assert.Equal("UTC", store.Load(null).TimeZone);

        store.CreateDefault(UserPath, overwrite: true);
        Assert.Equal(ConfigStore.DefaultJson, File.ReadAllText(UserPath));
    }

    [Fact]
    public void Save_RoundTripsAndRefusesInvalidConfigs()
    {
        var config = store.Load(null);
        config.DeviceMapping["SM-A415F"] = "A41";
        ConfigStore.Save(UserPath, config);

        Assert.Equal("A41", store.Load(null).DeviceMapping["SM-A415F"]);

        config.FileNameTemplate = "{nope}";
        Assert.Throws<InvalidConfigurationException>(() => ConfigStore.Save(UserPath, config));
        Assert.Equal(new Configuration().FileNameTemplate, store.Load(null).FileNameTemplate);
    }

    [Fact]
    public void Load_WarnsAboutUnknownSettings()
    {
        Write(UserPath, """
            {
                "InputDirectory": "D:/in",
                "TimeZon": "UTC",
                "Options": { "CopyingEnabled": true, "Recursive": true },
                "DeviceMapping": { "Anything Goes": "x" }
            }
            """);
        var warnings = new List<string>();

        var config = store.Load(null, warnings.Add);

        Assert.True(config.Options.CopyingEnabled);
        Assert.Equal(3, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("\"InputDirectory\""));
        Assert.Contains(warnings, w => w.Contains("\"TimeZon\""));
        Assert.Contains(warnings, w => w.Contains("\"Options.Recursive\""));
    }

    [Fact]
    public void BuiltInDefault_HasNoUnknownSettings()
    {
        Assert.Empty(ConfigStore.FindUnknownSettings(ConfigStore.DefaultJson));
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
