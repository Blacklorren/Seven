using System;
using HandballManager.Core;
using HandballManager.Data;

namespace HandballManager.Simulation.Core.Events
{
    /// <summary>
    /// Base interface for all events in the system.
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// Gets the timestamp when the event was created.
        /// </summary>
        DateTime Timestamp { get; }
    }

    /// <summary>
    /// Base class for all events providing common functionality.
    /// </summary>
    public abstract class EventBase : IEvent
    {
        /// <summary>
        /// Gets the timestamp when the event was created.
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.Now;
    }

    /// <summary>
    /// Event fired when the game state changes.
    /// </summary>
    public class GameStateChangedEvent : EventBase
    {
        /// <summary>
        /// Gets the previous game state.
        /// </summary>
        public GameState OldState { get; set; }

        /// <summary>
        /// Gets the new game state.
        /// </summary>
        public GameState NewState { get; set; }
    }

    /// <summary>
    /// Event fired when a match is completed.
    /// </summary>
    public class MatchCompletedEvent : EventBase
    {
        /// <summary>
        /// Gets the match result.
        /// </summary>
        public MatchResult Result { get; set; }
    }

    /// <summary>
    /// Event fired when a day advances in the game.
    /// </summary>
    public class DayAdvancedEvent : EventBase
    {
        /// <summary>
        /// Gets the new date after advancement.
        /// </summary>
        public DateTime NewDate { get; set; }
    }

    /// <summary>
    /// Event fired when a week advances in the game.
    /// </summary>
    public class WeekAdvancedEvent : EventBase
    {
        /// <summary>
        /// Gets the new date after advancement.
        /// </summary>
        public DateTime NewDate { get; set; }
    }

    /// <summary>
    /// Event fired when a month advances in the game.
    /// </summary>
    public class MonthAdvancedEvent : EventBase
    {
        /// <summary>
        /// Gets the new date after advancement.
        /// </summary>
        public DateTime NewDate { get; set; }
    }
}