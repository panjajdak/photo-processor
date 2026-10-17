namespace PhotoProcessor.Core.Commands;

internal interface ICommandLog
{
    void Record(ExecutedCommand executed);
}
