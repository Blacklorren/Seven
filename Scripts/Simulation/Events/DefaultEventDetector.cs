using UnityEngine;
using HandballManager.Simulation.Interfaces;
using HandballManager.Simulation.Common;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Core; // For ActionResult and SimConstants
using System;
using System.Linq;

namespace HandballManager.Simulation.Events
{
    public class DefaultEventDetector : IEventDetector
    {
        private readonly IGeometryProvider _geometry;

        // Constants moved from MatchSimulator or SimConstants
        private const float INTERCEPTION_RADIUS = MatchSimulator.INTERCEPTION_RADIUS; // Keep ref for now
        private const float BLOCK_RADIUS = MatchSimulator.BLOCK_RADIUS;
        private const float SAVE_REACH_BUFFER = 0.5f; // Adjusted radius buffer
        private const float LOOSE_BALL_PICKUP_RADIUS = MatchSimulator.LOOSE_BALL_PICKUP_RADIUS;
        private const float LOOSE_BALL_PICKUP_RADIUS_SQ = LOOSE_BALL_PICKUP_RADIUS * LOOSE_BALL_PICKUP_RADIUS;
        private const float OOB_RESTART_BUFFER = 0.1f;

        public DefaultEventDetector(IGeometryProvider geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        public void CheckReactiveEvents(MatchState state, ActionResolver actionResolver, IMatchEventHandler eventHandler)
        {
            // --- Logic copied from MatchSimulator ---
            if (state == null || state.CurrentPhase == GamePhase.Finished) return;
            CheckForInterceptions(state, actionResolver, eventHandler); if (state.CurrentPhase == GamePhase.Finished) return;
            CheckForBlocks(state, actionResolver, eventHandler); if (state.CurrentPhase == GamePhase.Finished) return;
            CheckForSaves(state, actionResolver, eventHandler); if (state.CurrentPhase == GamePhase.Finished) return;
            CheckForLooseBallPickup(state, eventHandler);
        }

        public void CheckPassiveEvents(MatchState state, IMatchEventHandler eventHandler)
        {
            // --- Logic copied from MatchSimulator ---
             if (state == null || state.Ball == null || eventHandler == null || state.CurrentPhase == GamePhase.Finished) return;
             try {
                 if (!CheckGoalLineCrossing(state, eventHandler)) {
                      CheckSideLineCrossing(state, eventHandler);
                 }
             } catch (Exception ex) { Debug.LogError($"Error checking passive events: {ex.Message}"); }
        }

        // --- Private Event Check Methods (Copied & Adapted) ---

        private void CheckForInterceptions(MatchState state, ActionResolver actionResolver, IMatchEventHandler eventHandler)
        {
             // --- Logic copied from MatchSimulator ---
             if (state?.Ball?.Passer == null || !state.Ball.IsInFlight || state.Ball.IntendedTarget == null) return;

             var potentialInterceptors = state.GetOpposingTeamOnCourt(state.Ball.Passer.TeamSimId)?.ToList();
             if (potentialInterceptors == null) return;

             foreach (var defender in potentialInterceptors) {
                 if (defender == null || defender.IsSuspended()) continue;
                 try {
                     // Use injected ActionResolver
                     float interceptChance = actionResolver.CalculateInterceptionChance(defender, state.Ball, state);
                     if (interceptChance > 0f && state.RandomGenerator.NextDouble() < interceptChance) {
                         ActionResult result = new ActionResult { Outcome = ActionResultOutcome.Intercepted, PrimaryPlayer = defender, SecondaryPlayer = state.Ball.Passer, ImpactPosition = defender.Position };
                         eventHandler.HandleActionResult(result, state); // Use injected handler
                         return;
                     }
                      if (defender.CurrentAction == PlayerAction.AttemptingIntercept) {
                          eventHandler.ResetPlayerActionState(defender, ActionResultOutcome.Failure); // Use handler
                     }
                 } catch (Exception ex) { Debug.LogError($"Error checking interception for defender {defender.GetPlayerId()}: {ex.Message}"); }
             }
        }

        private void CheckForBlocks(MatchState state, ActionResolver actionResolver, IMatchEventHandler eventHandler)
        {
            // --- Logic copied from MatchSimulator ---
            if (state?.Ball?.LastShooter == null || !state.Ball.IsInFlight) return;

            var potentialBlockers = state.GetOpposingTeamOnCourt(state.Ball.LastShooter.TeamSimId)?.ToList();
            if (potentialBlockers == null) return;

            // Use geometry provider for goal center
            Vector2 targetGoal = _geometry.GetOpponentGoalCenter(state.Ball.LastShooter.TeamSimId);
            Vector2 ballPos2D = CoordinateUtils.To2DGround(state.Ball.Position);

            foreach (var defender in potentialBlockers) {
                if (defender == null || defender.IsSuspended() || defender.IsGoalkeeper()) continue;
                try {
                    float distToBall = Vector2.Distance(defender.Position, ballPos2D);
                    if (distToBall < BLOCK_RADIUS * 1.5f) {
                        Vector2 shooterPos = state.Ball.LastShooter.Position;
                        float distToLine = SimulationUtils.CalculateDistanceToLine(defender.Position, shooterPos, targetGoal);

                        if (distToLine < BLOCK_RADIUS) {
                            // Simplified block chance calculation (ActionResolver doesn't have one currently)
                             float blockChance = 0.2f * Mathf.Lerp(0.5f, 1.5f, (defender.BaseData?.Blocking ?? 50f) / 75f)
                                                  * Mathf.Lerp(0.8f, 1.2f, (defender.BaseData?.Anticipation ?? 50f) / 100f)
                                                  * (1.0f - Mathf.Clamp01(distToLine / BLOCK_RADIUS));

                            if (state.RandomGenerator.NextDouble() < Mathf.Clamp01(blockChance)) {
                                ActionResult result = new ActionResult { Outcome = ActionResultOutcome.Blocked, PrimaryPlayer = defender, SecondaryPlayer = state.Ball.LastShooter, ImpactPosition = defender.Position };
                                eventHandler.HandleActionResult(result, state); // Use handler
                                return;
                            }
                        }
                    }
                } catch (Exception ex) { Debug.LogError($"Error checking block for defender {defender.GetPlayerId()}: {ex.Message}"); }
            }
        }

        private void CheckForSaves(MatchState state, ActionResolver actionResolver, IMatchEventHandler eventHandler)
        {
             // --- Logic copied from MatchSimulator ---
             // Assumes ball physics are calculated elsewhere (e.g., IBallPhysicsCalculator injected if needed)
             if (state?.Ball?.LastShooter == null || !state.Ball.IsInFlight) return;
             int defendingTeamSimId = 1 - state.Ball.LastShooter.TeamSimId;
             SimPlayer gk = state.GetGoalkeeper(defendingTeamSimId);
             if (gk == null || gk.IsSuspended()) return;

             try {
                 float goalLineX = (defendingTeamSimId == 0) ? 0f : _geometry.PitchLength;
                 Vector3 ballPos3D = state.Ball.Position;
                 Vector3 ballVel3D = state.Ball.Velocity;
                 float nextPosX = ballPos3D.x + ballVel3D.x * SimConstants.FLOAT_EPSILON * 10f; // Use small time step like 0.01f or TIME_STEP_SECONDS? Use fixed small lookahead

                 bool headingTowardsGoalPlane =
                     (defendingTeamSimId == 0 && ballVel3D.x < -0.1f && nextPosX <= goalLineX + 1f) ||
                     (defendingTeamSimId == 1 && ballVel3D.x > 0.1f && nextPosX >= goalLineX - 1f);

                 if (!headingTowardsGoalPlane) return;

                 // Simplified save chance calculation (ActionResolver doesn't have one currently)
                 // Need ball physics calculator if predicting impact accurately
                 // Vector3 predictedImpact3D = _ballPhysics.EstimateBallGoalLineImpact3D(state.Ball, defendingTeamSimId); // Assumes injected physics
                 Vector3 predictedImpact3D = ballPos3D + ballVel3D * 0.1f; // Simple forward prediction

                 Vector3 gkPos3D = CoordinateUtils.To3DGround(gk.Position, _geometry.Center.y);
                 float distanceToImpact = Vector3.Distance(gkPos3D, predictedImpact3D);
                 float ballSpeed = Mathf.Max(1f, ballVel3D.magnitude);
                 float timeToImpact = (ballSpeed > 0.1f) ? distanceToImpact / ballSpeed : 10f;
                 float agilityFactor = Mathf.Lerp(0.8f, 1.2f, (gk.BaseData?.Agility ?? 50f) / 100f);
                 float reachDistance = gk.EffectiveSpeed * timeToImpact * agilityFactor;

                 if (distanceToImpact < reachDistance + SimConstants.BALL_RADIUS + SAVE_REACH_BUFFER) {
                     float saveProb = 0.6f;
                     saveProb *= Mathf.Lerp(0.7f, 1.3f, (gk.BaseData?.Reflexes ?? 50f) / 80f);
                     saveProb *= Mathf.Lerp(0.8f, 1.2f, (gk.BaseData?.Handling ?? 50f) / 100f);
                     saveProb *= Mathf.Lerp(0.9f, 1.1f, (gk.BaseData?.PositioningGK ?? 50f) / 100f);
                     if(state.Ball.LastShooter?.BaseData != null) {
                          saveProb *= Mathf.Lerp(1.1f, 0.9f, state.Ball.LastShooter.BaseData.ShootingPower/100f);
                          saveProb *= Mathf.Lerp(1.1f, 0.8f, state.Ball.LastShooter.BaseData.ShootingAccuracy/100f);
                     }

                     if (state.RandomGenerator.NextDouble() < Mathf.Clamp01(saveProb)) {
                         ActionResult result = new ActionResult { Outcome = ActionResultOutcome.Saved, PrimaryPlayer = gk, SecondaryPlayer = state.Ball.LastShooter, ImpactPosition = CoordinateUtils.To2DGround(predictedImpact3D) };
                         eventHandler.HandleActionResult(result, state); // Use handler
                         return;
                     }
                 }
             } catch (Exception ex) { Debug.LogError($"Error checking save for GK {gk.GetPlayerId()}: {ex.Message}"); }
        }

        private void CheckForLooseBallPickup(MatchState state, IMatchEventHandler eventHandler)
        {
            // --- Logic copied from MatchSimulator ---
            if (state?.Ball == null || !state.Ball.IsLoose) return;
            SimPlayer potentialPicker = null; float minPickDistanceSq = LOOSE_BALL_PICKUP_RADIUS_SQ;
            var players = state.PlayersOnCourt?.ToList();
            if (players == null) return;

            Vector2 ballPos2D = CoordinateUtils.To2DGround(state.Ball.Position);

            try {
                 // Prioritize players actively chasing
                 potentialPicker = players
                    .Where(p => p != null && !p.IsSuspended() && p.CurrentAction == PlayerAction.ChasingBall)
                    .OrderBy(p => (p.Position - ballPos2D).sqrMagnitude)
                    .FirstOrDefault(p => (p.Position - ballPos2D).sqrMagnitude < minPickDistanceSq);

                 // If no chaser is close enough, check others
                 if (potentialPicker == null)
                 {
                    potentialPicker = players
                        .Where(p => p != null && !p.IsSuspended() && p.CurrentAction != PlayerAction.Fallen && p.CurrentAction != PlayerAction.ChasingBall)
                        .OrderBy(p => (p.Position - ballPos2D).sqrMagnitude)
                        .FirstOrDefault(p => (p.Position - ballPos2D).sqrMagnitude < minPickDistanceSq && (p.BaseData?.Technique ?? 0) > 30 && (p.BaseData?.Anticipation ?? 0) > 30);
                 }


                if (potentialPicker != null) {
                    ActionResult pickupResult = new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = potentialPicker, ImpactPosition = ballPos2D, Reason = "Picked up loose ball" };
                    eventHandler.HandlePossessionChange(state, potentialPicker.TeamSimId); // Use handler
                    state.Ball.SetPossession(potentialPicker);
                    eventHandler.ResetPlayerActionState(potentialPicker, pickupResult.Outcome); // Use handler
                }
            } catch (Exception ex) { Debug.LogError($"Error checking loose ball pickup: {ex.Message}"); }
        }

         // --- Passive Event Check Methods ---

         private bool CheckGoalLineCrossing(MatchState state, IMatchEventHandler eventHandler)
         {
            // --- Logic copied from MatchSimulator ---
             Vector3 currentBallPos = state.Ball.Position;
             Vector3 prevBallPos = currentBallPos - state.Ball.Velocity * SimConstants.FLOAT_EPSILON * 10f; // Small lookbehind

             var crossInfo = DidCrossGoalLinePlane(prevBallPos, currentBallPos);
             if (!crossInfo.DidCross) return false;

             if (Mathf.Abs(currentBallPos.x - prevBallPos.x) < SimConstants.FLOAT_EPSILON) return false;
             float intersectT = Mathf.Clamp01((crossInfo.GoalLineX - prevBallPos.x) / (currentBallPos.x - prevBallPos.x));
             Vector3 intersectPoint = prevBallPos + (currentBallPos - prevBallPos) * intersectT;

             if (IsWithinGoalBounds(intersectPoint, crossInfo.GoalLineX))
             {
                 if (IsValidGoalAttempt(state.Ball, crossInfo.GoalLineX == 0f))
                 {
                     ActionResult goalResult = new ActionResult { Outcome = ActionResultOutcome.Goal, PrimaryPlayer = state.Ball.LastShooter, ImpactPosition = CoordinateUtils.To2DGround(intersectPoint) };
                     eventHandler.HandleActionResult(goalResult, state); // Use handler
                     return true;
                 }
             }

              ActionResult oobResult = new ActionResult { Outcome = ActionResultOutcome.OutOfBounds, ImpactPosition = CoordinateUtils.To2DGround(intersectPoint), PrimaryPlayer = state.Ball.LastTouchedByPlayer };
              eventHandler.HandleOutOfBounds(oobResult, state, intersectPoint); // Use handler
             return true;
         }

         private bool CheckSideLineCrossing(MatchState state, IMatchEventHandler eventHandler)
         {
             // --- Logic copied from MatchSimulator ---
             Vector3 currentBallPos = state.Ball.Position;
             Vector3 prevBallPos = currentBallPos - state.Ball.Velocity * SimConstants.FLOAT_EPSILON * 10f; // Small lookbehind

             bool crossedBottomLine = (prevBallPos.z >= 0f && currentBallPos.z < 0f) || (prevBallPos.z < 0f && currentBallPos.z >= 0f);
             bool crossedTopLine = (prevBallPos.z <= _geometry.PitchWidth && currentBallPos.z > _geometry.PitchWidth) ||
                                   (prevBallPos.z > _geometry.PitchWidth && currentBallPos.z <= _geometry.PitchWidth);

             if (crossedBottomLine || crossedTopLine) {
                 if (state.Ball.IsLoose || state.Ball.IsInFlight) {
                     if (Mathf.Abs(currentBallPos.z - prevBallPos.z) < SimConstants.FLOAT_EPSILON) return false;
                     float sidelineZ = crossedBottomLine ? 0f : _geometry.PitchWidth;
                     float intersectT = Mathf.Clamp01((sidelineZ - prevBallPos.z) / (currentBallPos.z - prevBallPos.z));
                     Vector3 intersectPoint = prevBallPos + (currentBallPos - prevBallPos) * intersectT;

                     ActionResult oobResult = new ActionResult {
                         Outcome = ActionResultOutcome.OutOfBounds,
                         ImpactPosition = CoordinateUtils.To2DGround(intersectPoint),
                         PrimaryPlayer = state.Ball.LastTouchedByPlayer
                     };
                     eventHandler.HandleOutOfBounds(oobResult, state, intersectPoint); // Use handler
                     return true;
                 }
             }
             return false;
         }

         // --- Helper Methods ---

         private struct GoalLineCrossInfo { public bool DidCross; public float GoalLineX; }

         private GoalLineCrossInfo DidCrossGoalLinePlane(Vector3 prevPos, Vector3 currentPos)
         {
            // --- Logic copied from MatchSimulator ---
             if ((prevPos.x >= 0f && currentPos.x < 0f) || (prevPos.x < 0f && currentPos.x >= 0f)) {
                 return new GoalLineCrossInfo { DidCross = true, GoalLineX = 0f };
             }
             if ((prevPos.x <= _geometry.PitchLength && currentPos.x > _geometry.PitchLength) || (prevPos.x > _geometry.PitchLength && currentPos.x <= _geometry.PitchLength)) {
                 return new GoalLineCrossInfo { DidCross = true, GoalLineX = _geometry.PitchLength };
             }
             return new GoalLineCrossInfo { DidCross = false, GoalLineX = 0f };
         }

         private bool IsWithinGoalBounds(Vector3 position, float goalLineX)
         {
            // --- Logic copied from MatchSimulator ---
             Vector3 goalCenter = Mathf.Approximately(goalLineX, 0f) ? _geometry.HomeGoalCenter3D : _geometry.AwayGoalCenter3D;
             bool withinWidth = Mathf.Abs(position.z - goalCenter.z) <= _geometry.GoalWidth / 2f;
             bool underCrossbar = position.y <= _geometry.GoalHeight + SimConstants.BALL_RADIUS;
             bool aboveGround = position.y >= SimConstants.BALL_RADIUS;
             return withinWidth && underCrossbar && aboveGround;
         }

         private bool IsValidGoalAttempt(SimBall ball, bool crossingHomeLine)
         {
            // --- Logic copied from MatchSimulator ---
             return ball.IsInFlight && ball.LastShooter != null &&
                    ((crossingHomeLine && ball.LastShooter.TeamSimId == 1) ||
                     (!crossingHomeLine && ball.LastShooter.TeamSimId == 0));
         }
    }
}