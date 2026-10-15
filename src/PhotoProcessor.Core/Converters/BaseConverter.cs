namespace PhotoProcessor.Core.Converters;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PhotoProcessor.Core.Abstractions;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Resolvers;

internal abstract class BaseConverter : IConverter
{
    protected readonly Configuration Config;
    protected readonly IDeviceNameResolver DeviceResolver;
    protected abstract IList<string> HandledExtensions { get; }
    public BaseConverter(Configuration config)
    {
        Config = config;
        DeviceResolver = new DeviceNameResolver(config);
    }

    public abstract void Process(string path, string outputDirectory);

    public bool CanProcess(string extension) => HandledExtensions.Contains(extension);

    protected virtual string FormatDate(DateTime date)
        => date.ToString(Config.DateFormat);

    protected virtual DateTime GetDateTaken(string path)
        => File.GetCreationTime(path);

    protected static void AppendDirectory(StringBuilder sb, string outputDirectory)
    {
        sb.Append(outputDirectory);
        if (!outputDirectory.EndsWith("/"))
        {
            sb.Append("/");
        }
    }

    protected void AppendPrefix(StringBuilder sb, string prefix)
    {
        sb.Append(prefix);
        sb.Append(Config.Delimiter);
    }

    protected void AppendDate(StringBuilder sb, DateTime date)
    {
        var formattedDate = FormatDate(date);
        sb.Append(formattedDate);
    }

    protected void AppendExtension(StringBuilder sb, string path)
    {
        var extension = Path.GetExtension(path).ToLower();
        sb.Append(extension);
    }

    protected void AppendDeviceModel(StringBuilder sb, string deviceModel)
    {
        if (!string.IsNullOrEmpty(deviceModel))
        {
            sb.Append(Config.Delimiter);
            sb.Append(deviceModel);
        }
    }

    protected string BuildPath(StringBuilder sb, string extension)
    {
        var path = sb.ToString() + extension;

        if (File.Exists(path))
        {
            var i = 0;
            do
            {
                i++;
                path = sb.ToString() + Config.Delimiter + i + extension;
            }
            while (File.Exists(path));
        }

        return path;
    }

    protected void MoveFile(string path, string newPath)
    {
        if (Config.Options.CopyingEnabled)
        {
            File.Copy(path, newPath);
            return;
        }

        File.Move(path, newPath);
    }
}
