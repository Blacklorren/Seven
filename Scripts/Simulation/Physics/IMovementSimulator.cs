using HandballManager.Simulation.MatchData;
using UnityEngine; // Added for Vector types

namespace HandballManager.Simulation.Physics // Changed from Interfaces to Physics
{
    public interface IMovementSimulator
    {
        /// <summary>
        /// Updates the positions and velocities of all players and the ball based on physics simulation.
        /// </summary>
        /// <param name="state">The current match state containing players and ball data.</param>
        /// <param name="timeStep">The time step for the simulation update in seconds.</param>
        void UpdateMovement(MatchState state, float timeStep);

        /// <summary>
        /// Updates the ball's position and velocity based on physics simulation.
        /// </summary>
        /// <param name="state">The current match state containing the ball data.</param>
        /// <param name="timeStep">The time step for the simulation update in seconds.</param>
        void UpdateBallPhysics(MatchState state, float timeStep);

        /// <summary>
        /// Checks for and resolves collisions between players and between players and the ball.
        /// </summary>
        /// <param name="state">The current match state containing players and ball data.</param>
        void ResolveCollisions(MatchState state);

        /// <summary>
        /// Updates player stamina based on their current activity level.
        /// </summary>
        /// <param name="state">The current match state containing player data.</param>
        /// <param name="timeStep">The time step for the simulation update in seconds.</param>
        void UpdateStamina(MatchState state, float timeStep);
    }
}