// --- START OF FILE HandballManager/Simulation/AI/Positioning/IGoalkeeperPositioner.cs ---
using HandballManager.Simulation.Core.MatchData; // Updated to reflect new location of MatchData
using UnityEngine; // For Vector2, Vector3

namespace HandballManager.Simulation.AI.Positioning
{
    /// <summary>
    /// Interface for calculating the target position for a goalkeeper.
    /// </summary>
    public interface IGoalkeeperPositioner
    {
        /// <summary>
        /// Calculates the optimal 2D target position for the goalkeeper to attempt a save against an incoming shot.
        /// </summary>
        /// <param name="gk">The goalkeeper SimPlayer.</param>
        /// <param name="state">The current match state (contains ball information).</param>
        /// <param name="predictedImpactPoint3D">The estimated 3D impact point of the ball on the goal plane.</param>
        /// <returns>The calculated 2D target position on the pitch.</returns>
        Vector2 GetGoalkeeperSavePosition(SimPlayer gk, MatchState state, Vector3 predictedImpactPoint3D);

        /// <summary>
        /// Calculates the optimal 2D target position for the goalkeeper when the opponent has possession (defensive positioning).
        /// Considers ball position, threat level, and GK attributes.
        /// </summary>
        /// <param name="gk">The goalkeeper SimPlayer.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>The calculated 2D target position on the pitch.</returns>
        Vector2 GetGoalkeeperDefensivePosition(SimPlayer gk, MatchState state);

        /// <summary>
        /// Calculates the optimal 2D target position for the goalkeeper when their own team has possession (attacking support positioning).
        /// </summary>
        /// <param name="gk">The goalkeeper SimPlayer.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>The calculated 2D target position on the pitch (typically further out).</returns>
        Vector2 GetGoalkeeperAttackingSupportPosition(SimPlayer gk, MatchState state);
    }
}
// --- END OF FILE HandballManager/Simulation/AI/Positioning/IGoalkeeperPositioner.cs ---