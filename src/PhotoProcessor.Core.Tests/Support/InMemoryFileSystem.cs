namespace PhotoProcessor.Core.Tests.Support;

using System.Text;
using PhotoProcessor.Core.IO;

internal sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, StoredFile> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);

    public int Writes { get; private set; }

    public IReadOnlyCollection<string> Directories => directories;

    public void Add(string path, byte[] content, DateTime? lastWriteTime = null)
    {
        directories.Add(Path.GetDirectoryName(path)!);
        files[path] = new StoredFile(content, lastWriteTime ?? new DateTime(2030, 1, 1, 12, 0, 0));
    }

    public void Add(string path, string content, DateTime? lastWriteTime = null) => Add(path, Encoding.UTF8.GetBytes(content), lastWriteTime);

    public byte[] Content(string path) => files[path].Content;

    public string[] FileNames(string directory)
        => GetFiles(directory).Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal).ToArray();

    public IReadOnlyList<string> GetFiles(string directory)
        => files.Keys.Where(p => string.Equals(Path.GetDirectoryName(p), directory, StringComparison.OrdinalIgnoreCase)).ToList();

    public bool FileExists(string path) => files.ContainsKey(path);

    public bool DirectoryExists(string path) => directories.Contains(path);

    public void CreateDirectory(string path, bool hidden = false)
    {
        Writes++;
        directories.Add(path);
    }

    public void Move(string from, string to)
    {
        var file = Take(from, to);
        files.Remove(from);
        files[to] = file;
    }

    public void Copy(string from, string to) => files[to] = Take(from, to);

    public void Delete(string path)
    {
        Writes++;
        files.Remove(path);
    }

    public FileFacts GetFacts(string path)
    {
        var file = Get(path);
        return new FileFacts(file.Content.Length, file.LastWriteTime, file.LastWriteTime.ToUniversalTime());
    }

    public Stream OpenRead(string path) => new MemoryStream(Get(path).Content, writable: false);

    public IEnumerable<string> ReadLines(string path)
        => Encoding.UTF8.GetString(Get(path).Content).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r'));

    public void AppendLine(string path, string line)
    {
        Writes++;
        var existing = files.TryGetValue(path, out var file) ? file.Content : [];
        files[path] = new StoredFile([.. existing, .. Encoding.UTF8.GetBytes(line + "\n")], DateTime.Now);
    }

    private StoredFile Take(string from, string to)
    {
        Writes++;
        var file = Get(from);
        if (files.ContainsKey(to))
        {
            throw new IOException($"Cannot create '{to}' because a file with that name already exists.");
        }

        if (!directories.Contains(Path.GetDirectoryName(to)!))
        {
            throw new DirectoryNotFoundException(Path.GetDirectoryName(to));
        }

        return file;
    }

    private StoredFile Get(string path)
        => files.TryGetValue(path, out var file) ? file : throw new FileNotFoundException(path);

    private sealed record StoredFile(byte[] Content, DateTime LastWriteTime);
}
