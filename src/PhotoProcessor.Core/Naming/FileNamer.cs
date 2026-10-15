namespace PhotoProcessor.Core.Naming;

using System;
using System.Globalization;
using System.Text;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Media;

internal sealed class FileNamer
{
    private readonly Configuration config;

    public FileNamer(Configuration config)
    {
        this.config = config;
    }

    /// <summary>
    /// Builds the file name without extension or counter, e.g. "IMG 2021-04-08 19.41.08 A41".
    /// </summary>
    public string BuildBaseName(MediaKind kind, DateTime date, string? device)
    {
        var sb = new StringBuilder();
        sb.Append(kind == MediaKind.Video ? config.VideoPrefix : config.ImagePrefix);
        sb.Append(config.Delimiter);
        sb.Append(date.ToString(config.DateFormat, CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(device))
        {
            sb.Append(config.Delimiter);
            sb.Append(device);
        }

        return sb.ToString();
    }

    /// <summary>
    /// File name for the n-th file sharing a base name: the first keeps the plain name, later ones get " 1", " 2", ...
    /// </summary>
    public string BuildFileName(string baseName, int index, string extension)
        => index == 0
            ? baseName + extension
            : baseName + config.Delimiter + index.ToString(CultureInfo.InvariantCulture) + extension;
}
