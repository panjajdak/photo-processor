namespace PhotoProcessor.Core;

using System.IO;
using System.Threading;
using PhotoProcessor.Core.Commands;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.IO;
using PhotoProcessor.Core.Journal;
using PhotoProcessor.Core.Metadata;
using PhotoProcessor.Core.Naming;
using PhotoProcessor.Core.Pipeline;

/// <summary>Renames the photos and videos in a folder.</summary>
public sealed class RenameRunner
{
    private readonly Configuration config;
    private readonly IProcessingObserver observer;
    private readonly IMetadataReader metadataReader;
    private readonly IFileSystem fileSystem;
    private readonly FileNameTemplate template;

    /// <summary>Creates a runner; throws <see cref="InvalidConfigurationException"/> for an invalid configuration.</summary>
    public RenameRunner(Configuration config, IProcessingObserver observer)
        : this(config, observer, new MetadataExtractorReader(), PhysicalFileSystem.Instance)
    { }

    internal RenameRunner(Configuration config, IProcessingObserver observer, IMetadataReader metadataReader, IFileSystem fileSystem)
    {
        ConfigurationCheck.EnsureValid(config);
        this.config = config;
        this.observer = observer;
        this.metadataReader = metadataReader;
        this.fileSystem = fileSystem;
        template = FileNameTemplate.Parse(config.FileNameTemplate);
    }

    /// <summary>Renames, moves or copies the files of the input folder into the output folder.</summary>
    public ProcessingResult Run(string inputDirectory, string outputDirectory, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        inputDirectory = Path.GetFullPath(inputDirectory);
        outputDirectory = Path.GetFullPath(outputDirectory);

        var counter = new SummaryCounter();
        var events = new CompositeObserver(counter, observer);
        var copy = config.Options.CopyingEnabled;

        var files = new FileDiscovery(fileSystem, events).Discover(inputDirectory);
        var groups = new MediaGrouping(events).Group(files);
        var analyzed = new GroupAnalysis(config, metadataReader, fileSystem, events).Analyze(groups, cancellationToken);
        var batches = new RenamePlanning(template, fileSystem, events, copy).Plan(analyzed, outputDirectory, cancellationToken);

        var executor = new CommandExecutor(fileSystem, events);
        string? journalPath = null;
        if (dryRun)
        {
            executor.Describe(batches);
        }
        else
        {
            fileSystem.CreateDirectory(outputDirectory);
            var journal = new RunJournal(fileSystem, outputDirectory);
            executor.Execute(batches, journal, copy ? "Copying" : "Renaming", cancellationToken);
            journalPath = journal.Path;
        }

        return new ProcessingResult(counter.Summary, journalPath, dryRun, cancellationToken.IsCancellationRequested);
    }
}
