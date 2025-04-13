// --- START OF FILE HandballManager/Simulation/AI/Evaluation/ITacticalEvaluator.cs ---
using HandballManager.Gameplay; // For Tactic, Enums
using HandballManager.Simulation.Core.MatchData; // Updated to reflect new location of MatchData
using HandballManager.Core; // For PlayerAction enum

namespace HandballManager.Simulation.AI.Evaluation // Updated from Evaluators to Evaluation
{
    /// <summary>
    /// Interface for evaluating tactical influences on AI decision-making.
    /// </summary>
    public interface ITacticalEvaluator
    {
        /// <summary>
        /// Gets a modifier based on the team's tactical risk level.
        /// Higher values encourage riskier actions.
        /// </summary>
        /// <param name="tactic">The team's tactic.</param>
        /// <returns>A multiplier (e.g., 1.0 for neutral, >1 for high risk, <1 for low risk).</returns>
        float GetRiskModifier(Tactic tactic);

        /// <summary>
        /// Gets a modifier based on the team's tactical pace.
        /// Higher values encourage faster actions (shooting/direct passes).
        /// </summary>
        /// <param name="tactic">The team's tactic.</param>
        /// <returns>A multiplier.</returns>
        float GetPaceModifier(Tactic tactic);

        /// <summary>
        /// Checks if a potential action aligns with the team's offensive focus play.
        /// </summary>
        /// <param name="player">The player considering the action.</param>
        /// <param name="potentialTarget">The potential target player (for passes) or null (for shots/dribbles).</param>
        /// <param name="actionType">The type of action being considered.</param>
        /// <param name="focus">The team's offensive focus play setting.</param>
        /// <returns>True if the action aligns with the focus, false otherwise.</returns>
        bool DoesActionMatchFocus(SimPlayer player, SimPlayer potentialTarget, PlayerAction actionType, OffensiveFocusPlay focus);

        // Add other methods as needed, e.g., GetDefensiveAggressionModifier(Tactic tactic)
    }
}
// --- END OF FILE HandballManager/Simulation/AI/Evaluation/ITacticalEvaluator.cs ---