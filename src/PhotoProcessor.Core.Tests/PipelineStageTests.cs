namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Commands;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Dating;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.Metadata;
using PhotoProcessor.Core.Naming;
using PhotoProcessor.Core.Pipeline;
using PhotoProcessor.Core.Tests.Support;

public class PipelineStageTests
{
    private const string In = @"C:\in";
    private const string Out = @"C:\out";
    private static readonly DateTime Taken = new(2021, 4, 8, 19, 41, 8);

    private readonly InMemoryFileSystem fileSystem = new();
    private readonly TestObserver observer = new();
    private readonly Configuration config = new()
    {
        TimeZone = "UTC",
        DeviceMapping = new() { ["SM-A415F"] = "A41" },
    };

    [Fact]
    public void Discovery_KeepsMediaAndSidecars_ReportsTheRest()
    {
        fileSystem.Add($@"{In}\b.jpg", "b");
        fileSystem.Add($@"{In}\a.xmp", "a");
        fileSystem.Add($@"{In}\notes.txt", "n");

        var files = new FileDiscovery(fileSystem, observer).Discover(In);

        Assert.Equal([$@"{In}\a.xmp", $@"{In}\b.jpg"], files);
        Assert.Equal([new UnsupportedSkipped($@"{In}\notes.txt")], observer.Events);
    }

    [Fact]
    public void Grouping_PutsCompanionsAndSidecarsTogether_ReportsOrphans()
    {
        string[] files = [$@"{In}\IMG_1.HEIC", $@"{In}\IMG_1.MOV", $@"{In}\IMG_1.HEIC.xmp", $@"{In}\IMG_2.jpg", $@"{In}\IMG_9.AAE"];

        var groups = new MediaGrouping(observer).Group(files);

        Assert.Equal(2, groups.Count);
        Assert.Equal(["IMG_1", "IMG_2"], groups.Select(g => g.Stem));
        Assert.Equal(3, groups[0].Paths.Count);
        Assert.Equal([new OrphanSidecarSkipped($@"{In}\IMG_9.AAE")], observer.Events);
    }

    [Fact]
    public void Analysis_NamesGroupAfterBestDatedMember()
    {
        fileSystem.Add($@"{In}\IMG_1.CR3", "raw");
        fileSystem.Add($@"{In}\IMG_1.JPG", "jpg");
        var reader = new FakeMetadataReader()
            .Unreadable($@"{In}\IMG_1.CR3")
            .With($@"{In}\IMG_1.JPG", new MediaMetadata { DateTimeOriginal = Taken, Make = "samsung", Model = "SM-A415F" });

        var group = Analyze(reader, new MediaGroup("IMG_1", [$@"{In}\IMG_1.CR3", $@"{In}\IMG_1.JPG"]));

        Assert.Equal(new NameContext("IMG", Taken, "A41", "samsung", "SM-A415F", "IMG_1"), group.Context);
        Assert.Equal(DateSource.ExifOriginal, group.DateSource);
        Assert.Equal([".cr3", ".jpg"], group.Members.Select(m => m.Extension));
        Assert.Equal([new MetadataUnreadable($@"{In}\IMG_1.CR3", "File format could not be determined")], observer.Of<MetadataUnreadable>());
    }

    [Fact]
    public void Analysis_ReportsGroupsWhoseFilesWouldCollide()
    {
        fileSystem.Add($@"{In}\photo.JPG", "a");
        fileSystem.Add($@"{In}\photo.jpeg", "b");

        var groups = new GroupAnalysis(config, new FakeMetadataReader(), fileSystem, observer)
            .Analyze([new MediaGroup("photo", [$@"{In}\photo.JPG", $@"{In}\photo.jpeg"])], CancellationToken.None);

        Assert.Empty(groups);
        Assert.Equal(2, observer.Of<FileFailed>().Single().Paths.Count);
    }

    [Fact]
    public void Planning_MovesWholeGroupToFirstFreeCounter()
    {
        fileSystem.Add($@"{Out}\IMG 2021-04-08 19.41.08.jpg", "existing");
        fileSystem.Add($@"{In}\DSC_1.jpg", "new jpg");
        fileSystem.Add($@"{In}\DSC_1.dng", "new raw");

        var batches = Plan(Group("DSC_1", ".jpg", ".dng"));

        Assert.Equal(
            [new MoveFileCommand($@"{In}\DSC_1.jpg", $@"{Out}\IMG 2021-04-08 19.41.08 1.jpg"), new MoveFileCommand($@"{In}\DSC_1.dng", $@"{Out}\IMG 2021-04-08 19.41.08 1.dng")],
            batches.Single().Commands);
    }

    [Fact]
    public void Planning_NumbersGroupsWithTheSameNameInShootingOrder()
    {
        fileSystem.Add($@"{In}\b.jpg", "b");
        fileSystem.Add($@"{In}\a.jpg", "a");

        var batches = Plan(Group("b", ".jpg") with { SubSecondSortKey = "100000000" }, Group("a", ".jpg") with { SubSecondSortKey = "500000000" });

        Assert.Equal(
            [$@"{Out}\IMG 2021-04-08 19.41.08.jpg", $@"{Out}\IMG 2021-04-08 19.41.08 1.jpg"],
            batches.Select(b => b.Commands.Single().To));
        Assert.Equal([$@"{In}\b.jpg", $@"{In}\a.jpg"], batches.Select(b => b.Commands.Single().From));
    }

    [Fact]
    public void Planning_SkipsDuplicatesUnderAnyName()
    {
        fileSystem.Add($@"{Out}\renamed by hand.jpg", "same bytes");
        fileSystem.Add($@"{In}\IMG_1.jpg", "same bytes");

        var batches = Plan(Group("IMG_1", ".jpg"));

        Assert.Empty(batches);
        Assert.Equal([new DuplicateSkipped($@"{In}\IMG_1.jpg", $@"{Out}\renamed by hand.jpg")], observer.Of<DuplicateSkipped>());
    }

    [Fact]
    public void Planning_TreatsSecondIdenticalFileInBatchAsDuplicate()
    {
        fileSystem.Add($@"{In}\a.jpg", "same");
        fileSystem.Add($@"{In}\b.jpg", "same");

        var batches = Plan(Group("a", ".jpg"), Group("b", ".jpg"));

        Assert.Single(batches);
        Assert.Single(observer.Of<DuplicateSkipped>());
    }

    [Fact]
    public void Planning_LeavesAlreadyNamedGroupsAlone()
    {
        fileSystem.Add($@"{Out}\IMG 2021-04-08 19.41.08 3.jpg", "x");
        var named = new AnalyzedGroup(
            "IMG 2021-04-08 19.41.08 3",
            Out,
            [new GroupMember($@"{Out}\IMG 2021-04-08 19.41.08 3.jpg", ".jpg")],
            new NameContext("IMG", Taken, null, null, null, "IMG 2021-04-08 19.41.08 3"),
            DateSource.ExifOriginal,
            "");

        var batches = Plan(named);

        Assert.Empty(batches);
        Assert.Equal([new AlreadyNamed($@"{Out}\IMG 2021-04-08 19.41.08 3.jpg")], observer.Events.Where(e => e is not ProgressChanged));
    }

    [Fact]
    public void Planning_CopyModeLeavesSourceNamesTaken()
    {
        fileSystem.Add($@"{In}\IMG 2021-04-08 19.41.08.jpg", "already there");
        fileSystem.Add($@"{In}\x.jpg", "new");

        var batches = new RenamePlanning(FileNameTemplate.Parse(config.FileNameTemplate), fileSystem, observer, copy: true)
            .Plan([Group("x", ".jpg")], In, CancellationToken.None);

        Assert.Equal([new CopyFileCommand($@"{In}\x.jpg", $@"{In}\IMG 2021-04-08 19.41.08 1.jpg")], batches.Single().Commands);
    }

    [Fact]
    public void Runner_WorksEndToEndWithoutTouchingDisk()
    {
        fileSystem.Add($@"{In}\IMG_1.jpg", "photo");
        fileSystem.Add($@"{In}\IMG_1.xmp", "edits");
        fileSystem.Add($@"{In}\notes.txt", "n");
        var reader = new FakeMetadataReader().With($@"{In}\IMG_1.jpg", new MediaMetadata { DateTimeOriginal = Taken, Model = "SM-A415F" });

        var result = new RenameRunner(config, observer, reader, fileSystem).Run(In, Out);

        Assert.Equal(new ProcessingSummary(2, 0, 0, 1, 0), result.Summary);
        Assert.Equal(["IMG 2021-04-08 19.41.08 A41.jpg", "IMG 2021-04-08 19.41.08 A41.xmp"], fileSystem.FileNames(Out));
        Assert.True(fileSystem.FileExists(result.JournalPath!));
    }

    [Fact]
    public void Runner_DryRunWritesNothing()
    {
        fileSystem.Add($@"{In}\IMG_1.jpg", "photo");
        var reader = new FakeMetadataReader().With($@"{In}\IMG_1.jpg", new MediaMetadata { DateTimeOriginal = Taken });

        var result = new RenameRunner(config, observer, reader, fileSystem).Run(In, Out, dryRun: true);

        Assert.Equal(0, fileSystem.Writes);
        Assert.Equal(1, result.Summary.Renamed);
        Assert.True(observer.Of<FileRenamed>().Single().DryRun);
    }

    private AnalyzedGroup Analyze(FakeMetadataReader reader, MediaGroup group)
        => new GroupAnalysis(config, reader, fileSystem, observer).Analyze([group], CancellationToken.None).Single();

    private IReadOnlyList<CommandBatch> Plan(params AnalyzedGroup[] groups)
        => new RenamePlanning(FileNameTemplate.Parse(config.FileNameTemplate), fileSystem, observer, copy: false)
            .Plan(groups, Out, CancellationToken.None);

    private static AnalyzedGroup Group(string stem, params string[] extensions)
        => new(
            stem,
            In,
            extensions.Select(e => new GroupMember($@"{In}\{stem}{e}", e)).ToList(),
            new NameContext("IMG", Taken, null, null, null, stem),
            DateSource.ExifOriginal,
            "");
}
