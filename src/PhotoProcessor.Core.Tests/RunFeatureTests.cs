namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Journal;
using PhotoProcessor.Core.Tests.Support;

/// <summary>
/// Dry run, duplicate detection, custom templates and journal/undo through the whole processor.
/// </summary>
public sealed class RunFeatureTests : IDisposable
{
    private static readonly DateTime Taken = new(2021, 4, 8, 19, 41, 8);

    private readonly TempDirectory input = new();
    private readonly TempDirectory output = new();
    private readonly TestLog log = new();
    private readonly Configuration config = new()
    {
        TimeZone = "UTC",
        DeviceMapping = new() { ["SM-A415F"] = "A41" },
    };

    public void Dispose()
    {
        input.Dispose();
        output.Dispose();
    }

    // --- Dry run ---

    [Fact]
    public void DryRun_ChangesNothing_AndPredictsTheRealRun()
    {
        var existing = TestMedia.Jpeg(dateTimeOriginal: Taken, subSecondOriginal: "1");
        output.Write("IMG 2021-04-08 19.41.08.jpg", existing);
        input.Write("dup.jpg", existing);
        input.Write("a.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken, subSecondOriginal: "2"));
        input.Write("b.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken, subSecondOriginal: "3"));
        input.Write("b.xmp", [1]);
        input.Write("notes.txt", [1]);

        var dry = Process(dryRun: true);
        var predicted = log.Messages.Where(m => m.StartsWith("Would rename")).Select(m => m["Would rename ".Length..]).ToList();
        log.Messages.Clear();

        Assert.Equal(["a.jpg", "b.jpg", "b.xmp", "dup.jpg", "notes.txt"], input.FileNames());
        Assert.Equal(["IMG 2021-04-08 19.41.08.jpg"], output.FileNames());
        Assert.Null(dry.JournalPath);
        Assert.False(Directory.Exists(Path.Combine(output.Path, RunJournal.FolderName)));

        var real = Process();

        Assert.Equal(real.Summary, dry.Summary);
        Assert.Equal(new ProcessingSummary(3, 0, 1, 1, 0), real.Summary);
        Assert.Equal(predicted, log.Messages.Where(m => m.Contains(" -> ")).ToList());
    }

    [Fact]
    public void DryRun_DoesNotCreateOutputDirectory()
    {
        var target = Path.Combine(output.Path, "new");
        input.Write("a.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken));

        var result = Process(dryRun: true, outputDirectory: target);

        Assert.Equal(1, result.Summary.Renamed);
        Assert.False(Directory.Exists(target));
    }

    // --- Duplicates ---

    [Fact]
    public void CopyAlreadyInOutputUnderAnotherName_IsSkipped()
    {
        var photo = TestMedia.Jpeg(dateTimeOriginal: Taken);
        output.Write("renamed by hand.jpg", photo);
        input.Write("IMG_1.jpg", photo);

        var result = Process();

        Assert.Equal(new ProcessingSummary(0, 0, 1, 0, 0), result.Summary);
        Assert.Equal(["IMG_1.jpg"], input.FileNames());
        Assert.Contains(log.Messages, m => m.Contains("renamed by hand.jpg"));
    }

    [Fact]
    public void IdenticalFilesInInput_OnlyFirstIsRenamed()
    {
        var photo = TestMedia.Jpeg(dateTimeOriginal: Taken);
        input.Write("IMG_1.jpg", photo);
        input.Write("IMG_1 (1).jpg", photo);

        var result = Process();

        Assert.Equal(new ProcessingSummary(1, 0, 1, 0, 0), result.Summary);
        Assert.Equal(["IMG 2021-04-08 19.41.08.jpg"], output.FileNames());
        Assert.Single(input.FileNames());
    }

    [Fact]
    public void InPlace_IdenticalUnnamedFiles_OneIsRenamedNotBothSkipped()
    {
        var photo = TestMedia.Jpeg(dateTimeOriginal: Taken);
        input.Write("IMG_1.jpg", photo);
        input.Write("IMG_1 (1).jpg", photo);

        var result = Process(outputDirectory: input.Path);

        Assert.Equal(new ProcessingSummary(1, 0, 1, 0, 0), result.Summary);
        Assert.Contains("IMG 2021-04-08 19.41.08.jpg", input.FileNames());
    }

    [Fact]
    public void InPlace_CopyOfAlreadyNamedFile_IsDuplicate()
    {
        var photo = TestMedia.Jpeg(dateTimeOriginal: Taken);
        input.Write("IMG 2021-04-08 19.41.08.jpg", photo);
        input.Write("zz-copy.jpg", photo);

        var result = Process(outputDirectory: input.Path);

        Assert.Equal(new ProcessingSummary(0, 1, 1, 0, 0), result.Summary);
    }

    // --- Templates ---

    [Fact]
    public void CustomTemplate_IsUsedAndReRunsAreStable()
    {
        config.FileNameTemplate = "{date:yyyyMMdd_HHmmss}[_{device}] {original}";
        input.Write("IMG_1.jpg", TestMedia.Jpeg(model: "SM-A415F", dateTimeOriginal: Taken));
        input.Write("IMG_2.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken.AddMinutes(1)));

        Process(outputDirectory: input.Path);
        var names = input.FileNames();
        var second = Process(outputDirectory: input.Path);

        Assert.Equal(["20210408_194108_A41 IMG_1.jpg", "20210408_194208 IMG_2.jpg"], names);
        Assert.Equal(new ProcessingSummary(0, 2, 0, 0, 0), second.Summary);
        Assert.Equal(names, input.FileNames());
    }

    [Fact]
    public void ChangingTheTemplate_RenamesToTheNewPattern()
    {
        input.Write("IMG 2021-04-08 19.41.08.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken));
        config.FileNameTemplate = "{type}_{date:yyyyMMdd_HHmmss}";

        Process(outputDirectory: input.Path);

        Assert.Equal(["IMG_20210408_194108.jpg"], input.FileNames());
    }

    [Fact]
    public void InvalidTemplate_FailsBeforeTouchingFiles()
    {
        config.FileNameTemplate = "{date:HH:mm}";

        Assert.Throws<FormatException>(() => new DirectoryProcessor(config, log));
    }

    // --- Journal and undo ---

    [Fact]
    public void Undo_RestoresOriginalNamesAndLocations()
    {
        input.Write("DSC_1.JPG", TestMedia.Jpeg(dateTimeOriginal: Taken));
        input.Write("DSC_1.dng", TestMedia.Tiff(dateTimeOriginal: Taken));
        input.Write("DSC_2.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken, subSecondOriginal: "5"));
        var before = input.FileNames();

        var result = Process();
        Assert.NotNull(result.JournalPath);
        Assert.Equal(result.JournalPath, Undoer.FindLatestJournal(output.Path));

        var summary = new Undoer(log).Undo(result.JournalPath!);

        Assert.Equal(new UndoSummary(3, 0, 0), summary);
        Assert.Equal(before, input.FileNames());
        Assert.Empty(output.FileNames());
        Assert.Null(Undoer.FindLatestJournal(output.Path));
    }

    [Fact]
    public void Undo_InPlaceRun()
    {
        input.Write("IMG_1.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken));
        input.Write("IMG_1.xmp", [1]);

        var result = Process(outputDirectory: input.Path);
        Assert.Equal(["IMG 2021-04-08 19.41.08.jpg", "IMG 2021-04-08 19.41.08.xmp"], input.FileNames());

        new Undoer(log).Undo(result.JournalPath!);

        Assert.Equal(["IMG_1.jpg", "IMG_1.xmp"], input.FileNames());
    }

    [Fact]
    public void Undo_CopyRun_RemovesCopiesOnly()
    {
        config.Options.CopyingEnabled = true;
        input.Write("IMG_1.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken));

        var result = Process();
        var summary = new Undoer(log).Undo(result.JournalPath!);

        Assert.Equal(new UndoSummary(1, 0, 0), summary);
        Assert.Equal(["IMG_1.jpg"], input.FileNames());
        Assert.Empty(output.FileNames());
    }

    [Fact]
    public void Undo_CopyRun_KeepsCopyWhenOriginalIsGone()
    {
        config.Options.CopyingEnabled = true;
        var original = input.Write("IMG_1.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken));

        var result = Process();
        File.Delete(original);
        var summary = new Undoer(log).Undo(result.JournalPath!);

        Assert.Equal(new UndoSummary(0, 0, 1), summary);
        Assert.Equal(["IMG 2021-04-08 19.41.08.jpg"], output.FileNames());
    }

    [Fact]
    public void Undo_SkipsModifiedFiles_AndKeepsJournalForRetry()
    {
        input.Write("IMG_1.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken));
        input.Write("IMG_2.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken.AddSeconds(1)));

        var result = Process();
        File.AppendAllText(Path.Combine(output.Path, "IMG 2021-04-08 19.41.09.jpg"), "edited");
        var summary = new Undoer(log).Undo(result.JournalPath!);

        Assert.Equal(new UndoSummary(1, 0, 1), summary);
        Assert.Equal(["IMG_1.jpg"], input.FileNames());
        Assert.Equal(result.JournalPath, Undoer.FindLatestJournal(output.Path));
        Assert.Contains(log.Warnings, w => w.Contains("modified"));
    }

    [Fact]
    public void Undo_NeverOverwritesAFileAtTheOriginalLocation()
    {
        input.Write("IMG_1.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken));

        var result = Process();
        input.Write("IMG_1.jpg", [9, 9, 9]);
        var summary = new Undoer(log).Undo(result.JournalPath!);

        Assert.Equal(new UndoSummary(0, 0, 1), summary);
        Assert.Equal([9, 9, 9], File.ReadAllBytes(Path.Combine(input.Path, "IMG_1.jpg")));
        Assert.Single(output.FileNames());
    }

    [Fact]
    public void Undo_IsSafeToRepeat()
    {
        input.Write("IMG_1.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken));
        input.Write("IMG_2.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken.AddSeconds(1)));

        var result = Process();
        File.Move(Path.Combine(output.Path, "IMG 2021-04-08 19.41.08.jpg"), Path.Combine(input.Path, "IMG_1.jpg"));
        var summary = new Undoer(log).Undo(result.JournalPath!);

        Assert.Equal(new UndoSummary(1, 1, 0), summary);
        Assert.Equal(["IMG_1.jpg", "IMG_2.jpg"], input.FileNames());
    }

    [Fact]
    public void UndoDryRun_ChangesNothing()
    {
        input.Write("IMG_1.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken));

        var result = Process();
        var summary = new Undoer(log).Undo(result.JournalPath!, dryRun: true);

        Assert.Equal(new UndoSummary(1, 0, 0), summary);
        Assert.Single(output.FileNames());
        Assert.Equal(result.JournalPath, Undoer.FindLatestJournal(output.Path));
    }

    [Fact]
    public void RunThatChangesNothing_WritesNoJournal()
    {
        input.Write("notes.txt", [1]);

        var result = Process();

        Assert.Null(result.JournalPath);
        Assert.Null(Undoer.FindLatestJournal(output.Path));
    }

    [Fact]
    public void LatestJournal_IsUndoneFirst()
    {
        input.Write("IMG_1.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken));
        var first = Process();
        Thread.Sleep(5);
        input.Write("IMG_2.jpg", TestMedia.Jpeg(dateTimeOriginal: Taken.AddSeconds(1)));
        var second = Process();

        Assert.Equal(second.JournalPath, Undoer.FindLatestJournal(output.Path));
        new Undoer(log).Undo(second.JournalPath!);
        Assert.Equal(first.JournalPath, Undoer.FindLatestJournal(output.Path));
        Assert.Equal(["IMG_2.jpg"], input.FileNames());
    }

    private ProcessingResult Process(bool dryRun = false, string? outputDirectory = null)
        => new DirectoryProcessor(config, log).Process(input.Path, outputDirectory ?? output.Path, dryRun);
}
