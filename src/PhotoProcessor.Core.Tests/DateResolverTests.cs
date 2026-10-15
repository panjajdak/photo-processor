namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Dating;
using PhotoProcessor.Core.Media;
using PhotoProcessor.Core.Metadata;

public class DateResolverTests
{
    private static readonly DateTime FileTime = new(2030, 1, 1, 12, 0, 0);
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone("PlusTwo", TimeSpan.FromHours(2), "PlusTwo", "PlusTwo");
    private static readonly string[] GoProPatterns = [@"^GOPR\d{4}\.", @"^G[HXLP]\d{6}\."];

    private readonly DateResolver resolver = new(PlusTwo, GoProPatterns);

    [Fact]
    public void Image_PrefersDateTimeOriginalOverModifiedDate()
    {
        var metadata = new MediaMetadata
        {
            DateTimeOriginal = new DateTime(2021, 4, 8, 19, 41, 8),
            DateTimeDigitized = new DateTime(2021, 4, 8, 19, 41, 9),
            DateTimeModified = new DateTime(2023, 1, 1, 10, 0, 0),
        };

        var result = resolver.Resolve(MediaKind.Image, metadata, "edited.jpg", FileTime);

        Assert.Equal(new ResolvedDate(new DateTime(2021, 4, 8, 19, 41, 8), DateSource.ExifOriginal), result);
    }

    [Fact]
    public void Image_FallsBackToDigitized()
    {
        var metadata = new MediaMetadata { DateTimeDigitized = new DateTime(2021, 4, 8, 19, 41, 9) };

        var result = resolver.Resolve(MediaKind.Image, metadata, "x.jpg", FileTime);

        Assert.Equal(DateSource.ExifDigitized, result.Source);
    }

    [Fact]
    public void Image_PrefersFileNameOverModifiedDate()
    {
        var metadata = new MediaMetadata { DateTimeModified = new DateTime(2023, 1, 1, 10, 0, 0) };

        var result = resolver.Resolve(MediaKind.Image, metadata, "IMG_20210408_194108.jpg", FileTime);

        Assert.Equal(new ResolvedDate(new DateTime(2021, 4, 8, 19, 41, 8), DateSource.FileName), result);
    }

    [Fact]
    public void Image_UsesModifiedDateWhenNothingBetterExists()
    {
        var metadata = new MediaMetadata { DateTimeModified = new DateTime(2023, 1, 1, 10, 0, 0) };

        var result = resolver.Resolve(MediaKind.Image, metadata, "photo.jpg", FileTime);

        Assert.Equal(DateSource.ExifModified, result.Source);
    }

    [Fact]
    public void DateOnlyFileName_UsesMidnightWhenFileTimeIsAnotherDay()
    {
        var result = resolver.Resolve(MediaKind.Image, MediaMetadata.Empty, "IMG-20210408-WA0001.jpg", FileTime);

        Assert.Equal(new ResolvedDate(new DateTime(2021, 4, 8), DateSource.FileNameDateOnly), result);
    }

    [Fact]
    public void DateOnlyFileName_UsesFileTimeWhenSameDay()
    {
        var fileTime = new DateTime(2021, 4, 8, 18, 30, 0);

        var result = resolver.Resolve(MediaKind.Image, MediaMetadata.Empty, "IMG-20210408-WA0001.jpg", fileTime);

        Assert.Equal(new ResolvedDate(fileTime, DateSource.FileModified), result);
    }

    [Fact]
    public void NoInformation_UsesFileModifiedTime()
    {
        var result = resolver.Resolve(MediaKind.Image, MediaMetadata.Empty, "random.png", FileTime);

        Assert.Equal(new ResolvedDate(FileTime, DateSource.FileModified), result);
    }

    [Fact]
    public void Video_ConvertsUtcMovieHeaderToConfiguredTimeZone()
    {
        var metadata = new MediaMetadata { QuickTimeCreated = new DateTime(2021, 4, 8, 17, 41, 8) };

        var result = resolver.Resolve(MediaKind.Video, metadata, "20210408_194108.mp4", FileTime);

        Assert.Equal(new ResolvedDate(new DateTime(2021, 4, 8, 19, 41, 8), DateSource.QuickTimeUtc), result);
    }

    [Theory]
    [InlineData("GH013592.MP4")]
    [InlineData("GX010042.mp4")]
    [InlineData("GOPR3564.MP4")]
    public void Video_KeepsLocalTimeForMatchingCameras(string fileName)
    {
        var metadata = new MediaMetadata { QuickTimeCreated = new DateTime(2021, 12, 1, 21, 45, 0) };

        var result = resolver.Resolve(MediaKind.Video, metadata, fileName, FileTime);

        Assert.Equal(new ResolvedDate(new DateTime(2021, 12, 1, 21, 45, 0), DateSource.QuickTimeLocal), result);
    }

    [Fact]
    public void Video_PrefersQuickTimeCreationDateWallClock()
    {
        var metadata = new MediaMetadata
        {
            QuickTimeCreationDate = new DateTimeOffset(2021, 7, 1, 9, 0, 0, TimeSpan.FromHours(-4)),
            QuickTimeCreated = new DateTime(2021, 7, 1, 13, 0, 0),
        };

        var result = resolver.Resolve(MediaKind.Video, metadata, "IMG_0001.mp4", FileTime);

        Assert.Equal(new ResolvedDate(new DateTime(2021, 7, 1, 9, 0, 0), DateSource.QuickTimeCreationDate), result);
    }

    [Fact]
    public void Video_FallsBackToFileName()
    {
        var result = resolver.Resolve(MediaKind.Video, MediaMetadata.Empty, "VID_20210408_194108.mp4", FileTime);

        Assert.Equal(new ResolvedDate(new DateTime(2021, 4, 8, 19, 41, 8), DateSource.FileName), result);
    }

    [Fact]
    public void Video_UtcConversionFollowsDaylightSavingTime()
    {
        var warsaw = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        var dstResolver = new DateResolver(warsaw, []);

        var summer = dstResolver.Resolve(MediaKind.Video, new MediaMetadata { QuickTimeCreated = new DateTime(2021, 7, 1, 10, 0, 0) }, "a.mp4", FileTime);
        var winter = dstResolver.Resolve(MediaKind.Video, new MediaMetadata { QuickTimeCreated = new DateTime(2021, 1, 1, 10, 0, 0) }, "b.mp4", FileTime);

        Assert.Equal(new DateTime(2021, 7, 1, 12, 0, 0), summer.Value);
        Assert.Equal(new DateTime(2021, 1, 1, 11, 0, 0), winter.Value);
    }
}
