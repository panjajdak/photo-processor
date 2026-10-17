namespace PhotoProcessor.Core.Events;

using System;
using System.Collections.Generic;
using PhotoProcessor.Core.Dating;

/// <summary>Something that happened during a rename or undo.</summary>
public abstract record ProcessingEvent;

/// <summary>A file was moved to its new name, or would be in a dry run.</summary>
public sealed record FileRenamed(string From, string To, DateSource DateSource, bool DryRun) : ProcessingEvent;

/// <summary>A file was copied under its new name, or would be in a dry run.</summary>
public sealed record FileCopied(string From, string To, DateSource DateSource, bool DryRun) : ProcessingEvent;

/// <summary>A file was left in place because an identical copy already exists.</summary>
public sealed record DuplicateSkipped(string Path, string ExistingCopy) : ProcessingEvent;

/// <summary>A file already has the name it would be given.</summary>
public sealed record AlreadyNamed(string Path) : ProcessingEvent;

/// <summary>A file was left alone because its type is not supported.</summary>
public sealed record UnsupportedSkipped(string Path) : ProcessingEvent;

/// <summary>A sidecar was left alone because no photo or video shares its name.</summary>
public sealed record OrphanSidecarSkipped(string Path) : ProcessingEvent;

/// <summary>A file's metadata could not be read; it is dated from its name or file time instead.</summary>
public sealed record MetadataUnreadable(string Path, string Reason) : ProcessingEvent;

/// <summary>Files could not be processed.</summary>
public sealed record FileFailed(IReadOnlyList<string> Paths, Exception Error) : ProcessingEvent;

/// <summary>Progress of the current stage.</summary>
public sealed record ProgressChanged(string Stage, int Done, int Total) : ProcessingEvent;

/// <summary>A renamed file was moved back, or a copy removed, or would be in a dry run.</summary>
public sealed record FileRestored(string From, string To, bool WasCopy, bool DryRun) : ProcessingEvent;

/// <summary>A journal entry could not be undone safely and was left alone.</summary>
public sealed record UndoSkipped(string Path, string Reason) : ProcessingEvent;
