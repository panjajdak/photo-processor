namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Dating;

public class FileNameDateParserTests
{
    [Theory]
    [InlineData("IMG_20210408_194108.jpg", "2021-04-08 19:41:08")]
    [InlineData("IMG_20210408_203040(1).jpg", "2021-04-08 20:30:40")]
    [InlineData("20221113_135022.mp4", "2022-11-13 13:50:22")]
    [InlineData("VID_20210408_194108.mp4", "2021-04-08 19:41:08")]
    [InlineData("Screenshot_20210408-194108.png", "2021-04-08 19:41:08")]
    [InlineData("Screenshot_2021-04-08-19-41-08-123_com.app.jpg", "2021-04-08 19:41:08")]
    [InlineData("PXL_20210408_194108123.jpg", "2021-04-08 19:41:08")]
    [InlineData("IMG 2021-04-08 19.41.08 A41.jpg", "2021-04-08 19:41:08")]
    [InlineData("IMG--2021-04-08--19-41-08--A41--001.jpg", "2021-04-08 19:41:08")]
    [InlineData("IMG 2020-05-08 22-09-00.png", "2020-05-08 22:09:00")]
    public void ParseDateTime_RecognizesCommonNames(string fileName, string expected)
    {
        Assert.Equal(DateTime.Parse(expected), FileNameDateParser.ParseDateTime(fileName));
    }

    [Theory]
    [InlineData("GOPR3564.jpg")]
    [InlineData("GH013592.mp4")]
    [InlineData("DSC01234.jpg")]
    [InlineData("IMG-20210408-WA0001.jpg")]
    public void ParseDateTime_ReturnsNullWithoutFullTimestamp(string fileName)
    {
        Assert.Null(FileNameDateParser.ParseDateTime(fileName));
    }

    [Theory]
    [InlineData("IMG_20211340_101010.jpg")]
    [InlineData("IMG_20210230_101010.jpg")]
    [InlineData("IMG_20210408_256010.jpg")]
    public void ParseDateTime_RejectsImpossibleMonthDayOrHour(string fileName)
    {
        Assert.Null(FileNameDateParser.ParseDateTime(fileName));
    }

    [Theory]
    [InlineData("IMG-20210408-WA0001.jpg", "2021-04-08")]
    [InlineData("VID-20191231-WA0042.mp4", "2019-12-31")]
    public void ParseDate_RecognizesDateOnlyNames(string fileName, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), FileNameDateParser.ParseDate(fileName));
    }

    [Theory]
    [InlineData("GH013592.mp4")]
    [InlineData("IMG_123456789.jpg")]
    public void ParseDate_ReturnsNullWithoutDate(string fileName)
    {
        Assert.Null(FileNameDateParser.ParseDate(fileName));
    }
}
