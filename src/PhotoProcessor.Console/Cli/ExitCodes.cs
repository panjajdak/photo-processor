namespace PhotoProcessor.Console.Cli;

internal static class ExitCodes
{
    public const int Success = 0;

    /// <summary>The run finished, but some files failed or were skipped.</summary>
    public const int PartialFailure = 1;

    /// <summary>Invalid arguments or configuration; nothing was changed.</summary>
    public const int InvalidUsage = 2;

    /// <summary>An unexpected error stopped the run.</summary>
    public const int Error = 3;

    /// <summary>Stopped with Ctrl+C (128 + SIGINT, as shells report it).</summary>
    public const int Cancelled = 130;

    public static readonly (int Code, string Meaning)[] All =
    [
        (Success, "Success."),
        (PartialFailure, "Finished, but some files failed (rename) or were skipped (undo)."),
        (InvalidUsage, "Invalid arguments or configuration; nothing was changed."),
        (Error, "An unexpected error stopped the run; finished files are in the journal."),
        (Cancelled, "Stopped with Ctrl+C; finished files are in the journal."),
    ];
}
