// --- START OF FILE HandballManager/Simulation/AI/Decision/IDefensiveDecisionMaker.cs ---
using HandballManager.Simulation.Core.MatchData; // Updated to new namespace
using HandballManager.Gameplay; // For Tactic
using UnityEngine; // For Vector2
using HandballManager.Core; // For PlayerAction enum

namespace HandballManager.Simulation.AI.Decision // Updated namespace to match new folder structure
{
    /// <summary>
    /// Represents the chosen defensive action and associated target information.
    /// </summary>
    public class DefensiveAction
    {
        public PlayerAction Action { get; set; } = PlayerAction.Idle; // The chosen action (Tackle, Block, Mark, Move)
        public SimPlayer TargetPlayer { get; set; } = null;        // Target opponent (for Tackle or Mark)
        public Vector2 TargetPosition { get; set; } = Vector2.zero; // Target position (for Block or Move)
    }

    /// <summary>
    /// Interface for determining the primary defensive action for an AI player.
    /// </summary>
    public interface IDefensiveDecisionMaker
    {
        /// <summary>
        /// Decides the best defensive action for the player based on the current state and tactics.
        /// </summary>
        /// <param name="player">The defending player.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's current tactic.</param>
        /// <returns>A DefensiveAction object containing the chosen action and target details.</returns>
        DefensiveAction DecideDefensiveAction(SimPlayer player, MatchState state, Tactic tactic);
    }
}
// --- END OF FILE HandballManager/Simulation/AI/Decision/IDefensiveDecisionMaker.cs ---