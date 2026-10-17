namespace PhotoProcessor.Core.Tests.Support;

using PhotoProcessor.Core.Events;

internal sealed class TestObserver(Action<ProcessingEvent>? onEvent = null) : IProcessingObserver
{
    public List<ProcessingEvent> Events { get; } = [];

    public IEnumerable<T> Of<T>() where T : ProcessingEvent => Events.OfType<T>();

    public void On(ProcessingEvent processingEvent)
    {
        Events.Add(processingEvent);
        onEvent?.Invoke(processingEvent);
    }
}
