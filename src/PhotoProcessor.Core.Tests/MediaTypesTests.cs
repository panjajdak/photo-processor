namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Media;

public class MediaTypesTests
{
    [Theory]
    [InlineData("a.jpg", "Image")]
    [InlineData("a.JPEG", "Image")]
    [InlineData("a.png", "Image")]
    [InlineData("a.HEIC", "Image")]
    [InlineData("a.heif", "Image")]
    [InlineData("a.avif", "Image")]
    [InlineData("a.webp", "Image")]
    [InlineData("a.tif", "Image")]
    [InlineData("a.dng", "Image")]
    [InlineData("a.CR2", "Image")]
    [InlineData("a.CR3", "Image")]
    [InlineData("a.NEF", "Image")]
    [InlineData("a.ARW", "Image")]
    [InlineData("a.ORF", "Image")]
    [InlineData("a.RW2", "Image")]
    [InlineData("a.RAF", "Image")]
    [InlineData("a.MP4", "Video")]
    [InlineData("a.MOV", "Video")]
    [InlineData("a.m4v", "Video")]
    [InlineData("a.3gp", "Video")]
    [InlineData("a.xmp", null)]
    [InlineData("a.txt", null)]
    [InlineData("noextension", null)]
    public void DetectsKindByExtension(string fileName, string? expected)
    {
        Assert.Equal(expected, MediaTypes.GetKind(fileName)?.ToString());
    }

    [Theory]
    [InlineData("a.xmp", true)]
    [InlineData("a.XMP", true)]
    [InlineData("a.dng.xmp", true)]
    [InlineData("IMG_1234.AAE", true)]
    [InlineData("GH013592.THM", true)]
    [InlineData("a.jpg", false)]
    [InlineData("a.lrv", false)]
    public void DetectsSidecars(string fileName, bool expected)
    {
        Assert.Equal(expected, MediaTypes.IsSidecar(fileName));
    }

    [Theory]
    [InlineData("IMG_1234.HEIC", "IMG_1234")]
    [InlineData("IMG_1234.MOV", "IMG_1234")]
    [InlineData("IMG_1234.AAE", "IMG_1234")]
    [InlineData("IMG_1234.xmp", "IMG_1234")]
    [InlineData("IMG_1234.HEIC.xmp", "IMG_1234")]
    [InlineData("IMG 2021-04-08 19.41.08 A41.jpg", "IMG 2021-04-08 19.41.08 A41")]
    [InlineData("IMG 2021-04-08 19.41.08 A41.xmp", "IMG 2021-04-08 19.41.08 A41")]
    [InlineData("IMG 2021-04-08 19.41.08 A41.dng.xmp", "IMG 2021-04-08 19.41.08 A41")]
    [InlineData("notes.txt.xmp", "notes.txt")]
    public void GetsStemSharedWithCompanions(string fileName, string expected)
    {
        Assert.Equal(expected, MediaTypes.GetStem(fileName));
    }

    [Theory]
    [InlineData("a.JPG", ".jpg")]
    [InlineData("a.JPEG", ".jpg")]
    [InlineData("a.Png", ".png")]
    [InlineData("a.MP4", ".mp4")]
    [InlineData("a.HEIC", ".heic")]
    [InlineData("a.XMP", ".xmp")]
    [InlineData("a.DNG.xmp", ".dng.xmp")]
    [InlineData("a.JPEG.xmp", ".jpg.xmp")]
    [InlineData("IMG 2021-04-08 19.41.08.xmp", ".xmp")]
    public void NormalizesOutputExtensions(string fileName, string expected)
    {
        Assert.Equal(expected, MediaTypes.GetOutputExtension(fileName));
    }
}
