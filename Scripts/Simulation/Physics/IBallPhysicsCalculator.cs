// --- START OF FILE HandballManager/Simulation/Physics/IBallPhysicsCalculator.cs ---
using HandballManager.Simulation.MatchData;
using UnityEngine; // For Vector2, Vector3

namespace HandballManager.Simulation.Physics

{
    /// <summary>
    /// Interface for calculating ball physics, including trajectory and intercept points.
    /// </summary>
    public interface IBallPhysicsCalculator
    {
        /// <summary>
        /// Estimates the optimal 2D intercept point for a player to meet a ball in flight.
        /// Considers ball trajectory and player speed.
        /// </summary>
        /// <param name="ball">The ball in flight.</param>
        /// <param name="interceptor">The player attempting to intercept/receive.</param>
        /// <returns>The estimated 2D (X, Z plane) position on the pitch.</returns>
        Vector2 EstimatePassInterceptPoint(SimBall ball, SimPlayer interceptor);

        /// <summary>
        /// Estimates the 3D point where the ball's trajectory will intersect the specified goal line plane.
        /// </summary>
        /// <param name="ball">The ball in flight.</param>
        /// <param name="defendingTeamSimId">The simulation ID (0 or 1) of the team defending the goal line.</param>
        /// <returns>The estimated 3D impact point (X, Y, Z).</returns>
        Vector3 EstimateBallGoalLineImpact3D(SimBall ball, int defendingTeamSimId);

        /// <summary>
        /// Projects the ball's 3D position forward in time, considering physics.
        /// </summary>
        /// <param name="startPos">The starting 3D position.</param>
        /// <param name="velocity">The initial 3D velocity.</param>
        /// <param name="time">The time duration to project forward.</param>
        /// <returns>The estimated 3D position after the specified time.</returns>
        Vector3 ProjectBallPosition(Vector3 startPos, Vector3 velocity, float time);

        // Potentially add methods for bounce, roll calculations if needed outside MovementSimulator
    }
}
// --- END OF FILE HandballManager/Simulation/Physics/IBallPhysicsCalculator.cs ---