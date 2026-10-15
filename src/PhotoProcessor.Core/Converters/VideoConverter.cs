namespace PhotoProcessor.Core.Converters;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;
using PhotoProcessor.Core.Config;

internal sealed class VideoConverter : BaseConverter
{
    protected override IList<string> HandledExtensions => new[] { "mp4" };

    public VideoConverter(Configuration config) : base(config)
    { }

    public override void Process(string path, string outputDirectory)
    {
        var sb = new StringBuilder();
        var extension = Path.GetExtension(path).ToLower();

        AppendDirectory(sb, outputDirectory);
        AppendPrefix(sb, Config.VideoPrefix);
        AppendDate(sb, GetDateTaken(path));

        var newPath = BuildPath(sb, extension);
        MoveFile(path, newPath);
    }

    protected override DateTime GetDateTaken(string path)
    {
        var metadata = ImageMetadataReader.ReadMetadata(path);
        var directory = metadata.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
        var date = directory?.GetDateTime(QuickTimeMovieHeaderDirectory.TagCreated);

        return date != null
            ? date.Value
            : base.GetDateTaken(path);
    }
}
