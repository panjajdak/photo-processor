namespace PhotoProcessor.Core.Config;

public class Options
{
    /// <summary>
    /// Copies files to output directory instead of moving them
    /// </summary>
    public bool CopyingEnabled { get; set; }

    // TODO: implement double checking based on filename + size? file hash might be overkill
    /// <summary>
    /// Ignores file if double already exists in output directory
    /// </summary>
    //public bool IgnoreDoubles { get; set; }
}
