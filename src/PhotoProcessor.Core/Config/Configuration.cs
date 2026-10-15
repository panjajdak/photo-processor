namespace PhotoProcessor.Core.Config;

using System.Collections.Generic;

public class Configuration
{
    public string OutputDirectory { get; set; }
    public string InputDirectory { get; set; }
    public string DateFormat { get; set; }
    public string Delimiter { get; set; }
    public string ImagePrefix { get; set; }
    public string VideoPrefix { get; set; }
    public Options Options { get; set; }
    public Dictionary<string, string> DeviceMapping { get; set; }
}
