// --- START OF FILE HandballManager/Simulation/Events/Calculators/ActionCalculatorUtils.cs ---
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Constants;
using HandballManager.Simulation.Core.Constants;
using UnityEngine;
using System.Linq;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Provides utility methods shared across action calculators.
    /// </summary>
    public static class ActionCalculatorUtils
    {
        /// <summary>
        /// Calculates the pressure on a player from nearby opponents.
        /// </summary>
        public static float CalculatePressureOnPlayer(SimPlayer player, MatchState state)
        {
            float pressure = 0f;
            if (player == null || state == null) return 0f;
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            if (opponents == null) return 0f;

            foreach(var opponent in opponents) {
                if (opponent == null || opponent.IsSuspended()) continue;
                float dist = Vector2.Distance(player.Position, opponent.Position);
                if (dist < ActionResolverConstants.MAX_PRESSURE_DIST) {
                    // Pressure increases more sharply when very close
                    pressure += Mathf.Pow(1.0f - (dist / ActionResolverConstants.MAX_PRESSURE_DIST), 2);
                }
            }
            return Mathf.Clamp01(pressure); // Max pressure = 1.0
        }

        /// <summary>
        /// Converts a player's 2D position to 3D at standard ball height.
        /// </summary>
        public static Vector3 GetPosition3D(SimPlayer player)
        {
             if (player == null) return new Vector3(0, SimConstants.BALL_DEFAULT_HEIGHT, 0);
             // Use SimConstants for ball height
             return new Vector3(player.Position.x, SimConstants.BALL_DEFAULT_HEIGHT, player.Position.y);
        }

        /// <summary>
        /// Checks if a tackle attempt is coming significantly from behind a moving target.
        /// </summary>
        public static bool IsTackleFromBehind(SimPlayer tackler, SimPlayer target) {
             if (tackler == null || target == null) return false;
             if (target.Velocity.sqrMagnitude < 0.5f) return false; // Target needs to be moving

             Vector2 targetMovementDir = target.Velocity.normalized;
             Vector2 approachDir = (target.Position - tackler.Position);
             if(approachDir.sqrMagnitude < ActionResolverConstants.MIN_DISTANCE_CHECK_SQ) return false; // Avoid issues if overlapping
             approachDir.Normalize();

             // Angle between approach vector and *opposite* of target movement
             float angle = Vector2.Angle(approachDir, -targetMovementDir);
             return angle < 75f; // Considered 'behind' if approach is within 75 degrees of the opposite direction
         }

         /// <summary>
         /// Calculates the closing speed between two players along the axis connecting them.
         /// Positive value means they are getting closer.
         /// </summary>
         public static float CalculateClosingSpeed(SimPlayer playerA, SimPlayer playerB) {
              if (playerA == null || playerB == null) return 0f;
              Vector2 relativeVelocity = playerA.Velocity - playerB.Velocity;
              Vector2 axisToTarget = (playerB.Position - playerA.Position);
              if (axisToTarget.sqrMagnitude < ActionResolverConstants.MIN_DISTANCE_CHECK_SQ) return 0f;

              // Project relative velocity onto the *opposite* of the axis connecting them
              return Vector2.Dot(relativeVelocity, -axisToTarget.normalized);
         }

         /// <summary>
         /// Determines if the target player is currently in a clear scoring chance situation.
         /// </summary>
         public static bool IsClearScoringChance(SimPlayer target, MatchState state) {
             // Assumes _geometry is accessible via state or injected if needed, using constants for now
             if (target == null || state == null || !target.HasBall) return false;

             Vector2 opponentGoal = (target.TeamSimId == 0)
                 ? new Vector2(ActionResolverConstants.PITCH_LENGTH, ActionResolverConstants.PITCH_CENTER_Y) // Use constants
                 : new Vector2(0f, ActionResolverConstants.PITCH_CENTER_Y);
             float distToGoal = Vector2.Distance(target.Position, opponentGoal);

             // Check 1: Within scoring range? (e.g., inside 12m)
             if (distToGoal > ActionResolverConstants.FREE_THROW_LINE_RADIUS + 3f) return false;

             // Check 2: Reasonable angle/central position?
             float maxAngleOffset = Mathf.Lerp(6f, 9f, Mathf.Clamp01(distToGoal / (ActionResolverConstants.FREE_THROW_LINE_RADIUS + 3f)));
             if (Mathf.Abs(target.Position.y - ActionResolverConstants.PITCH_CENTER_Y) > maxAngleOffset) return false;

             // Check 3: Number of defenders significantly obstructing the path
             int defendersBlocking = 0;
             var opponents = state.GetOpposingTeamOnCourt(target.TeamSimId);
             if (opponents == null) return true; // If no opponents, it's clear

             Vector2 targetToGoalVec = opponentGoal - target.Position;
             if (targetToGoalVec.sqrMagnitude < ActionResolverConstants.MIN_DISTANCE_CHECK_SQ) return false;

             foreach(var opp in opponents) {
                 if (opp == null || opp.IsGoalkeeper() || opp.IsSuspended()) continue;
                 Vector2 targetToOppVec = opp.Position - target.Position;
                 float oppDistToGoal = Vector2.Distance(opp.Position, opponentGoal);

                 float dot = Vector2.Dot(targetToOppVec.normalized, targetToGoalVec.normalized);
                 if (dot > 0.5f && oppDistToGoal < distToGoal * 1.1f)
                 {
                       // Use SimulationUtils for distance to line
                       float distToLine = Simulation.Utils.SimulationUtils.CalculateDistanceToLine(opp.Position, target.Position, opponentGoal);
                       if (distToLine < 2.5f) // Wider cone check
                       {
                            defendersBlocking++;
                       }
                 }
             }
             return defendersBlocking <= 1; // Clear chance if 0 or 1 field defender is potentially obstructing
         }
    }
}