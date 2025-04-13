using HandballManager.Simulation.Core.MatchData; // Update to reflect new location of MatchData
using HandballManager.Gameplay; // For Tactic
using System.Collections.Generic;

namespace HandballManager.Simulation.AI.Decision // This namespace is already correctly updated in the file
{
    /// <summary>
    /// Represents the evaluated quality of a potential pass.
    /// </summary>
    public class PassOption
    {
        public SimPlayer Player { get; set; } // The potential receiver
        public float Score { get; set; }      // Overall desirability score (0-1, higher is better)
        public bool IsSafe { get; set; }      // Indicator if pass meets safety criteria
    }

    /// <summary>
    /// Interface for evaluating passing options for an AI player.
    /// </summary>
    public interface IPassingDecisionMaker
    {
        /// <summary>
        /// Evaluates all potential passing options for the passer.
        /// </summary>
        /// <param name="passer">The player considering the pass.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's current tactic.</param>
        /// <param name="safeOnly">If true, only consider options meeting safety criteria.</param>
        /// <returns>A list of PassOption objects, sorted by score descending.</returns>
        List<PassOption> EvaluatePassOptions(SimPlayer passer, MatchState state, Tactic tactic, bool safeOnly);

        /// <summary>
        /// Gets the single best pass option based on evaluation.
        /// </summary>
        /// <param name="passer">The player considering the pass.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's current tactic.</param>
        /// <returns>The best PassOption, or null if no suitable options exist.</returns>
        PassOption GetBestPassOption(SimPlayer passer, MatchState state, Tactic tactic);
    }
}