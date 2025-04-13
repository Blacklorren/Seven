// --- START OF FILE HandballManager/Simulation/Events/Calculators/FoulCalculator.cs ---
using UnityEngine;
using System;
using HandballManager.Simulation.Core.MatchData;
using HandballManager.Simulation.Core.Constants;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations related to determining foul severity.
    /// </summary>
    public class FoulCalculator
    {
        /// <summary>
        /// Determines the severity of a foul based on tackle context and match state
        /// </summary>
        /// <param name="tackler">Player committing the foul</param>
        /// <param name="target">Player being fouled</param>
        /// <param name="state">Current match state</param>
        /// <returns>Calculated foul severity level</returns>
        public FoulSeverity DetermineFoulSeverity(SimPlayer tackler, SimPlayer target, MatchState state)
        {
            if (tackler?.BaseData is null || target?.BaseData is null || state is null) 
    return FoulSeverity.FreeThrow;

            FoulSeverity severity = FoulSeverity.FreeThrow;
            float severityRoll = (float)state.RandomGenerator.NextDouble();
            float baseSeverityFactor = 0f;

            bool isFromBehind = ActionCalculatorUtils.IsTackleFromBehind(tackler, target); // Use Util
            if (isFromBehind) baseSeverityFactor += ActionResolverConstants.FOUL_SEVERITY_FROM_BEHIND_BONUS;

            if (ActionResolverConstants.MAX_PLAYER_SPEED <= 0f)
    throw new InvalidOperationException("MAX_PLAYER_SPEED must be positive");

float closingSpeed = ActionCalculatorUtils.CalculateClosingSpeed(tackler, target); // Use Util
            if (closingSpeed > ActionResolverConstants.MAX_PLAYER_SPEED * ActionResolverConstants.FOUL_SEVERITY_HIGH_SPEED_THRESHOLD_FACTOR)
                baseSeverityFactor += ActionResolverConstants.FOUL_SEVERITY_HIGH_SPEED_BONUS;

            baseSeverityFactor += Mathf.Clamp((tackler.BaseData.Aggression - 50f) / 50f, -1f, 1f) * ActionResolverConstants.FOUL_SEVERITY_AGGRESSION_FACTOR;

            bool clearScoringChance = ActionCalculatorUtils.IsClearScoringChance(target, state); // Use Util
            if (clearScoringChance) {
                baseSeverityFactor += ActionResolverConstants.FOUL_SEVERITY_DOGSO_BONUS;

                bool reckless = (isFromBehind && tackler.BaseData.Aggression > ActionResolverConstants.FOUL_SEVERITY_RECKLESS_AGGRESSION_THRESHOLD) ||
                                closingSpeed > ActionResolverConstants.MAX_PLAYER_SPEED * ActionResolverConstants.FOUL_SEVERITY_RECKLESS_SPEED_THRESHOLD_FACTOR;
                float redCardChanceDOGSO = MathF.Clamp(
    ActionResolverConstants.FOUL_SEVERITY_DOGSO_RED_CARD_BASE_CHANCE
    + baseSeverityFactor * ActionResolverConstants.FOUL_SEVERITY_DOGSO_RED_CARD_SEVERITY_SCALE
    + (reckless ? ActionResolverConstants.FOUL_SEVERITY_DOGSO_RED_CARD_RECKLESS_BONUS : 0f),
    0f, 1f
);
                if (severityRoll < redCardChanceDOGSO) {
                    return FoulSeverity.RedCard;
                }
            }

            float twoMinuteThreshold = ActionResolverConstants.FOUL_SEVERITY_TWO_MINUTE_THRESHOLD_BASE + baseSeverityFactor * ActionResolverConstants.FOUL_SEVERITY_TWO_MINUTE_THRESHOLD_SEVERITY_SCALE;
            float redCardThreshold = ActionResolverConstants.FOUL_SEVERITY_RED_CARD_THRESHOLD_BASE + baseSeverityFactor * ActionResolverConstants.FOUL_SEVERITY_RED_CARD_THRESHOLD_SEVERITY_SCALE;

            twoMinuteThreshold = Mathf.Clamp01(twoMinuteThreshold);
            redCardThreshold = Mathf.Clamp01(redCardThreshold);

            if (severityRoll < redCardThreshold) { severity = FoulSeverity.RedCard; }
            else if (severityRoll < twoMinuteThreshold) { severity = FoulSeverity.TwoMinuteSuspension; }

            // TODO: Add logic for OffensiveFoul based on movement/charge?

            return severity;
        }
    }
}