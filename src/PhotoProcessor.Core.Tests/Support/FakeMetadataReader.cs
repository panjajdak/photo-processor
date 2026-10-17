namespace PhotoProcessor.Core.Tests.Support;

using PhotoProcessor.Core.Metadata;

internal sealed class FakeMetadataReader : IMetadataReader
{
    private readonly Dictionary<string, MediaMetadata> metadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unreadable = new(StringComparer.OrdinalIgnoreCase);

    public FakeMetadataReader With(string path, MediaMetadata value)
    {
        metadata[path] = value;
        return this;
    }

    public FakeMetadataReader Unreadable(string path)
    {
        unreadable.Add(path);
        return this;
    }

    public MediaMetadata Read(string path)
        => unreadable.Contains(path)
            ? throw new InvalidDataException("File format could not be determined")
            : metadata.GetValueOrDefault(path, MediaMetadata.Empty);
}
