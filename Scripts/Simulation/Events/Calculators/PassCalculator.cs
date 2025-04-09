using HandballManager.Simulation.Constants;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Core.Constants; // For SimConstants
using UnityEngine;
using System;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations and resolution related to passing actions.
    /// </summary>
    public class PassCalculator
    {
        /// <summary>
        /// Resolves a pass attempt at the moment of release.
        /// Determines if the release is accurate or inaccurate.
        /// </summary>
        public ActionResult ResolvePassAttempt(SimPlayer passer, SimPlayer target, MatchState state)
        {
            if (passer == null || target == null || state == null)
                return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = passer, Reason = "Null input in ResolvePassAttempt" };

            float accuracyChance = CalculatePassAccuracy(passer, target, state);

            Vector3 passerPos3D = ActionCalculatorUtils.GetPosition3D(passer);
            Vector3 targetPos3D = ActionCalculatorUtils.GetPosition3D(target);

            if (state.RandomGenerator.NextDouble() < accuracyChance)
            {
                // ACCURATE RELEASE
                float baseSpeed = ActionResolverConstants.PASS_BASE_SPEED * Mathf.Lerp(0.8f, 1.2f, passer.BaseData.Passing / 100f);
                Vector3 direction3D = (targetPos3D - passerPos3D).normalized;
                if(direction3D.sqrMagnitude < SimConstants.FLOAT_EPSILON) direction3D = Vector3.forward;

                float launchAngle = ActionResolverConstants.PASS_BASE_LAUNCH_ANGLE_DEG + UnityEngine.Random.Range(-ActionResolverConstants.PASS_LAUNCH_ANGLE_VARIANCE_DEG, ActionResolverConstants.PASS_LAUNCH_ANGLE_VARIANCE_DEG) * (1.0f - accuracyChance);
                Vector3 axis = Vector3.Cross(direction3D, Vector3.up);
                // Ensure axis is valid before creating rotation
                Quaternion rotation = (axis.sqrMagnitude > SimConstants.FLOAT_EPSILON)
                                    ? Quaternion.AngleAxis(launchAngle, axis.normalized)
                                    : Quaternion.identity;
                Vector3 initialVelocity = rotation * direction3D * baseSpeed;

                float accurateOffsetAngle = UnityEngine.Random.Range(-ActionResolverConstants.PASS_ACCURATE_ANGLE_OFFSET_RANGE, ActionResolverConstants.PASS_ACCURATE_ANGLE_OFFSET_RANGE) * (1f - accuracyChance);
                Quaternion horizontalRotation = Quaternion.AngleAxis(accurateOffsetAngle, Vector3.up);
                initialVelocity = horizontalRotation * initialVelocity;

                state.Ball.ReleaseAsPass(passer, target, initialVelocity);

                return new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = passer, SecondaryPlayer = target, Reason = "Pass Released" };
            }
            else
            {
                // INACCURATE RELEASE
                Vector3 targetDirection3D = (targetPos3D - passerPos3D).normalized;
                if(targetDirection3D.sqrMagnitude < SimConstants.FLOAT_EPSILON) targetDirection3D = Vector3.forward;

                float angleOffset = UnityEngine.Random.Range(ActionResolverConstants.PASS_INACCURATE_ANGLE_OFFSET_MIN, ActionResolverConstants.PASS_INACCURATE_ANGLE_OFFSET_MAX) * (1.0f - accuracyChance) * (state.RandomGenerator.Next(0, 2) == 0 ? 1f : -1f);
                Quaternion horizontalRotation = Quaternion.AngleAxis(angleOffset, Vector3.up);
                Vector3 missDirection = horizontalRotation * targetDirection3D;
                float missSpeed = ActionResolverConstants.PASS_BASE_SPEED * UnityEngine.Random.Range(ActionResolverConstants.PASS_INACCURATE_SPEED_MIN_FACTOR, ActionResolverConstants.PASS_INACCURATE_SPEED_MAX_FACTOR);

                // Use SimConstants for release offset
                state.Ball.MakeLoose(passerPos3D + missDirection * SimConstants.BALL_RELEASE_OFFSET, missDirection * missSpeed, passer.TeamSimId, passer);

                return new ActionResult {
                   Outcome = ActionResultOutcome.Turnover, PrimaryPlayer = passer, SecondaryPlayer = target, Reason = "Pass Inaccurate",
                   ImpactPosition = CoordinateUtils.To2DGround(state.Ball.Position) // Use helper
                };
            }
        }

        /// <summary>
        /// Calculates the accuracy chance for a pass attempt.
        /// </summary>
        public float CalculatePassAccuracy(SimPlayer passer, SimPlayer target, MatchState state)
        {
            if (passer?.BaseData == null || target == null || state == null) return 0f;

            float accuracyChance = ActionResolverConstants.BASE_PASS_ACCURACY;

            // Attributes
            float passSkill = (passer.BaseData.Passing * ActionResolverConstants.PASS_SKILL_WEIGHT_PASSING +
                               passer.BaseData.DecisionMaking * ActionResolverConstants.PASS_SKILL_WEIGHT_DECISION +
                               passer.BaseData.Technique * ActionResolverConstants.PASS_SKILL_WEIGHT_TECHNIQUE);
            accuracyChance *= Mathf.Lerp(ActionResolverConstants.PASS_ACCURACY_SKILL_MIN_MOD, ActionResolverConstants.PASS_ACCURACY_SKILL_MAX_MOD, passSkill / 100f);

            // Distance
            float distance = Vector2.Distance(passer.Position, target.Position);
            accuracyChance -= Mathf.Clamp(distance * ActionResolverConstants.PASS_DISTANCE_FACTOR, 0f, 0.5f);

            // Pressure
            float pressure = ActionCalculatorUtils.CalculatePressureOnPlayer(passer, state); // Use Util
            float pressurePenalty = pressure * ActionResolverConstants.PASS_PRESSURE_FACTOR;
            pressurePenalty *= (1.0f - Mathf.Lerp(0f, ActionResolverConstants.PASS_COMPOSURE_FACTOR, passer.BaseData.Composure / 100f));
            accuracyChance -= pressurePenalty;

            return Mathf.Clamp01(accuracyChance);
        }
    }
}