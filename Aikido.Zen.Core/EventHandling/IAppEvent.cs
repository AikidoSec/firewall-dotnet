using System;

namespace Aikido.Zen.Core.EventHandling
{
    /// <summary>
    /// Base interface for application events in the mediator pattern.
    /// All events that need to be handled by the mediator should implement this interface.
    /// </summary>
    public interface IAppEvent
    {

        /// <summary>
        /// Gets the timestamp when this event was created
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// Gets the type name of this event
        /// </summary>
        string EventType { get; }
    }

    /// <summary>
    /// Base interface for application events with data in the mediator pattern.
    /// All events that need to be handled by the mediator should implement this interface.
    /// </summary>
    public interface IAppEvent<T> : IAppEvent
    {
        T Data { get; }
    }
}
