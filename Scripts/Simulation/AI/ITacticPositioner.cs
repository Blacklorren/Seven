using HandballManager.Simulation.Core.MatchData; // Updated to reflect new location of MatchData
using UnityEngine;

namespace HandballManager.Simulation.AI // Updated to match new folder location
{
    /// <summary>
    /// Defines a contract for positioning players according to tactical formations and game situations.
    /// Calculates optimal positions for players based on the current match state and tactical setup.
    /// </summary>
    public interface ITacticPositioner
    {
        /// <summary>
        /// Calculates the optimal position for a player based on tactical considerations.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player to calculate position for.</param>
        /// <returns>The target position vector for the player.</returns>
        Vector2 GetPlayerTargetPosition(MatchState state, SimPlayer player);

        /// <summary>
        /// Updates the tactical positioning for all players in the match.
        /// </summary>
        /// <param name="state">The current match state containing all player data.</param>
        void UpdateTacticalPositioning(MatchState state);

        /// <summary>
        /// Calculates defensive positions for players based on the current match state.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="defendingTeamId">The team ID of the defending team.</param>
        void PositionDefensivePlayers(MatchState state, int defendingTeamId);

        /// <summary>
        /// Calculates offensive positions for players based on the current match state.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="attackingTeamId">The team ID of the attacking team.</param>
        void PositionOffensivePlayers(MatchState state, int attackingTeamId);
    }
}