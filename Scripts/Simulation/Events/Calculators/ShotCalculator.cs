using HandballManager.Simulation.Constants;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Core.Constants; // For SimConstants
using UnityEngine;
using System;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations and resolution related to shooting actions.
    /// </summary>
    public class ShotCalculator
    {
        /// <summary>
        /// Resolves the release of a shot. Sets the ball in flight with calculated inaccuracy and spin.
        /// </summary>
        public ActionResult ResolveShotAttempt(SimPlayer shooter, MatchState state)
        {
            if (shooter?.BaseData == null || state == null)
                return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = shooter, Reason = "Null input in ResolveShotAttempt" };

            Vector2 targetGoalCenter2D = (shooter.TeamSimId == 0)
                ? new Vector2(ActionResolverConstants.PITCH_LENGTH, ActionResolverConstants.PITCH_CENTER_Y)
                : new Vector2(0f, ActionResolverConstants.PITCH_CENTER_Y);

            float accuracyFactor = Mathf.Clamp(shooter.BaseData.ShootingAccuracy, 1f, 100f) / ActionResolverConstants.SHOT_ACCURACY_BASE;
            float pressure = ActionCalculatorUtils.CalculatePressureOnPlayer(shooter, state); // Use Util
            float composureEffect = Mathf.Lerp(1.0f, 1.0f - ActionResolverConstants.SHOT_COMPOSURE_FACTOR, shooter.BaseData.Composure / 100f);

            float maxAngleDeviation = ActionResolverConstants.SHOT_MAX_ANGLE_OFFSET_DEGREES * (1.0f - accuracyFactor);
            maxAngleDeviation *= (1.0f + pressure * ActionResolverConstants.SHOT_PRESSURE_INACCURACY_MOD * composureEffect);
            maxAngleDeviation = Mathf.Clamp(maxAngleDeviation, 0f, ActionResolverConstants.SHOT_MAX_ANGLE_OFFSET_DEGREES * ActionResolverConstants.SHOT_MAX_DEVIATION_CLAMP_FACTOR);

            float horizontalAngleOffset = UnityEngine.Random.Range(-maxAngleDeviation, maxAngleDeviation);

            Vector3 shooterPos3D = ActionCalculatorUtils.GetPosition3D(shooter); // Use Util
            Vector3 targetGoalCenter3D = new Vector3(targetGoalCenter2D.x, 1.2f, targetGoalCenter2D.y);

            float speed = ActionResolverConstants.SHOT_BASE_SPEED * Mathf.Lerp(0.8f, 1.2f, shooter.BaseData.ShootingPower / 100f);
            Vector3 idealDirection3D = (targetGoalCenter3D - shooterPos3D).normalized;
            if(idealDirection3D.sqrMagnitude < SimConstants.FLOAT_EPSILON) idealDirection3D = Vector3.forward;

            Quaternion horizontalRotation = Quaternion.AngleAxis(horizontalAngleOffset, Vector3.up);
            Vector3 actualDirection3D = horizontalRotation * idealDirection3D;

            float launchAngle = ActionResolverConstants.SHOT_BASE_LAUNCH_ANGLE_DEG + UnityEngine.Random.Range(-ActionResolverConstants.SHOT_LAUNCH_ANGLE_VARIANCE_DEG, ActionResolverConstants.SHOT_LAUNCH_ANGLE_VARIANCE_DEG);
            Vector3 horizontalAxis = Vector3.Cross(actualDirection3D, Vector3.up);
             // Ensure axis is valid before creating rotation
             Quaternion launchRotation = (horizontalAxis.sqrMagnitude > SimConstants.FLOAT_EPSILON)
                                    ? Quaternion.AngleAxis(launchAngle, horizontalAxis.normalized)
                                    : Quaternion.identity;
            actualDirection3D = launchRotation * actualDirection3D;

            Vector3 spinAxis = CalculateShotSpinAxis(shooter, horizontalAxis, actualDirection3D);
            float spinMagnitude = CalculateShotSpinMagnitude(shooter);
            Vector3 angularVelocity = spinAxis * spinMagnitude;

            state.Ball.ReleaseAsShot(shooter, actualDirection3D * speed, angularVelocity);

            return new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = shooter, Reason = "Shot Taken" };
        }

        private Vector3 CalculateShotSpinAxis(SimPlayer shooter, Vector3 horizontalAxis, Vector3 shotDirection)
        {
             if (shooter?.BaseData == null) return horizontalAxis; // Default if no data

             Vector3 defaultSpinAxis = horizontalAxis;
             float techniqueInfluence = shooter.BaseData.Technique / 100f;
             bool isRightDominant = UnityEngine.Random.value < 0.7f;
             float sideSpinFactor = techniqueInfluence * (isRightDominant ? 1f : -1f) * UnityEngine.Random.Range(0.5f, 1.0f);

             Vector3 spinAxis = Vector3.Lerp(
                 defaultSpinAxis,
                 new Vector3(0, sideSpinFactor, 0), // Simple side spin component
                 techniqueInfluence * ActionResolverConstants.SHOT_TECHNIQUE_SPIN_FACTOR
             );

              // Normalize only if the vector is not near zero
             if (spinAxis.sqrMagnitude > SimConstants.FLOAT_EPSILON) {
                 return spinAxis.normalized;
             } else {
                 return horizontalAxis; // Fallback to horizontal axis
             }
        }


        private float CalculateShotSpinMagnitude(SimPlayer shooter)
        {
             if (shooter?.BaseData == null) return 0f;

             float techniqueInfluence = shooter.BaseData.Technique / 100f;
             float powerInfluence = shooter.BaseData.ShootingPower / 100f;

             float spinMagnitude = ActionResolverConstants.SHOT_MAX_SPIN_MAGNITUDE *
                                  Mathf.Lerp(0.3f, 1.0f, techniqueInfluence) *
                                  Mathf.Lerp(0.7f, 1.1f, powerInfluence) *
                                  ActionResolverConstants.SHOT_TYPE_SPIN_FACTOR;

             spinMagnitude *= UnityEngine.Random.Range(0.8f, 1.2f);
             return spinMagnitude;
        }
    }
}