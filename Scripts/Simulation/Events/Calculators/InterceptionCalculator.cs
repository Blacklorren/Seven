// --- START OF FILE HandballManager/Simulation/Events/Calculators/InterceptionCalculator.cs ---
using HandballManager.Simulation.Constants;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Utils;
using UnityEngine;
using System;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations related to interception chances.
    /// </summary>
    public class InterceptionCalculator
    {
        /// <summary>
        /// Calculates the probability of a defender intercepting a specific pass in flight.
        /// </summary>
        public float CalculateInterceptionChance(SimPlayer defender, SimBall ball, MatchState state)
        {
            if (defender?.BaseData == null || ball == null || state == null || !ball.IsInFlight || ball.IntendedTarget == null || ball.Passer == null || defender.TeamSimId == ball.Passer.TeamSimId)
                return 0f;

            float baseChance = ActionResolverConstants.INTERCEPTION_BASE_CHANCE;

            // Skill
            float defenderSkill = (defender.BaseData.Anticipation * ActionResolverConstants.INTERCEPTION_SKILL_WEIGHT_ANTICIPATION +
                                   defender.BaseData.Agility * ActionResolverConstants.INTERCEPTION_SKILL_WEIGHT_AGILITY +
                                   defender.BaseData.Positioning * ActionResolverConstants.INTERCEPTION_SKILL_WEIGHT_POSITIONING);
            float skillMod = Mathf.Lerp(ActionResolverConstants.INTERCEPTION_SKILL_MIN_MOD, ActionResolverConstants.INTERCEPTION_SKILL_MAX_MOD, defenderSkill / 100f);

            // Position
            float distToLine = SimulationUtils.CalculateDistanceToLine(defender.Position, CoordinateUtils.To2DGround(ball.PassOrigin), ball.IntendedTarget.Position); // Use helpers
            float lineProximityFactor = Mathf.Clamp01(1.0f - (distToLine / ActionResolverConstants.INTERCEPTION_RADIUS));
            lineProximityFactor *= lineProximityFactor;

            Vector2 ballPos2D = CoordinateUtils.To2DGround(ball.Position); // Use helper
            float distToBall = Vector2.Distance(defender.Position, ballPos2D);
            float ballProximityFactor = Mathf.Clamp01(1.0f - (distToBall / (ActionResolverConstants.INTERCEPTION_RADIUS * ActionResolverConstants.INTERCEPTION_RADIUS_EXTENDED_FACTOR)));

            // Pass Properties
            float passDistTotal = Vector2.Distance(CoordinateUtils.To2DGround(ball.PassOrigin), ball.IntendedTarget.Position);
            if (passDistTotal < 1.0f) passDistTotal = 1.0f;
            float passDistTravelled = Vector2.Distance(CoordinateUtils.To2DGround(ball.PassOrigin), ballPos2D);
            float passProgress = Mathf.Clamp01(passDistTravelled / passDistTotal);
            float passProgressFactor = ActionResolverConstants.INTERCEPTION_PASS_PROGRESS_BASE_FACTOR + (ActionResolverConstants.INTERCEPTION_PASS_PROGRESS_MIDPOINT_BONUS * Mathf.Sin(passProgress * Mathf.PI));

            // Ball Speed
            float ballSpeedFactor = Mathf.Clamp(1.0f - (ball.Velocity.magnitude / (ActionResolverConstants.PASS_BASE_SPEED * 1.5f)), // Use constant
                                                1.0f - ActionResolverConstants.INTERCEPTION_PASS_SPEED_MAX_PENALTY, 1.0f);

            // Combine
            float finalChance = baseChance
                              * Mathf.Lerp(1.0f, skillMod, ActionResolverConstants.INTERCEPTION_ATTRIBUTE_WEIGHT)
                              * Mathf.Lerp(1.0f, lineProximityFactor * ballProximityFactor, ActionResolverConstants.INTERCEPTION_POSITION_WEIGHT)
                              * passProgressFactor
                              * ballSpeedFactor;

            // Movement Direction
            if (defender.Velocity.sqrMagnitude > 1f) {
                Vector2 defenderToBallDir = (ballPos2D - defender.Position).normalized;
                float closingFactor = Vector2.Dot(defender.Velocity.normalized, defenderToBallDir);
                finalChance *= Mathf.Lerp(ActionResolverConstants.INTERCEPTION_CLOSING_FACTOR_MIN_SCALE, ActionResolverConstants.INTERCEPTION_CLOSING_FACTOR_MAX_SCALE, (closingFactor + 1f) / 2f);
            }

            return Mathf.Clamp01(finalChance);
        }
    }
}