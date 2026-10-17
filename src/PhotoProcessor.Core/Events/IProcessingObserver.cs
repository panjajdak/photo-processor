namespace PhotoProcessor.Core.Events;

/// <summary>Receives everything that happens during a rename or undo.</summary>
public interface IProcessingObserver
{
    /// <summary>Called for every event, in the order they happen.</summary>
    void On(ProcessingEvent processingEvent);
}
