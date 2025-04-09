using HandballManager.Simulation.Core.MatchData; // Updated to reflect new location of MatchData
using HandballManager.Core;
using UnityEngine; // Missing dependency for Vector2

namespace HandballManager.Simulation.AI // Updated to match new folder location
{
    /// <summary>
    /// Defines a contract for orchestrating the decisions of AI-controlled players during a match simulation.
    /// Queries specialized decision makers and evaluators to determine the player's intended action and target.
    /// </summary>
    public interface IPlayerAIController
    {
        /// <summary>
        /// Updates AI decisions for all players in the match based on the current game state.
        /// </summary>
        /// <param name="state">The current match state containing all player and game data.</param>
        /// <param name="timeStep">The time step for the simulation update in seconds.</param>
        void UpdatePlayerDecisions(MatchState state, float timeStep);

        /// <summary>
        /// Determines the best action for a specific player based on the current game state.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player to make a decision for.</param>
        /// <returns>The action the player should take.</returns>
        PlayerAction DeterminePlayerAction(MatchState state, PlayerData player);

        /// <summary>
        /// Calculates the optimal position for a player based on tactical considerations.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player to calculate position for.</param>
        /// <returns>The target position vector for the player.</returns>
        Vector2 CalculatePlayerPosition(MatchState state, PlayerData player);

        /// <summary>
        /// Evaluates potential passing targets and selects the best receiver.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="passer">The player attempting to pass.</param>
        /// <returns>The best player to receive the pass, or null if no good option exists.</returns>
        PlayerData FindBestPassTarget(MatchState state, PlayerData passer);
    }
}