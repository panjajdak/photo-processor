namespace PhotoProcessor.Core.Tests;

using PhotoProcessor.Core.Abstractions;
using PhotoProcessor.Core.Config;
using PhotoProcessor.Core.Tests.Support;

public sealed class DirectoryProcessorTests : IDisposable
{
    private static readonly DateTime FileTime = new(2030, 1, 1, 12, 0, 0);

    private readonly TempDirectory input = new();
    private readonly TempDirectory output = new();
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

    [Fact]
    public void RenamesUsingDateTakenAndDevice_NotTheEditDate()
    {
        input.Write("IMG_20210408_194108.JPEG", TestMedia.Jpeg(
            model: "SM-A415F",
            dateTime: new DateTime(2023, 1, 1, 10, 0, 0),
            dateTimeOriginal: new DateTime(2021, 4, 8, 19, 41, 8)));

        var summary = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(1, 0, 0, 0, 0), summary);
        Assert.Equal(["IMG 2021-04-08 19.41.08 A41.jpg"], output.FileNames());
        Assert.Empty(input.FileNames());
    }

    [Fact]
    public void RenamesVideosFromMovieHeader()
    {
        input.Write("20221113_135022.mp4", TestMedia.Mp4(new DateTime(2022, 11, 13, 12, 50, 22)));

        Process(input.Path, output.Path);

        Assert.Equal(["VID 2022-11-13 12.50.22.mp4"], output.FileNames());
    }

    [Fact]
    public void NumbersSameSecondShotsInSubSecondOrder_RegardlessOfFileNames()
    {
        var taken = new DateTime(2021, 4, 8, 20, 30, 40);
        input.Write("c.jpg", TestMedia.Jpeg(model: "SM-A415F", dateTimeOriginal: taken, subSecondOriginal: "1"));
        input.Write("a.jpg", TestMedia.Jpeg(model: "SM-A415F", dateTimeOriginal: taken, subSecondOriginal: "75"));
        input.Write("b.jpg", TestMedia.Jpeg(model: "SM-A415F", dateTimeOriginal: taken, subSecondOriginal: "5"));

        Process(input.Path, output.Path);

        Assert.Equal(["IMG 2021-04-08 20.30.40 A41 1.jpg", "IMG 2021-04-08 20.30.40 A41 2.jpg", "IMG 2021-04-08 20.30.40 A41.jpg"], output.FileNames());
        // ".1" < ".5" < ".75": c first, then b, then a.
        Assert.Equal(TestMedia.Jpeg(model: "SM-A415F", dateTimeOriginal: taken, subSecondOriginal: "1"), File.ReadAllBytes(Path.Combine(output.Path, "IMG 2021-04-08 20.30.40 A41.jpg")));
        Assert.Equal(TestMedia.Jpeg(model: "SM-A415F", dateTimeOriginal: taken, subSecondOriginal: "5"), File.ReadAllBytes(Path.Combine(output.Path, "IMG 2021-04-08 20.30.40 A41 1.jpg")));
        Assert.Equal(TestMedia.Jpeg(model: "SM-A415F", dateTimeOriginal: taken, subSecondOriginal: "75"), File.ReadAllBytes(Path.Combine(output.Path, "IMG 2021-04-08 20.30.40 A41 2.jpg")));
    }

    [Fact]
    public void RunningTwiceInPlace_ChangesNothingTheSecondTime()
    {
        var taken = new DateTime(2021, 4, 8, 20, 30, 40);
        input.Write("IMG_1.jpg", TestMedia.Jpeg(dateTimeOriginal: taken, subSecondOriginal: "1"));
        input.Write("IMG_2.jpg", TestMedia.Jpeg(dateTimeOriginal: taken, subSecondOriginal: "2"));
        input.Write("IMG_3.jpg", TestMedia.Jpeg(dateTimeOriginal: taken.AddSeconds(1)));

        var first = Process(input.Path, input.Path);
        var namesAfterFirstRun = input.FileNames();
        var second = Process(input.Path, input.Path);

        Assert.Equal(new ProcessingSummary(3, 0, 0, 0, 0), first);
        Assert.Equal(new ProcessingSummary(0, 3, 0, 0, 0), second);
        Assert.Equal(namesAfterFirstRun, input.FileNames());
        Assert.Equal(["IMG 2021-04-08 20.30.40 1.jpg", "IMG 2021-04-08 20.30.40.jpg", "IMG 2021-04-08 20.30.41.jpg"], namesAfterFirstRun);
    }

    [Fact]
    public void IdenticalFileAlreadyInOutput_IsReportedAsDuplicateAndLeftInPlace()
    {
        var photo = TestMedia.Jpeg(dateTimeOriginal: new DateTime(2021, 4, 8, 19, 41, 8));
        output.Write("IMG 2021-04-08 19.41.08.jpg", photo);
        input.Write("copy.jpg", photo);

        var summary = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(0, 0, 1, 0, 0), summary);
        Assert.Equal(["copy.jpg"], input.FileNames());
        Assert.Equal(["IMG 2021-04-08 19.41.08.jpg"], output.FileNames());
    }

    [Fact]
    public void CopyMode_RunTwice_DoesNotCreateDuplicates()
    {
        config.Options.CopyingEnabled = true;
        input.Write("photo.jpg", TestMedia.Jpeg(dateTimeOriginal: new DateTime(2021, 4, 8, 19, 41, 8)));

        var first = Process(input.Path, output.Path);
        var second = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(1, 0, 0, 0, 0), first);
        Assert.Equal(new ProcessingSummary(0, 0, 1, 0, 0), second);
        Assert.Equal(["photo.jpg"], input.FileNames());
        Assert.Equal(["IMG 2021-04-08 19.41.08.jpg"], output.FileNames());
    }

    [Fact]
    public void DifferentFileWithSameName_GetsNextNumber()
    {
        output.Write("IMG 2021-04-08 19.41.08.jpg", TestMedia.Jpeg(dateTimeOriginal: new DateTime(2021, 4, 8, 19, 41, 8), subSecondOriginal: "1"));
        input.Write("new.jpg", TestMedia.Jpeg(dateTimeOriginal: new DateTime(2021, 4, 8, 19, 41, 8), subSecondOriginal: "2"));

        Process(input.Path, output.Path);

        Assert.Equal(["IMG 2021-04-08 19.41.08 1.jpg", "IMG 2021-04-08 19.41.08.jpg"], output.FileNames());
    }

    [Fact]
    public void UnreadableMetadata_FallsBackToFileName()
    {
        input.Write("IMG_20210408_194108.jpg", "not really a jpeg"u8.ToArray());

        var summary = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(1, 0, 0, 0, 0), summary);
        Assert.Equal(["IMG 2021-04-08 19.41.08.jpg"], output.FileNames());
    }

    [Fact]
    public void NoDateAnywhere_UsesFileModifiedTime()
    {
        input.Write("scan.png", "not really a png"u8.ToArray(), lastWriteTime: FileTime);

        Process(input.Path, output.Path);

        Assert.Equal(["IMG 2030-01-01 12.00.00.png"], output.FileNames());
    }

    [Fact]
    public void UnsupportedFiles_AreCountedAndLeftAlone()
    {
        input.Write("20221113_135022.dng", [1, 2, 3]);
        input.Write("notes.txt", [1, 2, 3]);

        var summary = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(0, 0, 0, 2, 0), summary);
        Assert.Equal(["20221113_135022.dng", "notes.txt"], input.FileNames());
    }

    [Fact]
    public void CreatesMissingOutputDirectory()
    {
        var target = Path.Combine(output.Path, "nested", "out");
        input.Write("IMG_20210408_194108.jpg", TestMedia.Jpeg());

        Process(input.Path, target);

        Assert.Single(Directory.GetFiles(target));
    }

    private ProcessingSummary Process(string inputDirectory, string outputDirectory)
        => new DirectoryProcessor(config, new NullLog()).Process(inputDirectory, outputDirectory);

    private sealed class NullLog : ILog
    {
        public void LogError(Exception ex) { }

        public void LogMessage(string message) { }
    }
}
