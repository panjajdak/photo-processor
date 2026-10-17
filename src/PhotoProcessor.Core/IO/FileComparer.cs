namespace PhotoProcessor.Core.IO;

using System;
using System.IO;

internal static class FileComparer
{
    public static bool AreIdentical(IFileSystem fileSystem, string a, string b)
    {
        if (fileSystem.GetFacts(a).Length != fileSystem.GetFacts(b).Length)
        {
            return false;
        }

        const int BufferSize = 1 << 16;
        using var streamA = fileSystem.OpenRead(a);
        using var streamB = fileSystem.OpenRead(b);
        var bufferA = new byte[BufferSize];
        var bufferB = new byte[BufferSize];

        while (true)
        {
            var readA = streamA.ReadAtLeast(bufferA, BufferSize, throwOnEndOfStream: false);
            var readB = streamB.ReadAtLeast(bufferB, BufferSize, throwOnEndOfStream: false);
            if (readA != readB || !bufferA.AsSpan(0, readA).SequenceEqual(bufferB.AsSpan(0, readB)))
            {
                return false;
            }

            if (readA == 0)
            {
                return true;
            }
        }
    }

    public static bool PathsEqual(string a, string b)
        => string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
