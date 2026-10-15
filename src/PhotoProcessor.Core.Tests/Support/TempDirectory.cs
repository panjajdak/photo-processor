namespace PhotoProcessor.Core.Tests.Support;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PhotoProcessorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Write(string fileName, byte[] content, DateTime? lastWriteTime = null)
    {
        var path = System.IO.Path.Combine(Path, fileName);
        File.WriteAllBytes(path, content);
        if (lastWriteTime != null)
        {
            File.SetLastWriteTime(path, lastWriteTime.Value);
        }

        return path;
    }

    public string[] FileNames()
        => Directory.GetFiles(Path).Select(System.IO.Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
