namespace PhotoProcessor.Core.Metadata;

internal interface IMetadataReader
{
    MediaMetadata Read(string path);
}
