using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aikido.Zen.Core.EventHandling
{
    /// <summary>
    /// A simple mediator implementation that handles prioritized event listeners
    /// Implemented as a thread-safe singleton
    /// </summary>
    public class Mediator
    {
        private static readonly Lazy<Mediator> _instance = new Lazy<Mediator>(() => new Mediator());
        private readonly Dictionary<Type, List<(int priority, Func<IAppEvent, Task> handler)>> _handlers;
        private readonly object _lock = new object();

        /// <summary>
        /// Gets the singleton instance of the Mediator
        /// </summary>
        public static Mediator Instance => _instance.Value;

        private Mediator()
        {
            _handlers = new Dictionary<Type, List<(int priority, Func<IAppEvent, Task> handler)>>();
        }

        /// <summary>
        /// Registers an event handler with a specified priority
        /// </summary>
        /// <typeparam name="TEvent">Type of event to handle</typeparam>
        /// <param name="handler">The event handler</param>
        /// <param name="priority">Priority of the handler (higher numbers execute first)</param>
        public void Register<TEvent>(Func<TEvent, Task> handler, int priority = 0) where TEvent : IAppEvent
        {
            var eventType = typeof(TEvent);

            lock (_lock)
            {
                if (!_handlers.ContainsKey(eventType))
                {
                    _handlers[eventType] = new List<(int priority, Func<IAppEvent, Task> handler)>();
                }

                _handlers[eventType].Add((priority, async (evt) => await handler((TEvent)evt)));
                _handlers[eventType] = _handlers[eventType]
                    .OrderByDescending(x => x.priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Publishes an event to all registered handlers
        /// </summary>
        /// <param name="event">The event to publish</param>
        public async Task PublishAsync(IAppEvent @event)
        {
            var eventType = @event.GetType();
            List<(int priority, Func<IAppEvent, Task> handler)> handlersToExecute;

            lock (_lock)
            {
                if (!_handlers.ContainsKey(eventType))
                {
                    return;
                }
                handlersToExecute = _handlers[eventType].ToList();
            }

            foreach (var (_, handler) in handlersToExecute)
            {
                await handler(@event);
            }
        }

        /// <summary>
        /// Publishes an event to all registered handlers
        /// </summary>
        /// <param name="event">The event to publish</param>
        public void Publish(IAppEvent @event)
        {
            Task.Run(async () => await PublishAsync(@event));
        }

        /// <summary>
        /// Removes all handlers for a specific event type
        /// </summary>
        /// <typeparam name="TEvent">Type of event to clear handlers for</typeparam>
        public void ClearHandlers<TEvent>() where TEvent : IAppEvent
        {
            var eventType = typeof(TEvent);
            lock (_lock)
            {
                if (_handlers.ContainsKey(eventType))
                {
                    _handlers.Remove(eventType);
                }
            }
        }
    }
}

