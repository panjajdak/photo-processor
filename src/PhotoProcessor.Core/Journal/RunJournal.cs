namespace PhotoProcessor.Core.Journal;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Append-only record of the moves and copies of one run, one JSON object per line, so the run can be undone.
/// Stored in "&lt;output&gt;/.photoprocessor/runs/". The file is created on the first operation and every line is
/// flushed immediately, so even an interrupted run can be undone.
/// </summary>
internal sealed class RunJournal : IDisposable
{
    public const string FolderName = ".photoprocessor";
    private const string RunsFolderName = "runs";
    public const string TimestampFormat = "yyyyMMdd-HHmmss-fff";
    private const string UndoneSuffix = ".undone.jsonl";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string outputDirectory;
    private StreamWriter? writer;

    public RunJournal(string outputDirectory)
    {
        this.outputDirectory = outputDirectory;
    }

    /// <summary>
    /// Path of the journal file, or null when nothing has been recorded.
    /// </summary>
    public string? Path { get; private set; }

    public void Record(JournalOperation op, string from, string to)
    {
        if (writer == null)
        {
            var runs = GetRunsDirectory(outputDirectory);
            var folder = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(runs)!);
            folder.Attributes |= FileAttributes.Hidden;
            Directory.CreateDirectory(runs);

            Path = System.IO.Path.Combine(runs, DateTime.Now.ToString(TimestampFormat) + ".jsonl");
            writer = new StreamWriter(new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        }

        var info = new FileInfo(to);
        var entry = new JournalEntry(op, from, to, info.Length, info.LastWriteTimeUtc);
        writer.WriteLine(JsonSerializer.Serialize(entry, JsonOptions));
        writer.Flush();
    }

    public void Dispose() => writer?.Dispose();

    public static IReadOnlyList<JournalEntry> Read(string path)
        => File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<JournalEntry>(line, JsonOptions)!)
            .ToList();

    /// <summary>
    /// Latest journal in <paramref name="directory"/> that has not been undone yet.
    /// </summary>
    public static string? FindLatest(string directory)
        => ListAll(directory).LastOrDefault(p => !IsUndone(p));

    /// <summary>
    /// All journals in <paramref name="directory"/>, oldest first.
    /// </summary>
    public static IReadOnlyList<string> ListAll(string directory)
    {
        var runs = GetRunsDirectory(directory);
        return Directory.Exists(runs)
            ? Directory.GetFiles(runs, "*.jsonl").OrderBy(System.IO.Path.GetFileName, StringComparer.Ordinal).ToList()
            : [];
    }

    public static bool IsUndone(string path) => path.EndsWith(UndoneSuffix, StringComparison.OrdinalIgnoreCase);

    public static void MarkUndone(string path)
        => File.Move(path, System.IO.Path.ChangeExtension(path, null) + UndoneSuffix);

    private static string GetRunsDirectory(string directory)
        => System.IO.Path.Combine(directory, FolderName, RunsFolderName);
}
