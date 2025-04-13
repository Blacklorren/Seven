// --- START OF FILE HandballManager/Simulation/AI/Evaluation/IGameStateEvaluator.cs ---
using HandballManager.Simulation.Core.MatchData; // Updated to reflect new location of MatchData

namespace HandballManager.Simulation.AI.Evaluation // This namespace is already correctly noted in the comments
{
    /// <summary>
    /// Interface for evaluating game state influences (score, time) on AI decision-making.
    /// </summary>
    public interface IGameStateEvaluator
    {
        /// <summary>
        /// Gets a modifier influencing the risk level of offensive actions.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="playerTeamId">The simulation ID (0 or 1) of the player's team.</param>
        /// <returns>A multiplier (e.g., >1 when desperate, <1 when time-wasting).</returns>
        float GetAttackRiskModifier(MatchState state, int playerTeamId);

        /// <summary>
        /// Gets a modifier influencing the aggression level of defensive actions.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="playerTeamId">The simulation ID (0 or 1) of the player's team.</param>
        /// <returns>A multiplier (e.g., >1 when desperate, <1 when cautious).</returns>
        float GetDefensiveAggressionModifier(MatchState state, int playerTeamId);

        /// <summary>
        /// Gets a modifier influencing the goalkeeper's willingness to make safe vs. risky passes.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="playerTeamId">The simulation ID (0 or 1) of the goalkeeper's team.</param>
        /// <returns>A multiplier on the pass safety threshold (e.g., >1 encourages holding, <1 encourages quick distribution).</returns>
        float GetGoalkeeperPassSafetyModifier(MatchState state, int playerTeamId);

        /// <summary>
        /// Checks if the situation warrants a counter-attack mentality.
        /// </summary>
        /// <param name="player">The player being evaluated.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>True if a counter-attack is feasible/desirable, false otherwise.</returns>
        bool IsCounterAttackOpportunity(SimPlayer player, MatchState state);
    }
}
// --- END OF FILE HandballManager/Simulation/AI/Evaluators/IGameStateEvaluator.cs ---