namespace PhotoProcessor.Core.Journal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using PhotoProcessor.Core.Commands;
using PhotoProcessor.Core.IO;

internal sealed class RunJournal(IFileSystem fileSystem, string outputDirectory)
{
    public const string FolderName = ".photoprocessor";
    public const string TimestampFormat = "yyyyMMdd-HHmmss-fff";
    private const string RunsFolderName = "runs";
    private const string UndoneSuffix = ".undone.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string? Path { get; private set; }

    public void Record(ExecutedCommand executed)
    {
        Path ??= Start();
        var command = executed.Command;
        var entry = new JournalEntry(command.Operation, command.From, command.To, executed.Size, executed.ModifiedUtc);
        fileSystem.AppendLine(Path, JsonSerializer.Serialize(entry, JsonOptions));
    }

    public static IReadOnlyList<ExecutedCommand> Read(IFileSystem fileSystem, string path)
        => fileSystem.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<JournalEntry>(line, JsonOptions)!)
            .Select(ToExecutedCommand)
            .ToList();

    public static string? FindLatest(IFileSystem fileSystem, string directory)
        => ListAll(fileSystem, directory).LastOrDefault(p => !IsUndone(p));

    public static IReadOnlyList<string> ListAll(IFileSystem fileSystem, string directory)
    {
        var runs = GetRunsDirectory(directory);
        return fileSystem.DirectoryExists(runs)
            ? fileSystem.GetFiles(runs)
                .Where(p => p.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
                .OrderBy(System.IO.Path.GetFileName, StringComparer.Ordinal)
                .ToList()
            : [];
    }

    public static bool IsUndone(string path) => path.EndsWith(UndoneSuffix, StringComparison.OrdinalIgnoreCase);

    public static void MarkUndone(IFileSystem fileSystem, string path)
        => fileSystem.Move(path, System.IO.Path.ChangeExtension(path, null) + UndoneSuffix);

    private string Start()
    {
        var runs = GetRunsDirectory(outputDirectory);
        fileSystem.CreateDirectory(System.IO.Path.GetDirectoryName(runs)!, hidden: true);
        fileSystem.CreateDirectory(runs);
        return System.IO.Path.Combine(runs, DateTime.Now.ToString(TimestampFormat) + ".jsonl");
    }

    private static ExecutedCommand ToExecutedCommand(JournalEntry entry)
    {
        IFileCommand command = entry.Op == JournalOperation.Move
            ? new MoveFileCommand(entry.From, entry.To)
            : new CopyFileCommand(entry.From, entry.To);
        return new ExecutedCommand(command, entry.Size, entry.ModifiedUtc);
    }

    private static string GetRunsDirectory(string directory)
        => System.IO.Path.Combine(directory, FolderName, RunsFolderName);
}
