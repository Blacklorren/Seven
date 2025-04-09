using HandballManager.Data; // For PlayerData, PlayerPersonalityTrait
using UnityEngine;          // For Vector2 (optional, if adjusting position)

namespace HandballManager.Simulation.AI.Evaluation // Updated from Evaluators to Evaluation
{
    /// <summary>
    /// Interface for evaluating personality trait influences on AI decision-making.
    /// </summary>
    public interface IPersonalityEvaluator
    {
        /// <summary>
        /// Gets a modifier affecting the threshold or score for risky actions.
        /// </summary>
        /// <param name="playerData">The player's base data containing personality traits.</param>
        /// <returns>A multiplier (e.g., >1 for ambitious/volatile, <1 for professional/loyal).</returns>
        float GetRiskModifier(PlayerData playerData);

        /// <summary>
        /// Gets a modifier affecting the score for shooting actions.
        /// </summary>
        /// <param name="playerData">The player's base data.</param>
        /// <returns>A multiplier (e.g., >1 for ambitious/determined).</returns>
        float GetShootingTendencyModifier(PlayerData playerData);

        /// <summary>
        /// Gets a modifier affecting the score for passing actions.
        /// </summary>
        /// <param name="playerData">The player's base data.</param>
        /// <returns>A multiplier (e.g., >1 for loyal).</returns>
        float GetPassingTendencyModifier(PlayerData playerData);

        /// <summary>
        /// Gets a modifier affecting the score for dribbling actions.
        /// </summary>
        /// <param name="playerData">The player's base data.</param>
        /// <returns>A multiplier (e.g., >1 for ambitious/volatile).</returns>
        float GetDribblingTendencyModifier(PlayerData playerData);

        /// <summary>
        /// Gets a modifier affecting the decision threshold for tackling.
        /// </summary>
        /// <param name="playerData">The player's base data.</param>
        /// <returns>A multiplier (e.g., >1 for aggressive/determined, <1 for professional).</returns>
        float GetTacklingTendencyModifier(PlayerData playerData);

        /// <summary>
        /// Gets a modifier affecting action preparation time (lower = less hesitation).
        /// </summary>
        /// <param name="playerData">The player's base data.</param>
        /// <returns>A multiplier (e.g., <1 for determined/volatile).</returns>
        float GetHesitationModifier(PlayerData playerData);

        // Optional: Add methods for work rate effect on positioning if desired
        // float GetWorkRatePositioningFactor(PlayerData playerData);
        // Vector2 AdjustPositionForWorkRate(PlayerData playerData, Vector2 currentPosition, Vector2 targetPosition);
    }
}