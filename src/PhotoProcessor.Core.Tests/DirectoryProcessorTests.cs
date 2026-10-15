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
        input.Write("GL013592.LRV", [1, 2, 3]);
        input.Write("notes.txt", [1, 2, 3]);

        var summary = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(0, 0, 0, 2, 0), summary);
        Assert.Equal(["GL013592.LRV", "notes.txt"], input.FileNames());
    }

    [Fact]
    public void RenamesRawFiles()
    {
        input.Write("20221113_135022.dng", TestMedia.Tiff(model: "SM-G990B", dateTimeOriginal: new DateTime(2022, 11, 13, 13, 50, 22)));

        Process(input.Path, output.Path);

        Assert.Equal(["IMG 2022-11-13 13.50.22 SM-G990B.dng"], output.FileNames());
    }

    [Fact]
    public void RawAndJpegPair_GetTheSameName()
    {
        var taken = new DateTime(2022, 11, 13, 13, 50, 22);
        input.Write("DSC_0042.NEF", TestMedia.Tiff(model: "SM-A415F", dateTimeOriginal: taken));
        input.Write("DSC_0042.JPG", TestMedia.Jpeg(model: "SM-A415F", dateTimeOriginal: taken));

        var summary = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(2, 0, 0, 0, 0), summary);
        Assert.Equal(["IMG 2022-11-13 13.50.22 A41.jpg", "IMG 2022-11-13 13.50.22 A41.nef"], output.FileNames());
    }

    [Fact]
    public void UnreadableRaw_TakesDateAndDeviceFromItsJpeg()
    {
        var taken = new DateTime(2022, 11, 13, 13, 50, 22);
        input.Write("IMG_0042.CR3", [1, 2, 3], lastWriteTime: FileTime);
        input.Write("IMG_0042.JPG", TestMedia.Jpeg(model: "SM-A415F", dateTimeOriginal: taken));

        Process(input.Path, output.Path);

        Assert.Equal(["IMG 2022-11-13 13.50.22 A41.cr3", "IMG 2022-11-13 13.50.22 A41.jpg"], output.FileNames());
    }

    [Fact]
    public void LivePhotoVideo_IsNamedLikeItsPhoto()
    {
        input.Write("IMG_1234.HEIC", TestMedia.Jpeg(model: "iPhone 15", dateTimeOriginal: new DateTime(2024, 6, 1, 10, 0, 0)));
        input.Write("IMG_1234.MOV", TestMedia.Mov(new DateTime(2024, 6, 1, 10, 0, 2)));

        Process(input.Path, output.Path);

        Assert.Equal(["IMG 2024-06-01 10.00.00 iPhone 15.heic", "IMG 2024-06-01 10.00.00 iPhone 15.mov"], output.FileNames());
    }

    [Fact]
    public void Sidecars_FollowTheirMediaFile()
    {
        input.Write("IMG_1234.HEIC", TestMedia.Jpeg(dateTimeOriginal: new DateTime(2024, 6, 1, 10, 0, 0)));
        input.Write("IMG_1234.AAE", [1]);
        input.Write("IMG_1234.xmp", [2]);
        input.Write("IMG_1234.HEIC.xmp", [3]);

        var summary = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(4, 0, 0, 0, 0), summary);
        Assert.Equal(
            ["IMG 2024-06-01 10.00.00.aae", "IMG 2024-06-01 10.00.00.heic", "IMG 2024-06-01 10.00.00.heic.xmp", "IMG 2024-06-01 10.00.00.xmp"],
            output.FileNames());
    }

    [Fact]
    public void JpegSidecarExtension_IsNormalizedWithItsPhoto()
    {
        input.Write("photo.JPEG", TestMedia.Jpeg(dateTimeOriginal: new DateTime(2024, 6, 1, 10, 0, 0)));
        input.Write("photo.JPEG.xmp", [1]);

        Process(input.Path, output.Path);

        Assert.Equal(["IMG 2024-06-01 10.00.00.jpg", "IMG 2024-06-01 10.00.00.jpg.xmp"], output.FileNames());
    }

    [Fact]
    public void SidecarWithoutMedia_IsLeftAlone()
    {
        input.Write("IMG_9999.AAE", [1]);

        var summary = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(0, 0, 0, 1, 0), summary);
        Assert.Equal(["IMG_9999.AAE"], input.FileNames());
    }

    [Fact]
    public void GroupCollision_MovesWholeGroupToNextNumber()
    {
        var taken = new DateTime(2022, 11, 13, 13, 50, 22);
        output.Write("IMG 2022-11-13 13.50.22.jpg", TestMedia.Jpeg(dateTimeOriginal: taken, subSecondOriginal: "1"));
        input.Write("DSC_0042.JPG", TestMedia.Jpeg(dateTimeOriginal: taken, subSecondOriginal: "2"));
        input.Write("DSC_0042.dng", TestMedia.Tiff(dateTimeOriginal: taken));

        Process(input.Path, output.Path);

        // .dng alone would fit at index 0, but it must stay paired with its JPEG.
        Assert.Equal(["IMG 2022-11-13 13.50.22 1.dng", "IMG 2022-11-13 13.50.22 1.jpg", "IMG 2022-11-13 13.50.22.jpg"], output.FileNames());
    }

    [Fact]
    public void JpegAlreadyImported_RawIsAddedNextToIt()
    {
        var taken = new DateTime(2022, 11, 13, 13, 50, 22);
        var jpeg = TestMedia.Jpeg(dateTimeOriginal: taken);
        output.Write("IMG 2022-11-13 13.50.22.jpg", jpeg);
        input.Write("DSC_0042.JPG", jpeg);
        input.Write("DSC_0042.dng", TestMedia.Tiff(dateTimeOriginal: taken));

        var summary = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(1, 0, 1, 0, 0), summary);
        Assert.Equal(["IMG 2022-11-13 13.50.22.dng", "IMG 2022-11-13 13.50.22.jpg"], output.FileNames());
        Assert.Equal(["DSC_0042.JPG"], input.FileNames());
    }

    [Fact]
    public void GroupsRunTwiceInPlace_ChangeNothingTheSecondTime()
    {
        var taken = new DateTime(2022, 11, 13, 13, 50, 22);
        input.Write("A.JPG", TestMedia.Jpeg(dateTimeOriginal: taken, subSecondOriginal: "1"));
        input.Write("A.dng", TestMedia.Tiff(dateTimeOriginal: taken));
        input.Write("A.xmp", [1]);
        input.Write("B.JPG", TestMedia.Jpeg(dateTimeOriginal: taken, subSecondOriginal: "2"));
        input.Write("B.JPG.xmp", [2]);

        var first = Process(input.Path, input.Path);
        var names = input.FileNames();
        var second = Process(input.Path, input.Path);

        Assert.Equal(new ProcessingSummary(5, 0, 0, 0, 0), first);
        Assert.Equal(new ProcessingSummary(0, 5, 0, 0, 0), second);
        Assert.Equal(names, input.FileNames());
        Assert.Equal(
            ["IMG 2022-11-13 13.50.22 1.jpg", "IMG 2022-11-13 13.50.22 1.jpg.xmp", "IMG 2022-11-13 13.50.22.dng", "IMG 2022-11-13 13.50.22.jpg", "IMG 2022-11-13 13.50.22.xmp"],
            names);
    }

    [Fact]
    public void FilesWhoseExtensionsNormalizeToTheSameName_AreNotRenamed()
    {
        input.Write("photo.JPG", TestMedia.Jpeg(dateTimeOriginal: new DateTime(2024, 6, 1, 10, 0, 0)));
        input.Write("photo.jpeg", TestMedia.Jpeg(dateTimeOriginal: new DateTime(2024, 6, 1, 10, 0, 1)));

        var summary = Process(input.Path, output.Path);

        Assert.Equal(new ProcessingSummary(0, 0, 0, 0, 2), summary);
        Assert.Equal(["photo.JPG", "photo.jpeg"], input.FileNames());
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
