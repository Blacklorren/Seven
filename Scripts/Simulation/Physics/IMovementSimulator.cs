using HandballManager.Simulation.Core.MatchData;

namespace HandballManager.Simulation.Physics
{
    public interface IMovementSimulator
    {
        /// <summary>
        /// Updates the positions and velocities of all players and the ball based on physics simulation.
        /// </summary>
        /// <param name="state">The current match state containing players and ball data.</param>
        /// <param name="timeStep">The time step for the simulation update in seconds.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        /// <remarks>
        /// This method does not accept null arguments. The <paramref name="timeStep"/> should be obtained from the simulation timer.
        /// </remarks>
        void UpdateMovement(MatchState state, float timeStep);

        /// <summary>
        /// Updates the ball's position and velocity based on physics simulation.
        /// </summary>
        /// <param name="state">The current match state containing the ball data.</param>
        /// <param name="timeStep">The time step for the simulation update in seconds.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        /// <remarks>
        /// Null checks are performed on all input parameters. Time step values should align with simulation constants.
        /// </remarks>
        void UpdateBallPhysics(MatchState state, float timeStep);

        /// <summary>
        /// Checks for and resolves collisions between players and between players and the ball.
        /// </summary>
        /// <param name="state">The current match state containing players and ball data.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        /// <remarks>
        /// Collision resolution uses physics constants defined in the simulation parameters.
        /// </remarks>
        void ResolveCollisions(MatchState state);

        /// <summary>
        /// Updates player stamina based on their current activity level.
        /// </summary>
        /// <param name="state">The current match state containing player data.</param>
        /// <param name="timeStep">The time step for the simulation update in seconds.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        /// <remarks>
        /// Stamina calculations incorporate simulation time constants for accurate energy depletion.
        /// </remarks>
        void UpdateStamina(MatchState state, float timeStep);

        /// <summary>
        /// Ensures all entities remain within valid pitch boundaries.
        /// </summary>
        /// <param name="state">The current match state containing players and ball data.</param>
        /// <exception cref="ArgumentNullException">Thrown when state is null.</exception>
        /// <remarks>
        /// Applies boundary constraints to prevent players and ball from leaving the pitch area.
        /// </remarks>
        void EnforceBoundaries(MatchState state);

        /// <summary>
        /// Handles special movement cases during set pieces or specific game situations.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="situationType">The type of special situation being handled.</param>
        /// <remarks>
        /// Applies specific movement rules for situations like free throws or penalties.
        /// </remarks>
        void HandleSpecialMovement(MatchState state, GameSituationType situationType);
    }
}