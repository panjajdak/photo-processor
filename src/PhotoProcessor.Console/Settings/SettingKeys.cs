namespace PhotoProcessor.Console.Settings;

using PhotoProcessor.Console.Cli;
using PhotoProcessor.Core.Config;

internal static class SettingKeys
{
    private static readonly Dictionary<string, Setting> Settings = new Setting[]
    {
        new("FileNameTemplate", c => c.FileNameTemplate, (c, v) => c.FileNameTemplate = v),
        new("ImagePrefix", c => c.ImagePrefix, (c, v) => c.ImagePrefix = v),
        new("VideoPrefix", c => c.VideoPrefix, (c, v) => c.VideoPrefix = v),
        new("TimeZone", c => c.TimeZone, (c, v) => c.TimeZone = EmptyToNull(v)),
        new("CopyingEnabled", c => c.Options.CopyingEnabled.ToString().ToLowerInvariant(), (c, v) => c.Options.CopyingEnabled = ParseBool(v)),
    }.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> Names => Settings.Keys;

    public static string Set(Configuration config, string key, string value)
    {
        if (!Settings.TryGetValue(key, out var setting))
        {
            throw new UsageException($"Unknown setting \"{key}\". Settings: {string.Join(", ", Names)}. Edit lists such as LocalTimeVideoPatterns with \"{Program.AppName} config edit\".");
        }

        setting.Apply(config, value);
        return setting.Name;
    }

    public static string? Get(Configuration config, string key)
        => Settings.TryGetValue(key, out var setting) ? setting.Read(config) : null;

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool ParseBool(string value)
        => bool.TryParse(value, out var result) ? result : throw new UsageException($"\"{value}\" is not true or false.");

    private sealed record Setting(string Name, Func<Configuration, string?> Read, Action<Configuration, string> Apply);
}
