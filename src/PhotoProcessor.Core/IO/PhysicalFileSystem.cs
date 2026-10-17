namespace PhotoProcessor.Core.IO;

using System;
using System.Collections.Generic;
using System.IO;

internal sealed class PhysicalFileSystem : IFileSystem
{
    public static readonly PhysicalFileSystem Instance = new();

    public IReadOnlyList<string> GetFiles(string directory) => Directory.GetFiles(directory);

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path, bool hidden = false)
    {
        var directory = Directory.CreateDirectory(path);
        if (hidden)
        {
            directory.Attributes |= FileAttributes.Hidden;
        }
    }

    public void Move(string from, string to) => File.Move(from, to);

    public void Copy(string from, string to) => File.Copy(from, to);

    public void Delete(string path) => File.Delete(path);

    public FileFacts GetFacts(string path)
    {
        var info = new FileInfo(path);
        return new FileFacts(info.Length, info.LastWriteTime, info.LastWriteTimeUtc);
    }

    public Stream OpenRead(string path)
        => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1 << 16);

    public IEnumerable<string> ReadLines(string path) => File.ReadLines(path);

    public void AppendLine(string path, string line) => File.AppendAllText(path, line + Environment.NewLine);
}
