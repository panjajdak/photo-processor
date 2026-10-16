namespace PhotoProcessor.Core;

using System;
using System.Collections.Generic;
using System.IO;

internal sealed class FileSystemView
{
    private readonly HashSet<string> planned = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> vacated = new(StringComparer.OrdinalIgnoreCase);

    public bool Exists(string path)
        => planned.Contains(path) || (!vacated.Contains(path) && File.Exists(path));

    public void RecordPlanned(string source, string target, bool move)
    {
        planned.Add(target);
        if (move)
        {
            vacated.Add(source);
            planned.Remove(source);
        }
    }
}
