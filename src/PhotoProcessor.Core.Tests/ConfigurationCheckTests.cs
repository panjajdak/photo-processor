namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Config;

public class ConfigurationCheckTests
{
    [Fact]
    public void DefaultConfiguration_IsValid()
    {
        Assert.Empty(ConfigurationCheck.Validate(new Configuration()));
    }

    [Fact]
    public void ReportsEveryProblemAtOnce()
    {
        var config = new Configuration
        {
            FileNameTemplate = "{nope}",
            TimeZone = "Mars/Olympus_Mons",
            LocalTimeVideoPatterns = ["^GOPR(", "^G[HX]\\d{6}"],
            ImagePrefix = "IMG?",
        };

        var problems = ConfigurationCheck.Validate(config);

        Assert.Equal(4, problems.Count);
        Assert.Contains(problems, p => p.Contains("unknown token"));
        Assert.Contains(problems, p => p.Contains("Mars/Olympus_Mons"));
        Assert.Contains(problems, p => p.Contains("^GOPR("));
        Assert.Contains(problems, p => p.Contains("ImagePrefix"));
    }

    [Fact]
    public void EnsureValid_ThrowsWithAllProblems()
    {
        var config = new Configuration { FileNameTemplate = "{nope}", TimeZone = "Nowhere" };

        var ex = Assert.Throws<InvalidConfigurationException>(() => ConfigurationCheck.EnsureValid(config));

        Assert.Equal(2, ex.Problems.Count);
    }

    [Theory]
    [InlineData("Europe/Warsaw")]
    [InlineData("Central European Standard Time")]
    [InlineData("UTC")]
    public void AcceptsWindowsAndIanaTimeZones(string timeZone)
    {
        Assert.Empty(ConfigurationCheck.Validate(new Configuration { TimeZone = timeZone }));
    }

    [Fact]
    public void ExampleNames_UseTheTemplate()
    {
        var examples = ConfigurationCheck.ExampleNames(new Configuration());

        Assert.Equal(
            ["IMG 2021-04-08 19.41.08 A41.jpg", "IMG 2021-04-08 19.41.08 A41 1.jpg", "IMG 2021-04-08 19.41.08.png", "VID 2021-04-08 19.41.08 Hero7.mp4"],
            examples.Select(e => e.FileName));
    }
}
