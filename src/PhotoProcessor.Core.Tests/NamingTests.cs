namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Media;
using PhotoProcessor.Core.Naming;
using PhotoProcessor.Core.Resolvers;

public class NamingTests
{
    private static readonly Configuration Config = new()
    {
        DeviceMapping = new() { ["SM-A415F"] = "A41", ["HERO7 Black"] = "Hero7" },
    };

    [Theory]
    [InlineData("SM-A415F", "A41")]
    [InlineData("sm-a415f", "A41")]
    [InlineData("  SM-A415F ", "A41")]
    [InlineData("Pixel 7", "Pixel 7")]
    [InlineData("Model/With:Bad*Chars", "Model_With_Bad_Chars")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void DeviceNameResolver_MapsCaseInsensitivelyAndSanitizes(string? model, string? expected)
    {
        var resolver = new DeviceNameResolver(Config);

        Assert.Equal(expected, resolver.Resolve(model));
    }

    [Fact]
    public void FileNamer_BuildsNameFromPrefixDateAndDevice()
    {
        var namer = new FileNamer(Config);

        Assert.Equal("IMG 2021-04-08 19.41.08 A41", namer.BuildBaseName(MediaKind.Image, new DateTime(2021, 4, 8, 19, 41, 8), "A41"));
        Assert.Equal("VID 2021-04-08 19.41.08", namer.BuildBaseName(MediaKind.Video, new DateTime(2021, 4, 8, 19, 41, 8), null));
    }

    [Fact]
    public void FileNamer_FormatsDateIndependentlyOfCulture()
    {
        var namer = new FileNamer(new Configuration { DateFormat = "yyyy-MM-dd HH:mm:ss" });
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            // Finnish uses '.' as the time separator, which ':' would pick up under the current culture.
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fi-FI");
            Assert.NotEqual("19:41:08", new DateTime(2021, 4, 8, 19, 41, 8).ToString("HH:mm:ss"));

            Assert.Equal("IMG 2021-04-08 19:41:08", namer.BuildBaseName(MediaKind.Image, new DateTime(2021, 4, 8, 19, 41, 8), null));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void FileNamer_NumbersCollisionsAfterTheFirst()
    {
        var namer = new FileNamer(Config);

        Assert.Equal("IMG x.jpg", namer.BuildFileName("IMG x", 0, ".jpg"));
        Assert.Equal("IMG x 1.jpg", namer.BuildFileName("IMG x", 1, ".jpg"));
        Assert.Equal("IMG x 2.jpg", namer.BuildFileName("IMG x", 2, ".jpg"));
    }

    [Theory]
    [InlineData("a.JPG", ".jpg")]
    [InlineData("a.JPEG", ".jpg")]
    [InlineData("a.Png", ".png")]
    [InlineData("a.MP4", ".mp4")]
    public void MediaTypes_NormalizesExtensions(string fileName, string expected)
    {
        Assert.Equal(expected, MediaTypes.GetOutputExtension(fileName));
    }

    [Theory]
    [InlineData("a.jpg", "Image")]
    [InlineData("a.JPEG", "Image")]
    [InlineData("a.png", "Image")]
    [InlineData("a.MP4", "Video")]
    [InlineData("a.dng", null)]
    [InlineData("a.txt", null)]
    [InlineData("noextension", null)]
    public void MediaTypes_DetectsKindByExtension(string fileName, string? expected)
    {
        Assert.Equal(expected, MediaTypes.GetKind(fileName)?.ToString());
    }
}
