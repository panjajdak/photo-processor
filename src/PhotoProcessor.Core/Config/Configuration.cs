namespace PhotoProcessor.Core.Config;

using System.Collections.Generic;
using PhotoProcessor.Core.Naming;

/// <summary>Settings read from config.json.</summary>
public class Configuration
{
    /// <summary>Folder to process when none is given on the command line.</summary>
    public string? InputDirectory { get; set; }

    /// <summary>Folder for renamed files; defaults to the input folder.</summary>
    public string? OutputDirectory { get; set; }

    /// <summary>Pattern for new file names, e.g. "{type} {date:yyyy-MM-dd HH.mm.ss}[ {device}]".</summary>
    public string FileNameTemplate { get; set; } = Naming.FileNameTemplate.Default;

    /// <summary>Value of {type} for photos.</summary>
    public string ImagePrefix { get; set; } = "IMG";
    /// <summary>Value of {type} for videos.</summary>
    public string VideoPrefix { get; set; } = "VID";

    /// <summary>Windows or IANA time zone for UTC video times; the system time zone when empty.</summary>
    public string? TimeZone { get; set; }

    /// <summary>File name regexes of videos whose timestamps are already local time (e.g. GoPro).</summary>
    public List<string> LocalTimeVideoPatterns { get; set; } = new();

    /// <summary>Behavior switches.</summary>
    public Options Options { get; set; } = new();
    /// <summary>Camera model from metadata mapped to the short name used in file names.</summary>
    public Dictionary<string, string> DeviceMapping { get; set; } = new();
}
