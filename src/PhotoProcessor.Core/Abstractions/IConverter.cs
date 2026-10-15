namespace PhotoProcessor.Core.Abstractions;

using System.Collections.Generic;

internal interface IConverter
{
    public static IEnumerable<string> HandledExtensions { get; }

    public void Process(string path, string outputDirectory);

    public bool CanProcess(string extension);
}
