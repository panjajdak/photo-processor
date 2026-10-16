namespace PhotoProcessor.Core.Events;

using System.Collections.Generic;

internal sealed class CompositeObserver(IReadOnlyList<IProcessingObserver> observers) : IProcessingObserver
{
    public CompositeObserver(params IProcessingObserver[] observers)
        : this((IReadOnlyList<IProcessingObserver>)observers)
    { }

    public void On(ProcessingEvent processingEvent)
    {
        foreach (var observer in observers)
        {
            observer.On(processingEvent);
        }
    }
}
