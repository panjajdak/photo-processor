namespace PhotoProcessor.Core.Resolvers;

using System.Collections.Generic;
using System.Linq;
using PhotoProcessor.Core.Abstractions;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Converters;

internal sealed class ConverterResolver
{
    private readonly List<IConverter> converters;

    public ConverterResolver(Configuration config)
    {
        converters = new List<IConverter>
        {
            new ImageConverter(config),
            new VideoConverter(config)
        };
    }

    public IConverter Resolve(string filePath)
    {
        string extension = filePath.Substring(filePath.LastIndexOf('.') + 1).ToLowerInvariant();
        return converters.FirstOrDefault(c => c.CanProcess(extension));
    }
}
