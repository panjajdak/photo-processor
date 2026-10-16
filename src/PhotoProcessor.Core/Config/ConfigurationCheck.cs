namespace PhotoProcessor.Core.Config;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PhotoProcessor.Core.Naming;

/// <summary>Thrown when the configuration has errors; lists all of them.</summary>
public sealed class InvalidConfigurationException(IReadOnlyList<string> problems)
    : Exception("Invalid configuration:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "  - " + p)))
{
    /// <summary>Every problem found in the configuration.</summary>
    public IReadOnlyList<string> Problems { get; } = problems;
}

/// <summary>Validates a configuration before any file is touched.</summary>
public static class ConfigurationCheck
{
    /// <summary>Returns all problems in the configuration; empty when it is valid.</summary>
    public static IReadOnlyList<string> Validate(Configuration config)
    {
        var problems = new List<string>();

        try
        {
            FileNameTemplate.Parse(config.FileNameTemplate);
        }
        catch (FormatException ex)
        {
            problems.Add(ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(config.TimeZone))
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(config.TimeZone);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                problems.Add($"TimeZone \"{config.TimeZone}\" is not a known time zone id (e.g. \"Europe/Warsaw\").");
            }
        }

        foreach (var pattern in config.LocalTimeVideoPatterns)
        {
            try
            {
                _ = new Regex(pattern);
            }
            catch (ArgumentException ex)
            {
                problems.Add($"LocalTimeVideoPatterns entry \"{pattern}\" is not a valid regular expression: {ex.Message}");
            }
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var (name, value) in new[] { ("ImagePrefix", config.ImagePrefix), ("VideoPrefix", config.VideoPrefix) })
        {
            if (value.IndexOfAny(invalidChars) >= 0)
            {
                problems.Add($"{name} \"{value}\" contains characters not allowed in file names.");
            }
        }

        return problems;
    }

    /// <summary>Throws <see cref="InvalidConfigurationException"/> when the configuration has problems.</summary>
    public static void EnsureValid(Configuration config)
    {
        var problems = Validate(config);
        if (problems.Count > 0)
        {
            throw new InvalidConfigurationException(problems);
        }
    }

    /// <summary>Sample file names the configured template produces.</summary>
    public static IReadOnlyList<(string Description, string FileName)> ExampleNames(Configuration config)
    {
        var template = FileNameTemplate.Parse(config.FileNameTemplate);
        var taken = new DateTime(2021, 4, 8, 19, 41, 8);
        var phone = new NameContext(config.ImagePrefix, taken, "A41", "samsung", "SM-A415F", "IMG_20210408_194108");
        var unknown = new NameContext(config.ImagePrefix, taken, null, null, null, "scan");
        var video = new NameContext(config.VideoPrefix, taken, "Hero7", "GoPro", "HERO7 Black", "GH013592");

        return
        [
            ("Photo from a mapped device", template.Render(phone, 0) + ".jpg"),
            ("Second photo in the same second", template.Render(phone, 1) + ".jpg"),
            ("Photo without camera information", template.Render(unknown, 0) + ".png"),
            ("Video", template.Render(video, 0) + ".mp4"),
        ];
    }
}
