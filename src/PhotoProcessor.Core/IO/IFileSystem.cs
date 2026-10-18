namespace PhotoProcessor.Core.IO;

using System.Collections.Generic;
using System.IO;

internal interface IFileSystem
{
    IReadOnlyList<string> GetFiles(string directory);

    IReadOnlyList<string> GetDirectories(string directory);

    bool IsHidden(string path);

    bool FileExists(string path);

    bool DirectoryExists(string path);

    void CreateDirectory(string path, bool hidden = false);

    void Move(string from, string to);

    void Copy(string from, string to);

    void Delete(string path);

    FileFacts GetFacts(string path);

    Stream OpenRead(string path);

    IEnumerable<string> ReadLines(string path);

    void AppendLine(string path, string line);
}
