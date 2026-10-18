namespace PhotoProcessor.Console.Tests;

using System.Diagnostics;
using System.Text.Json;

public sealed class ConfigCommandTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PhotoProcessorConsoleTests", Guid.NewGuid().ToString("N"));

    public ConfigCommandTests()
    {
        Directory.CreateDirectory(root);
    }

    private string ConfigPath => Path.Combine(root, "settings", "config.json");

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void Init_CreatesTheConfigAndRefusesToOverwrite()
    {
        Assert.Equal(0, Run("config", "init").ExitCode);
        Assert.True(File.Exists(ConfigPath));

        var second = Run("config", "init");

        Assert.Equal(2, second.ExitCode);
        Assert.Contains("--force", second.Error);
    }

    [Fact]
    public void Set_ChangesTheFileAndShowReflectsIt()
    {
        Run("config", "init");

        var set = Run("config", "set", "TimeZone", "Europe/Warsaw");
        var shown = JsonDocument.Parse(Run("config", "show", "--json").Output);

        Assert.Equal(0, set.ExitCode);
        Assert.Contains("TimeZone = Europe/Warsaw", set.Output);
        Assert.Equal("Europe/Warsaw", shown.RootElement.GetProperty("TimeZone").GetString());
    }

    [Fact]
    public void Set_RejectsInvalidValuesAndKeepsTheFile()
    {
        Run("config", "init");
        var before = File.ReadAllText(ConfigPath);

        var result = Run("config", "set", "FileNameTemplate", "{date:HH:mm}");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("HH:mm", result.Error);
        Assert.Equal(before, File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void Device_AddChangeListAndRemove()
    {
        Assert.Equal(0, Run("config", "device", "add", "SM-S911B", "S23").ExitCode);
        Assert.Contains("Changed", Run("config", "device", "add", "sm-s911b", "GalaxyS23").Output);
        Assert.Contains("GalaxyS23", Run("config", "device", "list").Output);

        Assert.Equal(0, Run("config", "device", "remove", "SM-S911B").ExitCode);
        Assert.Contains("(none)", Run("config", "device").Output);
        Assert.Equal(2, Run("config", "device", "remove", "SM-S911B").ExitCode);
    }

    [Fact]
    public void Validate_ReportsProblemsThroughExitCode()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, "{ \"TimeZone\": \"Mars\" }");

        var result = Run("config", "validate");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Mars", result.Error);
        Assert.Equal("", result.Output);
    }

    [Fact]
    public void Path_PrintsTheConfigInUse()
    {
        Assert.Equal(ConfigPath, Run("config", "path").Output.Trim());
    }

    [Fact]
    public void Edit_CreatesTheFileOpensTheEditorAndValidates()
    {
        var result = Run(["config", "edit"], new Dictionary<string, string> { ["EDITOR"] = "type", ["VISUAL"] = "" });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Created", result.Output);
        Assert.Contains("\"FileNameTemplate\"", result.Output);
        Assert.Contains("The config is valid.", result.Output);
    }

    private CliResult Run(params string[] args) => Run(args, new Dictionary<string, string>());

    private CliResult Run(string[] args, Dictionary<string, string> environment)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "photoprocessor.exe"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        foreach (var arg in args.Append("--config").Append(ConfigPath))
        {
            start.ArgumentList.Add(arg);
        }

        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new CliResult(process.ExitCode, output.Result, error.Result);
    }

    private sealed record CliResult(int ExitCode, string Output, string Error);
}
