namespace PhotoProcessor.Core.Tests;

using System.Globalization;
using PhotoProcessor.Core.Naming;

public class FileNameTemplateTests
{
    private static readonly DateTime Taken = new(2021, 4, 8, 19, 41, 8);
    private static readonly NameContext Full = new("IMG", Taken, "A41", "samsung", "SM-A415F", "IMG_20210408_194108");
    private static readonly NameContext NoDevice = Full with { Device = null, Make = null, Model = null };

    [Fact]
    public void DefaultTemplate_ReproducesPreviousNames()
    {
        var template = FileNameTemplate.Parse(FileNameTemplate.Default);

        Assert.Equal("IMG 2021-04-08 19.41.08 A41", template.Render(Full, 0));
        Assert.Equal("IMG 2021-04-08 19.41.08 A41 1", template.Render(Full, 1));
        Assert.Equal("IMG 2021-04-08 19.41.08", template.Render(NoDevice, 0));
        Assert.Equal("IMG 2021-04-08 19.41.08 2", template.Render(NoDevice, 2));
    }

    [Theory]
    [InlineData("{type}--{date:yyyy-MM-dd--HH-mm-ss}[--{device}][--{counter:000}]", 1, "IMG--2021-04-08--19-41-08--A41--001")]
    [InlineData("{date:yyyyMMdd_HHmmss}[_{device}]", 0, "20210408_194108_A41")]
    [InlineData("{date:yyyyMMdd_HHmmss}[_{device}]", 3, "20210408_194108_A41 3")]
    [InlineData("{date:yyyy-MM-dd HH.mm.ss} {original}", 0, "2021-04-08 19.41.08 IMG_20210408_194108")]
    [InlineData("{date:yyyy}/{date:MM}", 0, null)]
    [InlineData("{make} {model} {date}", 0, "samsung SM-A415F 2021-04-08 19.41.08")]
    [InlineData("{{{date:yyyy}}} [[{type}]]", 0, "{2021} [IMG]")]
    [InlineData("  {date:yyyy}  spaced   out", 0, "  2021  spaced   out")]
    public void RendersCustomTemplates(string pattern, int counter, string? expected)
    {
        if (expected == null)
        {
            Assert.Throws<FormatException>(() => FileNameTemplate.Parse(pattern));
            return;
        }

        Assert.Equal(expected, FileNameTemplate.Parse(pattern).Render(Full, counter));
    }

    [Fact]
    public void OptionalSection_IsDroppedWhenAnyTokenIsMissing()
    {
        var template = FileNameTemplate.Parse("{date:yyyyMMdd}[ ({make} {model})]");

        Assert.Equal("20210408 (samsung SM-A415F)", template.Render(Full, 0));
        Assert.Equal("20210408", template.Render(Full with { Make = null }, 0));
    }

    [Fact]
    public void MissingTokenOutsideOptional_RendersEmpty()
    {
        var template = FileNameTemplate.Parse("{date:yyyyMMdd}_{device}");

        Assert.Equal("20210408_", template.Render(NoDevice, 0));
    }

    [Fact]
    public void TokenValuesAreMadeFileNameSafe()
    {
        var template = FileNameTemplate.Parse("{date:yyyy} {model}");

        Assert.Equal("2021 Model_X", template.Render(Full with { Model = "Model/X" }, 0));
    }

    [Fact]
    public void DateIsFormattedIndependentlyOfCulture()
    {
        var template = FileNameTemplate.Parse("{date:yyyy-MM-dd HH.mm.ss}");
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA"); // non-Gregorian calendar
            Assert.Equal("2021-04-08 19.41.08 1", template.Render(Full, 1));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("{dat}", "unknown token")]
    [InlineData("{date", "not closed")]
    [InlineData("date}", "without '{'")]
    [InlineData("{date}[ {device}", "'[' is not closed")]
    [InlineData("{date}]", "without '['")]
    [InlineData("{date}[ [{device}]]", "nested")]
    [InlineData("{date}[ - ]", "must contain a token")]
    [InlineData("{type} {device}", "{date} or {original}")]
    [InlineData("{date:HH:mm:ss}", "not allowed in file names")]
    [InlineData("{date}*", "not allowed in file names")]
    [InlineData("{date} ", "must not end")]
    [InlineData("{date}.", "must not end")]
    [InlineData("{device:x} {date}", "does not take a format")]
    [InlineData("{date}[ {device} {counter}]", "cannot share")]
    public void InvalidTemplates_AreRejectedWithReason(string pattern, string reason)
    {
        var ex = Assert.Throws<FormatException>(() => FileNameTemplate.Parse(pattern));

        Assert.Contains(reason, ex.Message);
    }

    [Theory]
    [InlineData("IMG 2021-04-08 19.41.08 A41", true)]
    [InlineData("IMG 2021-04-08 19.41.08 A41 1", true)]
    [InlineData("img 2021-04-08 19.41.08 a41 12", true)]
    [InlineData("IMG 2021-04-08 19.41.08 A41 x", false)]
    [InlineData("IMG 2021-04-08 19.41.08", false)]
    [InlineData("IMG 2021-04-08 19.41.09 A41", false)]
    [InlineData("IMG_20210408_194108", false)]
    public void Matches_RecognizesAnyCounter(string stem, bool expected)
    {
        var template = FileNameTemplate.Parse(FileNameTemplate.Default);

        Assert.Equal(expected, template.Matches(stem, Full));
    }

    [Fact]
    public void Matches_CounterInTheMiddle()
    {
        var template = FileNameTemplate.Parse("{date:yyyyMMdd}[-{counter:00}]_{device}");

        Assert.Equal("20210408-03_A41", template.Render(Full, 3));
        Assert.True(template.Matches("20210408-03_A41", Full));
        Assert.True(template.Matches("20210408_A41", Full));
        Assert.False(template.Matches("20210408-03_S21", Full));
    }

    [Fact]
    public void Matches_TreatsOriginalAsAnything_SoReRunsDoNotNestNames()
    {
        var template = FileNameTemplate.Parse("{date:yyyy-MM-dd} {original}");
        var firstRun = template.Render(Full, 0);

        Assert.True(template.Matches(firstRun, Full with { Original = firstRun }));
    }

    [Fact]
    public void TemplateWithoutCounter_GetsOneAppended()
    {
        var template = FileNameTemplate.Parse("{date:yyyyMMdd}");

        Assert.Equal("20210408", template.Render(Full, 0));
        Assert.Equal("20210408 2", template.Render(Full, 2));
        Assert.True(template.Matches("20210408 2", Full));
    }
}
