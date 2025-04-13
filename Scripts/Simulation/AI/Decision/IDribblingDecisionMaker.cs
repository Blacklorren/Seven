// --- START OF FILE HandballManager/Simulation/AI/Decision/IDribblingDecisionMaker.cs ---
using HandballManager.Simulation.Core.MatchData; // Updated to reflect new location of MatchData
using HandballManager.Gameplay; // For Tactic

namespace HandballManager.Simulation.AI.Decision // Updated to match new folder structure
{
    /// <summary>
    /// Interface for evaluating the desirability of dribbling for an AI player.
    /// </summary>
    public interface IDribblingDecisionMaker
    {
        /// <summary>
        /// Calculates a score representing how desirable dribbling/running with the ball is.
        /// </summary>
        /// <param name="player">The player considering dribbling.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's current tactic.</param>
        /// <returns>A score between 0 and 1 (higher is more desirable).</returns>
        float EvaluateDribbleScore(SimPlayer player, MatchState state, Tactic tactic);
    }
}
// --- END OF FILE HandballManager/Simulation/AI/DecisionMakers/IDribblingDecisionMaker.cs ---