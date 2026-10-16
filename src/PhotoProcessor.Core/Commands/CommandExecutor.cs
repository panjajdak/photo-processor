namespace PhotoProcessor.Core.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.IO;
using PhotoProcessor.Core.Journal;

internal sealed class CommandExecutor(IFileSystem fileSystem, IProcessingObserver observer)
{
    public void Describe(IReadOnlyList<CommandBatch> batches)
    {
        foreach (var batch in batches)
        {
            foreach (var command in batch.Commands)
            {
                observer.On(command.ToEvent(batch.DateSource, dryRun: true));
            }
        }
    }

    public void Execute(IReadOnlyList<CommandBatch> batches, RunJournal journal, string stage, CancellationToken cancellationToken)
    {
        var total = batches.Sum(b => b.Commands.Count);
        var done = 0;

        foreach (var batch in batches)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            foreach (var command in batch.Commands)
            {
                try
                {
                    journal.Record(command.Execute(fileSystem));
                    observer.On(command.ToEvent(batch.DateSource, dryRun: false));
                }
                catch (Exception ex)
                {
                    observer.On(new FileFailed([command.From], ex));
                }
            }

            done += batch.Commands.Count;
            observer.On(new ProgressChanged(stage, done, total));
        }
    }
}
