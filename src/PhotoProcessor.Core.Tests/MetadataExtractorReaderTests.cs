namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Metadata;
using PhotoProcessor.Core.Tests.Support;

public sealed class MetadataExtractorReaderTests : IDisposable
{
    private readonly TempDirectory temp = new();
    private readonly MetadataExtractorReader reader = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ReadsExifDatesAndCamera()
    {
        var path = temp.Write("photo.jpg", TestMedia.Jpeg(
            make: "samsung",
            model: "SM-A415F",
            dateTime: new DateTime(2023, 1, 1, 10, 0, 0),
            dateTimeOriginal: new DateTime(2021, 4, 8, 19, 41, 8),
            dateTimeDigitized: new DateTime(2021, 4, 8, 19, 41, 9),
            subSecondOriginal: "250"));

        var metadata = reader.Read(path);

        Assert.Equal(new DateTime(2021, 4, 8, 19, 41, 8), metadata.DateTimeOriginal);
        Assert.Equal(new DateTime(2021, 4, 8, 19, 41, 9), metadata.DateTimeDigitized);
        Assert.Equal(new DateTime(2023, 1, 1, 10, 0, 0), metadata.DateTimeModified);
        Assert.Equal("250", metadata.SubSecondOriginal);
        Assert.Equal("samsung", metadata.Make);
        Assert.Equal("SM-A415F", metadata.Model);
    }

    [Fact]
    public void JpegWithoutExif_ReturnsEmptyValues()
    {
        var path = temp.Write("plain.jpg", TestMedia.Jpeg());

        var metadata = reader.Read(path);

        Assert.Null(metadata.DateTimeOriginal);
        Assert.Null(metadata.Model);
    }

    [Fact]
    public void ReadsMovieHeaderCreationTime()
    {
        var path = temp.Write("clip.mp4", TestMedia.Mp4(new DateTime(2021, 4, 8, 17, 41, 8)));

        var metadata = reader.Read(path);

        Assert.Equal(new DateTime(2021, 4, 8, 17, 41, 8), metadata.QuickTimeCreated);
    }

    [Theory]
    [InlineData("2021-04-08T19:41:08+0200", 2)]
    [InlineData("2021-04-08T19:41:08+02:00", 2)]
    [InlineData("2021-04-08T19:41:08-0430", -4.5)]
    public void ParsesQuickTimeCreationDateWithOffset(string value, double offsetHours)
    {
        var parsed = MetadataExtractorReader.ParseQuickTimeCreationDate(value);

        Assert.Equal(new DateTimeOffset(2021, 4, 8, 19, 41, 8, TimeSpan.FromHours(offsetHours)), parsed);
        Assert.Equal(new DateTime(2021, 4, 8, 19, 41, 8), parsed!.Value.DateTime);
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData(null)]
    public void InvalidQuickTimeCreationDate_ReturnsNull(string? value)
    {
        Assert.Null(MetadataExtractorReader.ParseQuickTimeCreationDate(value));
    }
}
