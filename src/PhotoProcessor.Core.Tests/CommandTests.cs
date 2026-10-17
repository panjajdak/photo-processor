namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Commands;
using PhotoProcessor.Core.Dating;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.Journal;
using PhotoProcessor.Core.Tests.Support;

public class CommandTests
{
    private const string In = @"C:\in";
    private const string Out = @"C:\out";

    private readonly InMemoryFileSystem fileSystem = new();
    private readonly TestObserver observer = new();

    public CommandTests()
    {
        fileSystem.CreateDirectory(Out);
    }

    [Fact]
    public void Move_ExecutesAndUndoes()
    {
        fileSystem.Add($@"{In}\a.jpg", "photo");
        var executed = new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\b.jpg").Execute(fileSystem);

        Assert.Equal(["b.jpg"], fileSystem.FileNames(Out));
        Assert.Equal(UndoOutcome.Restored, executed.Undo(fileSystem, dryRun: false));
        Assert.Equal(["a.jpg"], fileSystem.FileNames(In));
        Assert.Empty(fileSystem.FileNames(Out));
    }

    [Fact]
    public void MoveUndo_SkipsFilesChangedAfterTheRun()
    {
        fileSystem.Add($@"{In}\a.jpg", "photo");
        var executed = new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\b.jpg").Execute(fileSystem);
        fileSystem.Add($@"{Out}\b.jpg", "edited photo");

        Assert.Equal(UndoResult.Skipped, executed.Undo(fileSystem, dryRun: false).Result);
        Assert.Equal(["b.jpg"], fileSystem.FileNames(Out));
    }

    [Fact]
    public void MoveUndo_NeverOverwritesTheOriginalLocation()
    {
        fileSystem.Add($@"{In}\a.jpg", "photo");
        var executed = new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\b.jpg").Execute(fileSystem);
        fileSystem.Add($@"{In}\a.jpg", "another file");

        Assert.Equal(UndoResult.Skipped, executed.Undo(fileSystem, dryRun: false).Result);
        Assert.Equal("another file"u8.ToArray(), fileSystem.Content($@"{In}\a.jpg"));
    }

    [Fact]
    public void MoveUndo_RecognizesAnEarlierUndo()
    {
        fileSystem.Add($@"{In}\a.jpg", "photo");
        var executed = new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\b.jpg").Execute(fileSystem);
        fileSystem.Move($@"{Out}\b.jpg", $@"{In}\a.jpg");

        Assert.Equal(UndoOutcome.AlreadyRestored, executed.Undo(fileSystem, dryRun: false));
    }

    [Fact]
    public void CopyUndo_RemovesCopyOnlyWhileOriginalIsIntact()
    {
        fileSystem.Add($@"{In}\a.jpg", "photo");
        var executed = new CopyFileCommand($@"{In}\a.jpg", $@"{Out}\b.jpg").Execute(fileSystem);
        fileSystem.Delete($@"{In}\a.jpg");

        Assert.Equal(UndoResult.Skipped, executed.Undo(fileSystem, dryRun: false).Result);
        Assert.Equal(["b.jpg"], fileSystem.FileNames(Out));

        fileSystem.Add($@"{In}\a.jpg", "photo");
        Assert.Equal(UndoOutcome.Restored, executed.Undo(fileSystem, dryRun: false));
        Assert.Empty(fileSystem.FileNames(Out));
    }

    [Fact]
    public void UndoDryRun_ChangesNothing()
    {
        fileSystem.Add($@"{In}\a.jpg", "photo");
        var executed = new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\b.jpg").Execute(fileSystem);
        var writesBefore = fileSystem.Writes;

        Assert.Equal(UndoOutcome.Restored, executed.Undo(fileSystem, dryRun: true));
        Assert.Equal(writesBefore, fileSystem.Writes);
    }

    [Fact]
    public void Executor_DescribeEmitsDryRunEventsWithoutWriting()
    {
        var batches = new[] { new CommandBatch([new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\b.jpg")], DateSource.FileName) };

        new CommandExecutor(fileSystem, observer).Describe(batches);

        Assert.Equal([new FileRenamed($@"{In}\a.jpg", $@"{Out}\b.jpg", DateSource.FileName, DryRun: true)], observer.Events);
        Assert.Equal(1, fileSystem.Writes);
    }

    [Fact]
    public void Executor_RecordsEveryCommandAndContinuesAfterAFailure()
    {
        fileSystem.Add($@"{In}\a.jpg", "a");
        fileSystem.Add($@"{In}\c.jpg", "c");
        var journal = new RunJournal(fileSystem, Out);
        var batches = new[]
        {
            new CommandBatch([new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\A.jpg")], DateSource.FileName),
            new CommandBatch([new MoveFileCommand($@"{In}\missing.jpg", $@"{Out}\B.jpg")], DateSource.FileName),
            new CommandBatch([new MoveFileCommand($@"{In}\c.jpg", $@"{Out}\C.jpg")], DateSource.FileName),
        };

        new CommandExecutor(fileSystem, observer).Execute(batches, journal, "Renaming", CancellationToken.None);

        Assert.Equal(["A.jpg", "C.jpg"], fileSystem.FileNames(Out));
        Assert.Equal([$@"{In}\missing.jpg"], observer.Of<FileFailed>().Single().Paths);
        Assert.Equal([$@"{Out}\A.jpg", $@"{Out}\C.jpg"], RunJournal.Read(fileSystem, journal.Path!).Select(e => e.Command.To));
    }

    [Fact]
    public void Executor_StopsBetweenBatchesWhenCancelled()
    {
        fileSystem.Add($@"{In}\a.jpg", "a");
        fileSystem.Add($@"{In}\a.dng", "raw");
        fileSystem.Add($@"{In}\b.jpg", "b");
        using var cancellation = new CancellationTokenSource();
        var cancelAfterFirstBatch = new TestObserver(e =>
        {
            if (e is ProgressChanged)
            {
                cancellation.Cancel();
            }
        });
        var batches = new[]
        {
            new CommandBatch([new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\A.jpg"), new MoveFileCommand($@"{In}\a.dng", $@"{Out}\A.dng")], DateSource.FileName),
            new CommandBatch([new MoveFileCommand($@"{In}\b.jpg", $@"{Out}\B.jpg")], DateSource.FileName),
        };

        new CommandExecutor(fileSystem, cancelAfterFirstBatch).Execute(batches, new RunJournal(fileSystem, Out), "Renaming", cancellation.Token);

        Assert.Equal(["A.dng", "A.jpg"], fileSystem.FileNames(Out));
        Assert.Equal(["b.jpg"], fileSystem.FileNames(In));
    }

    [Fact]
    public void Journal_RoundTripsCommands()
    {
        fileSystem.Add($@"{In}\a.jpg", "a");
        fileSystem.Add($@"{In}\b.jpg", "b");
        var journal = new RunJournal(fileSystem, Out);

        journal.Record(new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\A.jpg").Execute(fileSystem));
        journal.Record(new CopyFileCommand($@"{In}\b.jpg", $@"{Out}\B.jpg").Execute(fileSystem));
        var read = RunJournal.Read(fileSystem, journal.Path!);

        Assert.Equal<IFileCommand>(
            [new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\A.jpg"), new CopyFileCommand($@"{In}\b.jpg", $@"{Out}\B.jpg")],
            read.Select(e => e.Command));
        Assert.Equal([1L, 1L], read.Select(e => e.Size));
    }

    [Fact]
    public void Journal_ReadsRunsWrittenByEarlierVersions()
    {
        const string earlierVersionLine = """{"op":"move","from":"C:\\in\\IMG_1.jpg","to":"C:\\out\\IMG 2021-04-08 19.41.08.jpg","size":2481733,"modifiedUtc":"2024-06-01T08:00:03Z"}""";
        fileSystem.Add($@"{Out}\run.jsonl", earlierVersionLine);

        var executed = RunJournal.Read(fileSystem, $@"{Out}\run.jsonl").Single();

        Assert.Equal(new MoveFileCommand(@"C:\in\IMG_1.jpg", @"C:\out\IMG 2021-04-08 19.41.08.jpg"), executed.Command);
        Assert.Equal(2481733, executed.Size);
    }

    [Fact]
    public void Undoer_RevertsNewestFirstAndReportsEachFile()
    {
        fileSystem.Add($@"{In}\a.jpg", "a");
        var journal = new RunJournal(fileSystem, Out);
        journal.Record(new MoveFileCommand($@"{In}\a.jpg", $@"{Out}\A.jpg").Execute(fileSystem));
        journal.Record(new MoveFileCommand($@"{Out}\A.jpg", $@"{Out}\A2.jpg").Execute(fileSystem));

        var summary = new Undoer(observer, fileSystem).Undo(journal.Path!);

        Assert.Equal(new UndoSummary(2, 0, 0), summary);
        Assert.Equal(["a.jpg"], fileSystem.FileNames(In));
        Assert.Equal([$@"{Out}\A2.jpg", $@"{Out}\A.jpg"], observer.Of<FileRestored>().Select(e => e.To));
    }
}
