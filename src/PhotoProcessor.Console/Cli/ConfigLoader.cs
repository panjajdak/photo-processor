namespace PhotoProcessor.Console.Cli;

using System.Text.Json;
using PhotoProcessor.Core.Config;

internal static class ConfigLoader
{
    public const string EnvironmentVariable = "PHOTOPROCESSOR_CONFIG";

    public static (string Path, string Source) Locate(FileInfo? option)
    {
        if (option != null)
        {
            return (option.FullName, "--config");
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return (Path.GetFullPath(fromEnvironment), EnvironmentVariable);
        }

        return (Path.Combine(AppContext.BaseDirectory, "config.json"), "default");
    }

    public static Configuration Load(FileInfo? option)
    {
        var (path, source) = Locate(option);
        if (!File.Exists(path))
        {
            throw new UsageException($"Config file not found: {path} (from {source}).");
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            return JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path), options)
                ?? throw new UsageException($"Config file is empty: {path}");
        }
        catch (JsonException ex)
        {
            throw new UsageException($"Config file {path} is not valid JSON: {ex.Message}");
        }
    }
}

internal sealed class UsageException(string message) : Exception(message);
