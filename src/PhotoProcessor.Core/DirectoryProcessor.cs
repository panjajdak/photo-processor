namespace PhotoProcessor.Core;

using System;
using System.IO;
using System.Linq;
using PhotoProcessor.Core.Abstractions;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Resolvers;

public class DirectoryProcessor
{
    private readonly ConverterResolver converterResolver;
    private readonly ILog log;

    public DirectoryProcessor(Configuration config, ILog log)
    {
        converterResolver = new ConverterResolver(config);
        this.log = log;
    }

    public void Process(string inputDirectory, string outputDirectory)
    {
        var processedCount = 0;
        var filePaths = Directory.GetFiles(inputDirectory)
                                 .ToList();

        foreach (var filePath in filePaths)
        {
            try
            {
                var converter = converterResolver.Resolve(filePath);

                if (converter == null)
                {
                    log.LogMessage($"{filePath}: no matching converter");
                    processedCount++;
                    continue;
                }

                converter.Process(filePath, outputDirectory);
                processedCount++;
                log.LogMessage($"Processed {processedCount}/{filePaths.Count}");
            }
            catch (Exception ex)
            {
                log.LogError(ex);
                log.LogMessage(filePath);
            }
        }
    }
}
