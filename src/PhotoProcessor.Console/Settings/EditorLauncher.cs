namespace PhotoProcessor.Console.Settings;

using System.ComponentModel;
using System.Diagnostics;

internal static class EditorLauncher
{
    public static string? ConfiguredEditor
        => new[] { "VISUAL", "EDITOR" }
            .Select(Environment.GetEnvironmentVariable)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    public static int OpenAndWait(string editor, string path)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/s /c \"{editor} \"{path}\"\"")
        {
            UseShellExecute = false,
        })!;
        process.WaitForExit();
        return process.ExitCode;
    }

    public static void OpenWithDefaultApp(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true })?.Dispose();
        }
    }
}
