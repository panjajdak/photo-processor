namespace PhotoProcessor.Console.Tests;

using PhotoProcessor.Console.Cli;
using PhotoProcessor.Console.Settings;
using PhotoProcessor.Core.Config;

public class SettingKeysTests
{
    [Theory]
    [InlineData("InputDirectory", @"\\NAS\in")]
    [InlineData("outputdirectory", @"D:\Photos")]
    [InlineData("FileNameTemplate", "{date:yyyyMMdd}")]
    [InlineData("ImagePrefix", "PHOTO")]
    [InlineData("VideoPrefix", "MOVIE")]
    [InlineData("TimeZone", "Europe/Warsaw")]
    [InlineData("CopyingEnabled", "true")]
    public void Set_ChangesTheSetting(string key, string value)
    {
        var config = new Configuration();

        var name = SettingKeys.Set(config, key, value);

        Assert.Equal(value, SettingKeys.Get(config, name));
    }

    [Fact]
    public void Set_ReturnsTheCanonicalName()
    {
        Assert.Equal("TimeZone", SettingKeys.Set(new Configuration(), "timezone", "UTC"));
    }

    [Theory]
    [InlineData("InputDirectory")]
    [InlineData("OutputDirectory")]
    [InlineData("TimeZone")]
    public void Set_EmptyValueClearsOptionalSettings(string key)
    {
        var config = new Configuration { InputDirectory = "x", OutputDirectory = "y", TimeZone = "UTC" };

        SettingKeys.Set(config, key, "");

        Assert.Null(SettingKeys.Get(config, key));
    }

    [Fact]
    public void Set_RejectsUnknownKeysAndListsTheValidOnes()
    {
        var ex = Assert.Throws<UsageException>(() => SettingKeys.Set(new Configuration(), "DeviceMapping", "x"));

        Assert.Contains("TimeZone", ex.Message);
        Assert.Contains("config edit", ex.Message);
    }

    [Fact]
    public void Set_RejectsNonBooleanCopyingEnabled()
    {
        Assert.Throws<UsageException>(() => SettingKeys.Set(new Configuration(), "CopyingEnabled", "maybe"));
    }
}
