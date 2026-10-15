namespace PhotoProcessor.Core.Converters;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PhotoProcessor.Core.Config;
using RedCorners.ExifLibrary;

internal sealed class ImageConverter : BaseConverter
{
    protected override IList<string> HandledExtensions => new[] { "jpg", "png" };

    public ImageConverter(Configuration config) : base(config)
    { }

    public override void Process(string path, string outputDirectory)
    {
        var image = ImageFile.FromFile(path);
        var sb = new StringBuilder();
        var extension = Path.GetExtension(path).ToLower();

        AppendDirectory(sb, outputDirectory);
        AppendPrefix(sb, Config.ImagePrefix);
        AppendDate(sb, GetDateTaken(path, image));
        AppendDeviceModel(sb, GetDeviceModel(image));

        var newPath = BuildPath(sb, extension);
        MoveFile(path, newPath);
    }

    private DateTime GetDateTaken(string path, ImageFile image)
    {
        if (image.Properties.TryGetValue(ExifTag.DateTime, out var exifDate))
        {
            return (DateTime)exifDate?.Value;
        }

        return File.GetCreationTime(path);
    }

    private string GetDeviceModel(ImageFile image)
    {
        if (image.Properties.TryGetValue(ExifTag.Model, out var exifModel))
        {
            return DeviceResolver.Resolve(exifModel.ToString());
        }

        return null;
    }
}
