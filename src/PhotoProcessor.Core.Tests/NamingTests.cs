namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Naming;

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
}
