namespace PhotoProcessor.Console.Tests;

using PhotoProcessor.Console.Cli;
using PhotoProcessor.Console.Settings;
using PhotoProcessor.Core.Config;

public class SettingKeysTests
{
    [Theory]
    [InlineData("FileNameTemplate", "{date:yyyyMMdd}")]
    [InlineData("ImagePrefix", "PHOTO")]
    [InlineData("VideoPrefix", "MOVIE")]
    [InlineData("TimeZone", "Europe/Warsaw")]
    [InlineData("timezone", "UTC")]
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

    [Fact]
    public void Set_EmptyValueClearsTheTimeZone()
    {
        var config = new Configuration { TimeZone = "UTC" };

        SettingKeys.Set(config, "TimeZone", "");

        Assert.Null(SettingKeys.Get(config, "TimeZone"));
    }

    [Theory]
    [InlineData("InputDirectory")]
    [InlineData("OutputDirectory")]
    [InlineData("DeviceMapping")]
    public void Set_RejectsUnknownKeysAndListsTheValidOnes(string key)
    {
        var ex = Assert.Throws<UsageException>(() => SettingKeys.Set(new Configuration(), key, "x"));

        Assert.Contains("TimeZone", ex.Message);
        Assert.Contains("config edit", ex.Message);
    }

    [Fact]
    public void Set_RejectsNonBooleanCopyingEnabled()
    {
        Assert.Throws<UsageException>(() => SettingKeys.Set(new Configuration(), "CopyingEnabled", "maybe"));
    }
}
