// --- START OF FILE HandballManager/Core/Time/IGameTimeProvider.cs ---
using System; // For DateTime

namespace HandballManager.Core.Time
{
    /// <summary>
    /// Defines a contract for providing the current simulated game date and time.
    /// Decouples components from the specific implementation of the game's time progression.
    /// </summary>
    public interface IGameTimeProvider
    {
        /// <summary>
        /// Gets the current date within the simulation.
        /// </summary>
        DateTime CurrentDate { get; }

        // Optional: Add CurrentDateTime if precise time is also needed
        // DateTime CurrentDateTime { get; }
    }
}
// --- END OF FILE HandballManager/Core/Time/IGameTimeProvider.cs ---