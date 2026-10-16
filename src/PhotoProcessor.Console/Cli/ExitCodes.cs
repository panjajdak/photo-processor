namespace PhotoProcessor.Console.Cli;

internal static class ExitCodes
{
    public const int Success = 0;

    public const int PartialFailure = 1;

    public const int InvalidUsage = 2;

    public const int Error = 3;

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
