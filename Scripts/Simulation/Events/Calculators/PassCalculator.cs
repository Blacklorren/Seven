// --- START OF REVISED FILE PassCalculator.cs ---

using HandballManager.Simulation.Core;
using HandballManager.Simulation.Core.Constants; // For SimConstants, ActionResolverConstants
using HandballManager.Simulation.Core.MatchData;
using HandballManager.Simulation.Core.Utils;
using HandballManager.Data;
using UnityEngine;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations and resolution related to passing actions.
    /// Provides methods to determine pass accuracy and resolve the pass attempt itself.
    /// </summary>
    public class PassCalculator
    {
        // --- Constants Moved ---
        // It's generally better practice to define constants at the class level
        // or ideally, within a dedicated constants class (like ActionResolverConstants).
        // Assuming these might belong in ActionResolverConstants based on naming conventions.
        // If they are truly specific ONLY to this accurate pass calculation,
        // static readonly fields here would be appropriate. For this example,
        // let's assume they should be in ActionResolverConstants.
        // Example if they were moved to ActionResolverConstants:
        // private const float PASS_SPEED_LOW_FACTOR = ActionResolverConstants.PASS_ACCURATE_SPEED_LOW_FACTOR;
        // private const float PASS_SPEED_HIGH_FACTOR = ActionResolverConstants.PASS_ACCURATE_SPEED_HIGH_FACTOR;
        // For now, let's define them here as static readonly if they aren't elsewhere.
        private static readonly float PASS_SPEED_LOW_FACTOR = 0.8f; // Example value
        private static readonly float PASS_SPEED_HIGH_FACTOR = 1.2f; // Example value


        /// <summary>
        /// Resolves a pass attempt at the moment of release.
        /// Determines if the release is accurate or inaccurate and updates the ball state.
        /// </summary>
        /// <param name="passer">The player attempting the pass.</param>
        /// <param name="target">The intended recipient of the pass.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>An ActionResult indicating the outcome (Success, Turnover, or Failure).</returns>
        public ActionResult ResolvePassAttempt(SimPlayer passer, SimPlayer target, MatchState state)
        {
            // --- Input Validation ---
            // Check for null references early. Also check BaseData which is used extensively.
            if (passer?.BaseData == null || target?.BaseData == null || state?.Ball == null || state.RandomGenerator == null)
            {
                // Provide a more specific reason if possible
                string reason = "Invalid input: ";
                if (passer?.BaseData == null) reason += "Passer or Passer.BaseData is null. ";
                if (target?.BaseData == null) reason += "Target or Target.BaseData is null. ";
                if (state == null) reason += "MatchState is null. ";
                else if (state.Ball == null) reason += "MatchState.Ball is null. ";
                else if (state.RandomGenerator == null) reason += "MatchState.RandomGenerator is null.";

                return new ActionResult
                {
                    Outcome = ActionResultOutcome.Failure,
                    PrimaryPlayer = passer, // Passer might be null, but pass it anyway for context if available
                    SecondaryPlayer = target,
                    Reason = reason.Trim()
                };
            }

            // --- Pre-calculate common values ---
            Vector3 passerPos3D = ActionCalculatorUtils.GetPosition3D(passer);
            Vector3 targetPos3D = ActionCalculatorUtils.GetPosition3D(target);
            Vector3 directionToTarget3D = targetPos3D - passerPos3D;
            float distanceToTarget = directionToTarget3D.magnitude; // Keep distance if needed later, otherwise use sqrMagnitude for checks

            // Handle edge case where passer and target are at the exact same position
            if (directionToTarget3D.sqrMagnitude < SimConstants.FLOAT_EPSILON)
            {
                // Cannot determine a direction. Could fail, or use a default. Let's fail clearly.
                Debug.LogWarning($"Passer {passer.FullName} and target {target.FullName} are at the same position {passerPos3D}. Failing pass.");
                return new ActionResult
                {
                    Outcome = ActionResultOutcome.Failure,
                    PrimaryPlayer = passer,
                    SecondaryPlayer = target,
                    Reason = "Passer and target at same position"
                };
                // Or alternatively, use a default direction if failing isn't desired:
                // directionToTarget3D = Vector3.forward; // Use a default direction
                // distanceToTarget = 0f; // Although distance is technically 0
            }

            Vector3 normalizedDirection = directionToTarget3D.normalized; // Normalize *once* after the check

            // --- Calculate Accuracy ---
            float accuracyChance = CalculatePassAccuracy(passer, target, state, distanceToTarget); // Pass pre-calculated distance

            // --- Determine Outcome (Accurate vs Inaccurate) ---
            if (state.RandomGenerator.NextDouble() < accuracyChance)
            {
                // --- ACCURATE RELEASE ---
                // Calculate base speed based on passer's skill
                float baseSpeed = ActionResolverConstants.PASS_BASE_SPEED *
                    Mathf.Lerp(PASS_SPEED_LOW_FACTOR, PASS_SPEED_HIGH_FACTOR, passer.BaseData.Passing / 100f);

                // Calculate vertical launch angle variation
                float launchAngleVariance = ActionResolverConstants.PASS_LAUNCH_ANGLE_VARIANCE_DEG;
                float launchAngle = ActionResolverConstants.PASS_BASE_LAUNCH_ANGLE_DEG +
                                    (float)(state.RandomGenerator.NextDouble() * 2.0 - 1.0) * launchAngleVariance; // More concise random range [-Var, +Var]

                // Calculate rotation axis for vertical launch angle (perpendicular to direction and up)
                Vector3 verticalRotationAxis = Vector3.Cross(normalizedDirection, Vector3.up);
                Quaternion verticalRotation = Quaternion.identity; // Default to no rotation

                // Ensure axis is valid before creating rotation (prevents issues if direction is purely vertical)
                if (verticalRotationAxis.sqrMagnitude > SimConstants.FLOAT_EPSILON)
                {
                    verticalRotation = Quaternion.AngleAxis(launchAngle, verticalRotationAxis.normalized);
                }
                else
                {
                    // Direction is likely straight up or down, vertical rotation axis is zero.
                    // Log warning or handle as needed. Applying no extra vertical rotation is safe.
                    Debug.LogWarning($"Pass direction {normalizedDirection} is parallel to Vector3.up. Skipping vertical launch angle rotation.");
                }

                // Apply vertical launch angle rotation
                Vector3 velocityAfterVertical = verticalRotation * normalizedDirection * baseSpeed;

                // Calculate slight horizontal inaccuracy for "accurate" passes (more accurate = less offset)
                float accurateOffsetAngleDeg = UnityEngine.Random.Range(
                                                -ActionResolverConstants.PASS_ACCURATE_ANGLE_OFFSET_RANGE,
                                                 ActionResolverConstants.PASS_ACCURATE_ANGLE_OFFSET_RANGE)
                                               * (1f - accuracyChance); // Less deviation for higher accuracy chance

                Quaternion horizontalRotation = Quaternion.AngleAxis(accurateOffsetAngleDeg, Vector3.up);

                // Apply horizontal offset rotation
                Vector3 finalInitialVelocity = horizontalRotation * velocityAfterVertical;

                // Release the ball towards the target
                state.Ball.ReleaseAsPass(passer, target, finalInitialVelocity);

                return new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = passer, SecondaryPlayer = target, Reason = "Pass Released Accurately" };
            }
            else
            {
                // --- INACCURATE RELEASE ---
                // Calculate significant horizontal angle offset (more inaccurate = more offset)
                float inaccurateAngleOffsetMax = ActionResolverConstants.PASS_INACCURATE_ANGLE_OFFSET_MAX;
                float inaccurateAngleOffsetMin = ActionResolverConstants.PASS_INACCURATE_ANGLE_OFFSET_MIN;
                // Ensure min is not greater than max if constants are configurable
                float baseAngleOffset = UnityEngine.Random.Range(inaccurateAngleOffsetMin, inaccurateAngleOffsetMax);
                float angleOffset = baseAngleOffset * (1.0f - accuracyChance); // Scale offset by inaccuracy factor
                angleOffset *= (state.RandomGenerator.Next(0, 2) == 0 ? 1f : -1f); // Randomize direction (left/right)

                Quaternion horizontalRotation = Quaternion.AngleAxis(angleOffset, Vector3.up);

                // Determine the direction the inaccurate pass goes
                Vector3 missDirection = horizontalRotation * normalizedDirection;

                // Calculate speed for the inaccurate pass (usually slower or more variable)
                float missSpeed = ActionResolverConstants.PASS_BASE_SPEED *
                                  UnityEngine.Random.Range(ActionResolverConstants.PASS_INACCURATE_SPEED_MIN_FACTOR,
                                                           ActionResolverConstants.PASS_INACCURATE_SPEED_MAX_FACTOR);

                // Calculate release position with a small offset from the passer
                Vector3 releasePosition = passerPos3D + missDirection * SimConstants.BALL_RELEASE_OFFSET;

                // Make the ball loose (turnover)
                state.Ball.MakeLoose(releasePosition, missDirection * missSpeed, passer.TeamSimId, passer);

                return new ActionResult
                {
                    Outcome = ActionResultOutcome.Turnover,
                    PrimaryPlayer = passer,
                    SecondaryPlayer = target, // Target is still relevant contextually
                    Reason = "Pass Inaccurate",
                    ImpactPosition = CoordinateUtils.To2DGround(state.Ball.Position) // Use helper for ground position
                };
            }
        }

        /// <summary>
        /// Calculates the accuracy chance (0.0 to 1.0) for a pass attempt based on various factors.
        /// </summary>
        /// <param name="passer">The player attempting the pass.</param>
        /// <param name="target">The intended recipient of the pass.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="distance">Optional pre-calculated distance between passer and target.</param>
        /// <returns>A float representing the probability of the pass being accurate (0.0 to 1.0).</returns>
        public float CalculatePassAccuracy(SimPlayer passer, SimPlayer target, MatchState state, float? distance = null)
        {
            // Basic validation
            // Note: These checks might be redundant if called only from ResolvePassAttempt which already validates.
            // However, if this method could be called externally, these checks are essential.
            if (passer?.BaseData == null || target == null || state == null)
            {
                Debug.LogError("CalculatePassAccuracy called with invalid parameters.");
                return 0f;
            }

            // Start with the base accuracy defined in constants
            float accuracyChance = ActionResolverConstants.BASE_PASS_ACCURACY;

            // --- Factor 1: Player Skills ---
            // Combine relevant skills with weighting factors
            // Ensure division by 300 if weights sum to 1 and attributes are 0-100. Or normalize differently.
            // Assuming weights add up to 1 here for a 0-100 combined skill score.
            float passSkill = (passer.BaseData.Passing * ActionResolverConstants.PASS_SKILL_WEIGHT_PASSING +
                               passer.BaseData.DecisionMaking * ActionResolverConstants.PASS_SKILL_WEIGHT_DECISION +
                               passer.BaseData.Technique * ActionResolverConstants.PASS_SKILL_WEIGHT_TECHNIQUE);

            // Apply skill modifier using Lerp between min/max modifiers defined in constants
            // Ensure passSkill is clamped between 0 and 100 if necessary before dividing
            float clampedSkill = Mathf.Clamp(passSkill, 0f, 100f);
            accuracyChance *= Mathf.Lerp(ActionResolverConstants.PASS_ACCURACY_SKILL_MIN_MOD,
                                         ActionResolverConstants.PASS_ACCURACY_SKILL_MAX_MOD,
                                         clampedSkill / 100f);

            // --- Factor 2: Distance ---
            // Calculate distance if not provided
            float dist = distance ?? Vector2.Distance(passer.Position, target.Position);
            // Apply penalty based on distance. Clamp the penalty effect to avoid excessive reduction.
            // Example: Max penalty of 0.5 (50% reduction) from distance alone.
            float distancePenalty = Mathf.Clamp(dist * ActionResolverConstants.PASS_DISTANCE_FACTOR, 0f, ActionResolverConstants.PASS_DISTANCE_MAX_PENALTY_ABS); // Use a constant for max penalty
            accuracyChance -= distancePenalty;


            // --- Factor 3: Pressure ---
            // Calculate pressure on the passer using a utility function
            float pressure = ActionCalculatorUtils.CalculatePressureOnPlayer(passer, state);
            // Calculate base pressure penalty
            float pressurePenalty = pressure * ActionResolverConstants.PASS_PRESSURE_FACTOR;
            // Reduce penalty based on passer's composure (higher composure = less affected by pressure)
            float composureModifier = Mathf.Lerp(0f, ActionResolverConstants.PASS_COMPOSURE_MAX_EFFECT, passer.BaseData.Composure / 100f); // Use a constant for max effect
            pressurePenalty *= (1.0f - composureModifier);
            // Apply pressure penalty
            accuracyChance -= pressurePenalty;

            // --- Factor 4: Fatigue (Optional Example) ---
            // if (ActionResolverConstants.PASS_FATIGUE_FACTOR > 0)
            // {
            //     float fatiguePenalty = passer.CurrentFatigue * ActionResolverConstants.PASS_FATIGUE_FACTOR; // Assuming CurrentFatigue is 0-1
            //     accuracyChance -= fatiguePenalty;
            // }

            // --- Final Step: Clamp Result ---
            // Ensure the final accuracy chance is within the valid probability range [0, 1]
            return Mathf.Clamp01(accuracyChance);
        }
    }
}
// --- END OF REVISED FILE PassCalculator.cs ---