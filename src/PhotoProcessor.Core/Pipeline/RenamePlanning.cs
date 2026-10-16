namespace PhotoProcessor.Core.Pipeline;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PhotoProcessor.Core.Commands;
using PhotoProcessor.Core.Events;
using PhotoProcessor.Core.IO;
using PhotoProcessor.Core.Naming;

internal sealed class RenamePlanning(FileNameTemplate template, IFileSystem fileSystem, IProcessingObserver observer, bool copy)
{
    public IReadOnlyList<CommandBatch> Plan(IReadOnlyList<AnalyzedGroup> groups, string outputDirectory, CancellationToken cancellationToken)
    {
        var pending = new List<AnalyzedGroup>();
        foreach (var group in groups)
        {
            if (!IsAlreadyNamed(group, outputDirectory))
            {
                pending.Add(group);
                continue;
            }

            foreach (var member in group.Members)
            {
                observer.On(new AlreadyNamed(member.SourcePath));
            }
        }

        var outputFolder = new OutputFolderState(fileSystem, outputDirectory, pending.SelectMany(g => g.Members).Select(m => m.SourcePath));
        var ordered = pending
            .OrderBy(g => g.Context.Date)
            .ThenBy(g => g.SubSecondSortKey, StringComparer.Ordinal)
            .ThenBy(g => g.Stem, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var batches = new List<CommandBatch>();
        var total = ordered.Sum(g => g.Members.Count);
        var done = 0;
        foreach (var group in ordered)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return [];
            }

            try
            {
                if (PlanGroup(group, outputDirectory, outputFolder) is { Commands.Count: > 0 } batch)
                {
                    batches.Add(batch);
                }
            }
            catch (Exception ex)
            {
                observer.On(new FileFailed(group.Members.Select(m => m.SourcePath).ToList(), ex));
            }

            done += group.Members.Count;
            observer.On(new ProgressChanged("Planning", done, total));
        }

        return batches;
    }

    private bool IsAlreadyNamed(AnalyzedGroup group, string outputDirectory)
        => FileComparer.PathsEqual(group.Directory, outputDirectory) && template.Matches(group.Stem, group.Context);

    private CommandBatch? PlanGroup(AnalyzedGroup group, string outputDirectory, OutputFolderState outputFolder)
    {
        var toPlace = new List<GroupMember>();
        foreach (var member in group.Members)
        {
            if (outputFolder.FindIdentical(member.SourcePath) is { } existing)
            {
                observer.On(new DuplicateSkipped(member.SourcePath, existing));
            }
            else
            {
                toPlace.Add(member);
            }
        }

        if (toPlace.Count == 0)
        {
            return null;
        }

        var targets = FirstFreeTargets(group, toPlace, outputDirectory, outputFolder);
        var commands = new List<IFileCommand>();
        foreach (var (member, target) in targets)
        {
            if (FileComparer.PathsEqual(member.SourcePath, target))
            {
                observer.On(new AlreadyNamed(member.SourcePath));
                continue;
            }

            outputFolder.Reserve(member.SourcePath, target, sourceIsMoved: !copy);
            commands.Add(copy ? new CopyFileCommand(member.SourcePath, target) : new MoveFileCommand(member.SourcePath, target));
        }

        return new CommandBatch(commands, group.DateSource);
    }

    private List<(GroupMember Member, string Target)> FirstFreeTargets(
        AnalyzedGroup group,
        IReadOnlyList<GroupMember> members,
        string outputDirectory,
        OutputFolderState outputFolder)
    {
        for (var counter = 0; ; counter++)
        {
            var stem = template.Render(group.Context, counter);
            var targets = members
                .Select(m => (Member: m, Target: Path.Combine(outputDirectory, stem + m.Extension)))
                .ToList();

            if (!targets.Any(t => !FileComparer.PathsEqual(t.Member.SourcePath, t.Target) && outputFolder.IsTaken(t.Target)))
            {
                return targets;
            }
        }
    }
}
