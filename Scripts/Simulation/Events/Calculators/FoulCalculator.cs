// --- START OF FILE HandballManager/Simulation/Events/Calculators/FoulCalculator.cs ---
using HandballManager.Simulation.Constants;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Utils;
using UnityEngine;
using System;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations related to determining foul severity.
    /// </summary>
    public class FoulCalculator
    {
        /// <summary>
        /// Determines the severity of a foul based on context.
        /// </summary>
        public FoulSeverity DetermineFoulSeverity(SimPlayer tackler, SimPlayer target, MatchState state)
        {
            if (tackler?.BaseData == null || target?.BaseData == null || state == null) return FoulSeverity.FreeThrow;

            FoulSeverity severity = FoulSeverity.FreeThrow;
            float severityRoll = (float)state.RandomGenerator.NextDouble();
            float baseSeverityFactor = 0f;

            bool isFromBehind = ActionCalculatorUtils.IsTackleFromBehind(tackler, target); // Use Util
            if (isFromBehind) baseSeverityFactor += ActionResolverConstants.FOUL_SEVERITY_FROM_BEHIND_BONUS;

            float closingSpeed = ActionCalculatorUtils.CalculateClosingSpeed(tackler, target); // Use Util
            if (closingSpeed > ActionResolverConstants.MAX_PLAYER_SPEED * ActionResolverConstants.FOUL_SEVERITY_HIGH_SPEED_THRESHOLD_FACTOR)
                baseSeverityFactor += ActionResolverConstants.FOUL_SEVERITY_HIGH_SPEED_BONUS;

            baseSeverityFactor += Mathf.Clamp((tackler.BaseData.Aggression - 50f) / 50f, -1f, 1f) * ActionResolverConstants.FOUL_SEVERITY_AGGRESSION_FACTOR;

            bool clearScoringChance = ActionCalculatorUtils.IsClearScoringChance(target, state); // Use Util
            if (clearScoringChance) {
                baseSeverityFactor += ActionResolverConstants.FOUL_SEVERITY_DOGSO_BONUS;

                bool reckless = (isFromBehind && tackler.BaseData.Aggression > ActionResolverConstants.FOUL_SEVERITY_RECKLESS_AGGRESSION_THRESHOLD) ||
                                closingSpeed > ActionResolverConstants.MAX_PLAYER_SPEED * ActionResolverConstants.FOUL_SEVERITY_RECKLESS_SPEED_THRESHOLD_FACTOR;
                float redCardChanceDOGSO = ActionResolverConstants.FOUL_SEVERITY_DOGSO_RED_CARD_BASE_CHANCE
                                           + baseSeverityFactor * ActionResolverConstants.FOUL_SEVERITY_DOGSO_RED_CARD_SEVERITY_SCALE
                                           + (reckless ? ActionResolverConstants.FOUL_SEVERITY_DOGSO_RED_CARD_RECKLESS_BONUS : 0f);
                if (severityRoll < Mathf.Clamp01(redCardChanceDOGSO)) {
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