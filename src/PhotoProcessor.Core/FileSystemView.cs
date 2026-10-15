namespace PhotoProcessor.Core;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// The file system as the run sees it: real files, plus the moves and copies of a dry run that were only planned.
/// </summary>
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
