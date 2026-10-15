namespace PhotoProcessor.Core.Config;

using System.Collections.Generic;

public class Configuration
{
    public string DateFormat { get; set; } = "yyyy-MM-dd HH.mm.ss";
    public string Delimiter { get; set; } = " ";
    public string ImagePrefix { get; set; } = "IMG";
    public string VideoPrefix { get; set; } = "VID";

    /// <summary>
    /// Time zone used to convert UTC video timestamps to local time.
    /// Windows or IANA id (e.g. "Central European Standard Time" or "Europe/Warsaw"); system time zone when empty.
    /// </summary>
    public string? TimeZone { get; set; }

    /// <summary>
    /// Regexes matched against original video file names whose embedded timestamp is already local time
    /// instead of UTC (GoPro cameras write local time).
    /// </summary>
    public List<string> LocalTimeVideoPatterns { get; set; } = new();

    public Options Options { get; set; } = new();
    public Dictionary<string, string> DeviceMapping { get; set; } = new();
}
