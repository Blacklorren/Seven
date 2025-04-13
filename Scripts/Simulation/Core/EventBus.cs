using System;
using System.Collections.Generic;

namespace HandballManager.Simulation.Core
{
    /// <summary>
    /// Interface for the event bus system that allows components to communicate without direct dependencies.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Subscribe to events of type TEvent.
        /// </summary>
        /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
        /// <param name="handler">The action to execute when the event is published.</param>
        void Subscribe<TEvent>(Action<TEvent> handler);

        /// <summary>
        /// Publish an event to all subscribers.
        /// </summary>
        /// <typeparam name="TEvent">The type of event being published.</typeparam>
        /// <param name="event">The event instance to publish.</param>
        void Publish<TEvent>(TEvent @event);
    }

    /// <summary>
    /// Implementation of the event bus that manages subscriptions and event publishing.
    /// </summary>
    public class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Action<object>>> _subscriptions = new Dictionary<Type, List<Action<object>>>();
        
        /// <summary>
        /// Subscribe to events of type TEvent.
        /// </summary>
        /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
        /// <param name="handler">The action to execute when the event is published.</param>
        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);
            if (!_subscriptions.ContainsKey(type))
            {
                _subscriptions[type] = new List<Action<object>>();
            }
            _subscriptions[type].Add(e => handler((TEvent)e));
        }
        
        /// <summary>
        /// Publish an event to all subscribers.
        /// </summary>
        /// <typeparam name="TEvent">The type of event being published.</typeparam>
        /// <param name="event">The event instance to publish.</param>
        public void Publish<TEvent>(TEvent @event)
        {
            if (_subscriptions.TryGetValue(typeof(TEvent), out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler(@event);
                }
            }
        }
    }
}