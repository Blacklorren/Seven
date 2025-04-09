// --- START OF FILE HandballManager/Simulation/Events/IActionResolver.cs ---
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Core; // For ActionResult

namespace HandballManager.Simulation.Events
{
    /// <summary>
    /// Interface for resolving player actions like passes, shots, and tackles.
    /// </summary>
    public interface IActionResolver
    {
        /// <summary>
        /// Resolves the outcome of a prepared player action (Pass, Shot, Tackle).
        /// </summary>
        /// <param name="player">The player whose action timer completed.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>An ActionResult describing the outcome.</returns>
        ActionResult ResolvePreparedAction(SimPlayer player, MatchState state);

        /// <summary>
        /// Calculates the probability of a defender intercepting a pass currently in flight.
        /// Called reactively by the event detector.
        /// </summary>
        /// <param name="defender">The defending player.</param>
        /// <param name="ball">The ball in flight.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>Probability score (0-1).</returns>
        float CalculateInterceptionChance(SimPlayer defender, SimBall ball, MatchState state);

        /// <summary>
        /// Calculates tackle success and foul probabilities for AI decision making or analysis.
        /// </summary>
        /// <param name="tackler">The potential tackler.</param>
        /// <param name="target">The player being targeted.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>A tuple containing (successChance, foulChance).</returns>
        (float successChance, float foulChance) CalculateTackleProbabilities(SimPlayer tackler, SimPlayer target, MatchState state);
    }
}
// --- END OF FILE HandballManager/Simulation/Interfaces/IActionResolver.cs ---