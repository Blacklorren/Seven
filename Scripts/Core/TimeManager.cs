using System;
using UnityEngine; // Required for Debug.Log

namespace HandballManager.Core
{
    /// <summary>
    /// Manages the progression of game time (date).
    /// Provides events for other systems to subscribe to time changes.
    /// </summary>
    public class TimeManager
    {
        // --- Events ---
        /// <summary>
        /// Fired *after* a single day has advanced. Carries the new date.
        /// </summary>
        public event Action OnDayAdvanced; // Simpler: no date passed, systems can get it from CurrentDate property

        /// <summary>
        /// Fired *after* a full week (7 days) has advanced.
        /// </summary>
        public event Action OnWeekAdvanced;

        /// <summary>
        /// Fired *after* the month changes.
        /// </summary>
        public event Action OnMonthAdvanced;

        // --- Properties ---
        /// <summary>
        /// Gets the current date in the game simulation.
        /// </summary>
        public DateTime CurrentDate { get; private set; }

        /// <summary>
        /// Allows pausing time advancement (e.g., during popups, critical decisions).
        /// </summary>
        public bool IsPaused { get; set; } = false;

        // --- Private Fields ---
        private DayOfWeek _startOfWeek = DayOfWeek.Monday; // Define when the week 'starts' for events
        private bool _weekEventTriggeredThisCycle = false;

        // --- Constructor ---
        /// <summary>
        /// Initializes the TimeManager, setting the starting date.
        /// </summary>
        /// <param name="startDate">The initial date for the game simulation.</param>
        public TimeManager(DateTime startDate)
        {
            CurrentDate = startDate.Date; // Ensure we start at the beginning of the day
            Debug.Log($"TimeManager initialized. Current Date: {CurrentDate.ToShortDateString()} ({CurrentDate.DayOfWeek})");
        }

        // --- Public Methods ---

        /// <summary>
        /// Advances the game time by a single day, if not paused. Triggers related events.
        /// </summary>
        public void AdvanceDay()
        {
            if (IsPaused)
            {
                // Debug.Log("TimeManager is paused. Cannot advance day.");
                return;
            }

            DateTime previousDate = CurrentDate;
            CurrentDate = CurrentDate.AddDays(1);

            // Fire Day Advanced Event
            OnDayAdvanced?.Invoke();

            // Check for Week Advanced Event (trigger on the first day *of* the new week)
            if (CurrentDate.DayOfWeek == _startOfWeek && !_weekEventTriggeredThisCycle)
            {
                OnWeekAdvanced?.Invoke();
                _weekEventTriggeredThisCycle = true; // Ensure it only fires once per week start
            }
            else if (CurrentDate.DayOfWeek != _startOfWeek)
            {
                _weekEventTriggeredThisCycle = false; // Reset trigger flag once we're past the start day
            }


            // Check for Month Advanced Event
            if (CurrentDate.Month != previousDate.Month)
            {
                OnMonthAdvanced?.Invoke();
            }
             // Optional: Log date change less frequently
            // if (CurrentDate.Day % 5 == 0) Debug.Log($"Date Advanced: {CurrentDate.ToShortDateString()}");
        }

        /// <summary>
        /// Advances the game time by a specified number of days.
        /// Respects the IsPaused flag between days.
        /// </summary>
        /// <param name="days">Number of days to advance.</param>
        public void AdvanceMultipleDays(int days)
        {
            if (days <= 0) return;
            // Debug.Log($"TimeManager attempting to advance by {days} days.");
            for (int i = 0; i < days; i++)
            {
                if (IsPaused)
                {
                    Debug.LogWarning($"Time advancement stopped after {i} days due to pause.");
                    break;
                }
                AdvanceDay();
            }
        }

        /// <summary>
        /// Advances the game time by exactly one week (7 days).
        /// </summary>
        public void AdvanceWeek()
        {
            AdvanceMultipleDays(7);
        }

        /// <summary>
        /// Sets the current date explicitly. Use with caution (e.g., loading).
        /// Resets the weekly event trigger flag.
        /// </summary>
        /// <param name="newDate">The date to set.</param>
        public void SetDate(DateTime newDate)
        {
             CurrentDate = newDate.Date; // Use .Date to strip time component
             _weekEventTriggeredThisCycle = false; // Reset week trigger
             Debug.Log($"TimeManager date explicitly set to: {CurrentDate.ToShortDateString()}");
             // Optionally trigger events if needed after loading? Be careful of order.
             // OnDayAdvanced?.Invoke(); // Maybe not desirable on load
        }

        /// <summary>
        /// Toggles the paused state of the TimeManager.
        /// </summary>
        /// <returns>The new paused state.</returns>
        public bool TogglePause()
        {
            IsPaused = !IsPaused;
            Debug.Log($"TimeManager Paused: {IsPaused}");
            return IsPaused;
        }
    }
}