namespace PhotoProcessor.Core;

using System;
using System.IO;

internal static class FileComparer
{
    /// <summary>
    /// Byte-by-byte comparison; cheaper than hashing because files of different size are rejected without reading.
    /// </summary>
    public static bool AreIdentical(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length)
        {
            return false;
        }

        const int BufferSize = 1 << 16;
        using var streamA = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        using var streamB = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
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
