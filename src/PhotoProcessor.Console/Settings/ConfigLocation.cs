namespace PhotoProcessor.Console.Settings;

internal sealed record ConfigLocation(string Path, ConfigSource Source, bool Exists)
{
    public string Describe() => Source switch
    {
        ConfigSource.Option => "from --config",
        ConfigSource.EnvironmentVariable => $"from {ConfigStore.EnvironmentVariable}",
        ConfigSource.NextToExecutable => "next to the executable",
        _ => Exists ? "user config" : "not created yet; built-in defaults are used",
    };
}
