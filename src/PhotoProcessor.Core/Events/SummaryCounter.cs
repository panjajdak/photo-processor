namespace PhotoProcessor.Core.Events;

internal sealed class SummaryCounter : IProcessingObserver
{
    private int renamed;
    private int alreadyNamed;
    private int duplicates;
    private int unsupported;
    private int failed;

    public ProcessingSummary Summary => new(renamed, alreadyNamed, duplicates, unsupported, failed);

    public void On(ProcessingEvent processingEvent)
    {
        switch (processingEvent)
        {
            case FileRenamed or FileCopied:
                renamed++;
                break;
            case AlreadyNamed:
                alreadyNamed++;
                break;
            case DuplicateSkipped:
                duplicates++;
                break;
            case UnsupportedSkipped or OrphanSidecarSkipped:
                unsupported++;
                break;
            case FileFailed failure:
                failed += failure.Paths.Count;
                break;
        }
    }
}
