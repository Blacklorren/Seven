using UnityEngine;
using HandballManager.Simulation.MatchData;
using System.Linq;
using System.Collections.Generic; // Required for List
using HandballManager.Core; // For PlayerPosition enum
using System; // For Math

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Controls the decisions and actions of AI-controlled players during a match simulation.
    /// Determines player movement targets, actions (pass, shoot, tackle), and reactions based on game state.
    /// </summary>
    public class PlayerAIController
    {
        // --- AI Decision Constants ---

        // General Action/State
        private const float DIST_TO_TARGET_IDLE_THRESHOLD = 0.5f; // Distance within target to consider arrived/idle
        private const float DIST_TO_TARGET_MOVE_TRIGGER_THRESHOLD = 0.6f; // Min distance difference to trigger a move command
        private const float MIN_DISTANCE_CHECK_SQ = 0.01f; // Squared distance threshold for near-zero checks

        // Pass Evaluation
        private const float PASS_EVAL_MAX_DIST = 18f;
        private const float PASS_EVAL_MIN_DIST = 1.0f; // Minimum distance for pass evaluation
        private const float PASS_EVAL_OPENNESS_WEIGHT = 0.5f;
        private const float PASS_EVAL_DISTANCE_WEIGHT = 0.2f;
        private const float PASS_EVAL_INTERCEPTION_RISK_WEIGHT = 0.3f;
        private const float PASS_ATTEMPT_THRESHOLD = 0.40f; // Base threshold to attempt a pass
        private const float PASS_FORWARD_BONUS_FACTOR = 0.1f; // Slight bonus for forward passes
        private const float GK_PASS_SAFETY_THRESHOLD_MOD = 0.7f; // Multiplier for GK pass threshold (lower = more cautious)
        private const float PASS_INTERCEPTION_RISK_SAFE_THRESHOLD = 0.15f; // Risk below this is considered 'safe'
        private const float PASS_OPENNESS_SAFE_THRESHOLD = 3.0f; // Min distance to defender for 'safe'

        // Shooting Evaluation
        private const float MIN_SHOOTING_DIST = 5f;
        private const float MAX_SHOOTING_DIST = 18f;
        private const float MIN_ACCEPTABLE_ANGLE_DEGREES = 10f;
        private const float BLOCKER_CHECK_DIST = 4f;
        private const float BLOCKER_CHECK_WIDTH = 1.0f;
        private const float SHOOT_DESIRABILITY_BASE_THRESHOLD = 0.5f;
        private const float FATIGUE_SHOOT_PENALTY = 0.5f; // Max penalty at 0 stamina
        private const float LATE_GAME_MINUTES_THRESHOLD = 5f; // How many minutes left trigger late game logic
        private const int DESPERATION_SCORE_DIFF = -3; // Score difference threshold for desperation
        private const float SHOOT_GK_COVER_PENALTY_FACTOR = 0.8f;
        private const float SHOOT_ATTRIBUTE_BENCHMARK = 75f;
        private const float SHOOT_ATTRIBUTE_SCALE_MIN = 0.7f;
        private const float SHOOT_ATTRIBUTE_SCALE_MAX = 1.4f;
        private const float SHOOT_POS_MOD_BACK = 1.1f;
        private const float SHOOT_POS_MOD_WING = 0.9f;
        private const float SHOOT_LATE_DESPERATION_MOD = 1.4f;
        private const float SHOOT_LATE_COMFORTABLE_LEAD_MOD = 0.7f;
        private const float SHOOT_ONE_BLOCKER_PENALTY = 0.6f;
        private const float SHOOT_DECISION_MAKING_THRESHOLD_MIN_MOD = 1.2f; // Higher DM makes shooting less likely unless clear
        private const float SHOOT_DECISION_MAKING_THRESHOLD_MAX_MOD = 0.8f; // Lower DM makes shooting more likely

        // Interception Constants
        private const float INTERCEPT_DECISION_THRESHOLD = 0.3f;
        private const float INTERCEPT_HIGH_CHANCE_THRESHOLD = 0.6f;
        private const float INTERCEPT_ESTIMATE_PROJECTION_FACTOR = 0.3f;
        private const float INTERCEPT_ESTIMATE_PROJECTION_MIN_TIME = 0.05f;
        private const float INTERCEPT_ESTIMATE_PROJECTION_MAX_TIME = 0.4f;
        private const float INTERCEPT_PHYSICALLY_POSSIBLE_TIME_FACTOR = 1.3f;
        private const float INTERCEPT_MIN_POSSIBLE_CHANCE = 0.05f;
        private const float INTERCEPT_CHASE_RANGE_MULTIPLIER = 3.0f; // Multiplier for loose ball pickup radius for chasing

        // Tackle Decision Constants
        private const float MAX_TACKLE_INITIATE_RANGE = MatchSimulator.TACKLE_RADIUS * 1.3f;
        private const float MIN_TACKLE_SUCCESS_CHANCE_ESTIMATE = 0.20f;
        private const float MAX_TACKLE_FOUL_CHANCE_ESTIMATE = 0.60f;
        private const float TACKLE_ORIENTATION_THRESHOLD_DEG = 100f;
        private const float TACKLE_TARGET_SPEED_PENALTY = 0.1f;
        private const float TACKLE_NEAR_GOAL_BONUS = 0.15f;
        private const float TACKLE_DESPERATION_BONUS = 0.2f;
        private const float TACKLE_DECISION_MAKING_FACTOR = 0.3f;
        private const float TACKLE_AGGRESSION_FACTOR = 0.2f;
        private const float TACKLE_DESIRABILITY_BASE_THRESHOLD = 0.0f;
        private const float TACKLE_SUCCESS_DESIRABILITY_WEIGHT = 1.5f;
        private const float TACKLE_FOUL_DESIRABILITY_WEIGHT = 1.8f;
        private const float TACKLE_NEAR_GOAL_THRESHOLD_DIST_FACTOR = 4f;

        // Marking / Blocking
        private const float BLOCK_DISTANCE_THRESHOLD_FACTOR = 5.0f; // Max distance factor from block radius to consider blocking
        private const float BLOCK_ALIGNMENT_THRESHOLD = 0.7f; // Min dot product for alignment check
        private const float BLOCK_POSITION_MIN_DIST_FROM_SHOOTER = 2f;
        private const float BLOCK_POSITION_MAX_DIST_FROM_SHOOTER = 4f;
        private const float BLOCK_POSITION_LATERAL_OFFSET_RANGE = 0.5f; // +/- Y offset
        private const float MARKING_INTERCEPT_BREAK_DIST = 2.0f;
        private const float GOALSIDE_MARKING_FACTOR = 0.9f;
        private const float GOALSIDE_MARKING_LERP_DIST = 10f;
        private const float GOALSIDE_MARKING_LERP_BASE = 0.6f;
        private const float GOALSIDE_MARKING_LERP_CLOSE_FACTOR = 0.4f;
        private const float MARKING_THREAT_DISTANCE_SCALE = 0.6f; // Pitch length factor for threat calculation
        private const float MARKING_THREAT_WIDTH_SCALE = 0.4f; // Pitch width factor for threat calculation
        private const float MARKING_THREAT_WEIGHT = 0.6f; // How much threat influences marking score
        private const float MARKING_DISTANCE_WEIGHT_CURRENT = 0.3f; // Weight for distance from current pos
        private const float MARKING_DISTANCE_WEIGHT_TACTICAL = 0.7f; // Weight for distance from tactical pos

        // Action Timers
        private const float SHOT_PREP_TIME_BASE = 0.6f;
        private const float SHOT_PREP_TIME_RANDOM_FACTOR = 0.3f;
        private const float PASS_PREP_TIME_BASE = 0.4f;
        private const float PASS_PREP_TIME_RANDOM_FACTOR = 0.2f;
        private const float GK_PASS_PREP_TIME_BASE = 0.6f;
        private const float GK_PASS_PREP_TIME_RANDOM_FACTOR = 0.2f;
        private const float TACKLE_PREP_TIME = 0.3f;


        private readonly TacticPositioner _positioner;
        private readonly ActionResolver _actionResolver;

        public PlayerAIController(TacticPositioner positioner, ActionResolver actionResolver)
        {
            _positioner = positioner ?? throw new ArgumentNullException(nameof(positioner));
            _actionResolver = actionResolver ?? throw new ArgumentNullException(nameof(actionResolver));
        }

        /// <summary>
        /// Updates decisions for all players currently on the court.
        /// Iterates over a copy of the list for safety, as decisions might indirectly lead to list changes (e.g., suspension).
        /// </summary>
        public void UpdatePlayerDecisions(MatchState state)
        {
             if (state == null) return; // Safety check for state
             // Using ToList() for safety as DecidePlayerAction can lead to state changes handled by
             // EventHandler which might modify the underlying PlayersOnCourt list (e.g., suspension removes player).
             // While less common during AI update than event handling, it's safer. Consider removing if performance critical AND proven safe.
             foreach (var player in state.PlayersOnCourt.ToList())
             {
                 if (player == null) continue; // Safety check for player in list
                 try {
                    DecidePlayerAction(player, state);
                 } catch (Exception ex) {
                      // Log error specific to this player's decision making
                      Debug.LogError($"[PlayerAIController] Error deciding action for player {player.GetPlayerId()}: {ex.Message}\n{ex.StackTrace}");
                      // Attempt to reset player to a safe state
                      if(player != null) player.CurrentAction = PlayerAction.Idle;
                 }
             }
        }

        /// <summary>
        /// Core decision logic router for a single player based on game state.
        /// Determines the player's primary action and target for the current simulation step.
        /// </summary>
        private void DecidePlayerAction(SimPlayer player, MatchState state)
        {
             // --- Pre-Checks & State Persistence ---
             if (player.IsSuspended()) { // Handle suspended state first
                 player.CurrentAction = PlayerAction.Suspended;
                 player.TargetPosition = player.Position; // Stay put (off-court)
                 player.TargetPlayer = null;
                 return;
             }
             // Don't interrupt actions with timers (pass/shot/tackle windup)
             if (player.ActionTimer > 0) {
                 return; // Action is already in progress
             }
             // Persist receiving state if still relevant
             if (player.CurrentAction == PlayerAction.ReceivingPass && state.Ball.IsInFlight && state.Ball.IntendedTarget == player) {
                 player.TargetPosition = EstimatePassInterceptPoint(state.Ball, player); // Update intercept target
                 return; // Continue receiving
             }
             // Persist intercept attempt state if still relevant
             if (player.CurrentAction == PlayerAction.AttemptingIntercept && state.Ball.IsInFlight) {
                 player.TargetPosition = EstimatePassInterceptPoint(state.Ball, player); // Update intercept target
                 return; // Continue intercept attempt
             }

             // Clear previous player target unless explicitly marking or tackling
             if (player.CurrentAction != PlayerAction.MarkingPlayer && player.CurrentAction != PlayerAction.AttemptingTackle) {
                 player.TargetPlayer = null;
             }

             // --- Determine Basic Game Context ---
             bool isOwnTeamPossession = player.TeamSimId == state.PossessionTeamId && state.PossessionTeamId != -1;
             bool hasBall = player.HasBall;
             bool ballIsLoose = state.Ball.IsLoose;
             bool ballInFlight = state.Ball.IsInFlight;

             // --- Action Decision Branching ---
             if (player.IsGoalkeeper()) {
                 DecideGoalkeeperAction(player, state, isOwnTeamPossession);
             }
             else // Field Player Decisions
             {
                 // 1. Immediate Reactions (Highest Priority)
                 bool reactionTaken = TryReactToBallState(player, state, ballIsLoose, ballInFlight, isOwnTeamPossession);
                 if (reactionTaken) return; // Reaction handled, decision made for this step

                 // 2. Standard Phase Actions (Offense / Defense)
                 if (isOwnTeamPossession) {
                     DecideOffensiveActions(player, state, hasBall);
                 } else {
                     DecideDefensiveActions(player, state, ballIsLoose);
                 }
             }

             // --- Fallback Positioning (If no specific action decided) ---
             // If player is Idle or has reached target, find new tactical position
             bool needsRepositioning = player.CurrentAction == PlayerAction.Idle ||
                                     (IsMovementAction(player.CurrentAction) && Vector2.Distance(player.Position, player.TargetPosition) < DIST_TO_TARGET_IDLE_THRESHOLD) ||
                                     (player.CurrentAction == PlayerAction.MarkingPlayer && player.TargetPlayer == null); // Lost mark target

             if (needsRepositioning) {
                 SetPlayerToMoveToTacticalPosition(player, state);
             }
        }

        /// <summary>Checks for and handles immediate reactions like chasing loose balls or receiving passes.</summary>
        /// <returns>True if a reaction was triggered, false otherwise.</returns>
        private bool TryReactToBallState(SimPlayer player, MatchState state, bool ballIsLoose, bool ballInFlight, bool isOwnTeamPossession)
        {
             // React to Loose Ball nearby
             if (ballIsLoose && Vector2.Distance(player.Position, state.Ball.Position) < MatchSimulator.LOOSE_BALL_PICKUP_RADIUS * INTERCEPT_CHASE_RANGE_MULTIPLIER)
             {
                 player.CurrentAction = PlayerAction.ChasingBall;
                 player.TargetPosition = state.Ball.Position;
                 return true; // Reaction: Chase loose ball
             }

             // React to Incoming Pass
             if (ballInFlight && IsPassIncoming(player, state))
             {
                 player.CurrentAction = PlayerAction.ReceivingPass;
                 player.TargetPosition = EstimatePassInterceptPoint(state.Ball, player);
                 return true; // Reaction: Receive pass
             }

             // React to Potential Interception Opportunity
             if (ballInFlight && !isOwnTeamPossession && CanIntercept(player, state, out float interceptChance) && interceptChance > INTERCEPT_DECISION_THRESHOLD)
             {
                 // Decide whether to commit to interception based on current state and chance
                 bool shouldAttemptIntercept = player.CurrentAction != PlayerAction.MarkingPlayer ||
                                             Vector2.Distance(player.Position, player.TargetPosition) > MARKING_INTERCEPT_BREAK_DIST ||
                                             interceptChance > INTERCEPT_HIGH_CHANCE_THRESHOLD;
                 if (shouldAttemptIntercept)
                 {
                     player.CurrentAction = PlayerAction.AttemptingIntercept;
                     player.TargetPosition = EstimatePassInterceptPoint(state.Ball, player);
                     return true; // Reaction: Attempt Intercept
                 }
                 // Else: Stay marking (handled later)
             }

             return false; // No immediate reaction triggered
        }

        /// <summary>Is the player currently in a state primarily focused on movement?</summary>
        private bool IsMovementAction(PlayerAction action)
        {
            return action == PlayerAction.MovingToPosition ||
                   action == PlayerAction.MovingWithBall ||
                   action == PlayerAction.ChasingBall ||
                   action == PlayerAction.MarkingPlayer || // Includes movement
                   action == PlayerAction.ReceivingPass ||
                   action == PlayerAction.AttemptingIntercept ||
                   action == PlayerAction.GoalkeeperPositioning;
        }

        /// <summary>Commands the player to move towards their calculated tactical position.</summary>
        private void SetPlayerToMoveToTacticalPosition(SimPlayer player, MatchState state)
        {
             if (player == null || state == null || _positioner == null) return;

             Vector2 tacticalPos = _positioner.GetPlayerTargetPosition(player, state);
             if (Vector2.Distance(player.Position, tacticalPos) > DIST_TO_TARGET_MOVE_TRIGGER_THRESHOLD) {
                 player.TargetPosition = tacticalPos;
                 player.CurrentAction = PlayerAction.MovingToPosition;
             } else {
                 // Already close enough, set to Idle and stop movement
                 player.CurrentAction = PlayerAction.Idle;
                 player.TargetPosition = player.Position;
                 player.Velocity *= 0.5f; // Dampen velocity
             }
        }


        /// <summary>
        /// Decision logic specific to goalkeepers. Handles saving, positioning, and passing.
        /// </summary>
        private void DecideGoalkeeperAction(SimPlayer gk, MatchState state, bool isOwnTeamPossession)
        {
             if (gk == null || state == null) return;

             Vector2 goalCenter = gk.TeamSimId == 0 ? MatchSimulator.PitchGeometry.HomeGoalCenter : MatchSimulator.PitchGeometry.AwayGoalCenter;
             float goalLineX = goalCenter.x;
             Vector2 defaultDefPosition = goalCenter + Vector2.right * (gk.TeamSimId == 0 ? MatchSimulator.DEF_GK_DEPTH : -MatchSimulator.DEF_GK_DEPTH);

             // --- Action Priority ---
             // 1. Has Ball: Try safe pass
             if (gk.HasBall) {
                 TryGoalkeeperPass(gk, state);
                 return;
             }

             // 2. Ball In Flight (Shot towards own goal): Position to save
             if (state.Ball.IsInFlight && state.Ball.LastShooter != null && state.Ball.LastShooter.TeamSimId != gk.TeamSimId) {
                 PositionGoalkeeperToSave(gk, state, goalLineX, goalCenter);
                 return;
             }

             // 3. Opponent Has Ball (Not Shot): Position defensively relative to ball
             if (!isOwnTeamPossession && state.Ball.Holder != null) {
                 PositionGoalkeeperDefensively(gk, state, defaultDefPosition, goalCenter);
                 return;
             }

             // 4. Default (Own Possession / Ball Loose Far Away): Move to tactical position
             // Let the fallback logic in DecidePlayerAction handle this via SetPlayerToMoveToTacticalPosition
             // Explicitly setting Idle here allows the fallback to take over.
              gk.CurrentAction = PlayerAction.Idle;
        }

        /// <summary>Goalkeeper attempts a safe pass if possible.</summary>
        private void TryGoalkeeperPass(SimPlayer gk, MatchState state)
        {
             var bestPassOption = EvaluatePassOptions(gk, state, true).FirstOrDefault(); // Prioritize safe passes
             if (bestPassOption != null && bestPassOption.Score > PASS_ATTEMPT_THRESHOLD * GK_PASS_SAFETY_THRESHOLD_MOD) {
                 gk.TargetPlayer = bestPassOption.Player;
                 gk.CurrentAction = PlayerAction.PreparingPass;
                 // Use state's random generator
                 gk.ActionTimer = GK_PASS_PREP_TIME_BASE + (float)_state.RandomGenerator.NextDouble() * GK_PASS_PREP_TIME_RANDOM_FACTOR;
                 gk.TargetPosition = gk.Position; // Stay put
             } else {
                 // No safe pass, hold ball
                 gk.CurrentAction = PlayerAction.Idle;
                 gk.TargetPosition = gk.Position;
             }
        }

        /// <summary>Positions the GK reactively to an incoming shot.</summary>
        private void PositionGoalkeeperToSave(SimPlayer gk, MatchState state, float goalLineX, Vector2 goalCenter)
        {
             Vector2 predictedImpactPoint = EstimateBallGoalLineImpact(state.Ball, gk.TeamSimId);
             predictedImpactPoint.x = goalLineX + (gk.TeamSimId == 0 ? 0.1f : -0.1f); // Target slightly off line
             // Clamp Y more tightly to goal width for saving
             predictedImpactPoint.y = Mathf.Clamp(predictedImpactPoint.y, goalCenter.y - MatchSimulator.PitchGeometry.GoalWidth * 0.5f, goalCenter.y + MatchSimulator.PitchGeometry.GoalWidth * 0.5f);
             gk.TargetPosition = predictedImpactPoint;
             gk.CurrentAction = PlayerAction.GoalkeeperPositioning; // Represents moving to save attempt
        }

        /// <summary>Positions the GK defensively based on ball carrier position.</summary>
        private void PositionGoalkeeperDefensively(SimPlayer gk, MatchState state, Vector2 defaultPosition, Vector2 goalCenter)
        {
            // Calculate target position based on angle cutting and GK positioning skill
            Vector2 ballPos = state.Ball.Position;
            Vector2 post1 = goalCenter + Vector2.up * (MatchSimulator.PitchGeometry.GoalWidth / 2f);
            Vector2 post2 = goalCenter + Vector2.down * (MatchSimulator.PitchGeometry.GoalWidth / 2f);
            Vector2 dir1 = (post1 - ballPos).normalized;
            Vector2 dir2 = (post2 - ballPos).normalized;
            Vector2 bisector = (dir1 + dir2).normalized;

            if (bisector.sqrMagnitude < 0.1f || Vector2.Dot(dir1, dir2) < -0.9f) {
                 bisector = Vector2.right * (gk.TeamSimId == 0 ? 1f : -1f); bisector.y = 0f;
            }

            float distanceToBall = Vector2.Distance(ballPos, goalCenter);
            float pushOutFactor = Mathf.Lerp(0.1f, 1.8f, (gk.BaseData?.PositioningGK ?? 50f) / 100f) * Mathf.Clamp01(1f - (distanceToBall / 15f));
            pushOutFactor = Mathf.Clamp(pushOutFactor, 0.05f, 2.5f);

            Vector2 targetPos = defaultPosition + bisector * pushOutFactor;
            targetPos.x = defaultPosition.x; // Keep fixed depth
            targetPos.y = Mathf.Clamp(targetPos.y, goalCenter.y - MatchSimulator.PitchGeometry.GoalWidth * 0.6f, goalCenter.y + MatchSimulator.PitchGeometry.GoalWidth * 0.6f); // Clamp Y

            gk.TargetPosition = targetPos;
            gk.CurrentAction = PlayerAction.GoalkeeperPositioning;
        }


        /// <summary>
        /// Decision logic for field players when their team has possession.
        /// Prioritizes shooting, then passing, then moving with the ball.
        /// </summary>
        private void DecideOffensiveActions(SimPlayer player, MatchState state, bool hasBall)
        {
            if (player == null || state == null) return; // Safety check

            if (hasBall) {
                // Priority 1: Shoot if good opportunity
                if (EvaluateShootingOpportunity(player, state)) {
                    player.CurrentAction = PlayerAction.PreparingShot;
                    player.ActionTimer = SHOT_PREP_TIME_BASE + (float)_state.RandomGenerator.NextDouble() * SHOT_PREP_TIME_RANDOM_FACTOR; // Use constant
                    player.TargetPosition = player.Position; // Stay put
                    return;
                }

                // Priority 2: Pass if good option available
                var bestPassOption = EvaluatePassOptions(player, state, false).FirstOrDefault();
                if (bestPassOption != null && bestPassOption.Score > PASS_ATTEMPT_THRESHOLD) {
                    player.TargetPlayer = bestPassOption.Player;
                    player.CurrentAction = PlayerAction.PreparingPass;
                    player.ActionTimer = PASS_PREP_TIME_BASE + (float)_state.RandomGenerator.NextDouble() * PASS_PREP_TIME_RANDOM_FACTOR; // Use constant
                    player.TargetPosition = player.Position; // Stay put
                    return;
                }

                // Priority 3: Move with ball towards tactical position/open space
                // If no good shot or pass, set player to move. Fallback logic will handle the movement state.
                 player.CurrentAction = PlayerAction.MovingWithBall;
                 // Let fallback logic determine TargetPosition based on tactical needs
                 SetPlayerToMoveToTacticalPosition(player, state); // Explicitly call repositioning

            } else { // Does not have ball
                 // Move to tactical position to support attack / find space
                 // Let the fallback logic in DecidePlayerAction handle this
                 player.CurrentAction = PlayerAction.Idle; // Set to idle so fallback repositions
            }
        }

        /// <summary>
        /// Decision logic for field players when opponent team has possession or ball is loose (far away).
        /// Handles tackling, blocking, and marking decisions.
        /// </summary>
        private void DecideDefensiveActions(SimPlayer player, MatchState state, bool ballIsLoose)
        {
            if (player == null || state == null) return; // Safety check

            // If ball is controlled by opponent
            if (!ballIsLoose && state.Ball.Holder != null && state.Ball.Holder.TeamSimId != player.TeamSimId) {
                SimPlayer opponentWithBall = state.Ball.Holder;
                if (opponentWithBall == null) { // Safety check
                    SetPlayerToMoveToTacticalPosition(player, state); return; // Fallback if holder invalid
                }

                // Priority 1: Attempt Tackle if viable
                if (ShouldAttemptTackle(player, opponentWithBall, state)) {
                    return; // Action set within ShouldAttemptTackle
                }

                // Priority 2: Attempt Block if opponent likely to shoot
                if (ShouldAttemptBlock(player, opponentWithBall, state)) {
                    return; // Action set within ShouldAttemptBlock
                }

                // Priority 3: Mark an appropriate opponent
                if (ShouldMarkOpponent(player, state)) {
                    return; // Action set within ShouldMarkOpponent
                }

                // Fallback: If no specific action, move to tactical defensive position
                 SetPlayerToMoveToTacticalPosition(player, state);

            } else { // Ball is loose far away, or own team has it (but this function implies opponent possession phase)
                 // Default defensive action: Move to tactical position
                 SetPlayerToMoveToTacticalPosition(player, state);
            }
        }

        /// <summary>Decides if a tackle should be attempted and sets player state.</summary>
        /// <returns>True if tackle attempt was initiated, false otherwise.</returns>
        private bool ShouldAttemptTackle(SimPlayer player, SimPlayer opponentWithBall, MatchState state) {
             float distanceToBallHolder = Vector2.Distance(player.Position, opponentWithBall.Position);
             if (distanceToBallHolder < MAX_TACKLE_INITIATE_RANGE && CanTackle(player, opponentWithBall, state)) {
                 player.CurrentAction = PlayerAction.AttemptingTackle;
                 player.TargetPlayer = opponentWithBall;
                 player.ActionTimer = TACKLE_PREP_TIME;
                 player.TargetPosition = player.Position; // Root player
                 return true;
             }
             return false;
        }

        /// <summary>Decides if blocking is the priority and sets player state.</summary>
        /// <returns>True if block attempt was initiated, false otherwise.</returns>
        private bool ShouldAttemptBlock(SimPlayer player, SimPlayer opponentWithBall, MatchState state) {
             if (player.IsGoalkeeper()) return false; // GKs don't block this way

             bool opponentPreparingShot = opponentWithBall.CurrentAction == PlayerAction.PreparingShot;
             float distanceToShooter = Vector2.Distance(player.Position, opponentWithBall.Position);
             Vector2 ownGoal = player.TeamSimId == 0 ? MatchSimulator.PitchGeometry.HomeGoalCenter : MatchSimulator.PitchGeometry.AwayGoalCenter;
             Vector2 opponentToGoalDir = (ownGoal - opponentWithBall.Position).normalized;
             Vector2 opponentToPlayerDir = (player.Position - opponentWithBall.Position).normalized;

             // Conditions: Opponent preparing shot, player close enough, player generally between shooter and goal
             if (opponentPreparingShot && distanceToShooter < MatchSimulator.BLOCK_RADIUS * BLOCK_DISTANCE_THRESHOLD_FACTOR
                 && Vector2.Dot(opponentToPlayerDir, opponentToGoalDir) > BLOCK_ALIGNMENT_THRESHOLD)
             {
                  // Calculate target block position
                  Vector2 blockPos = opponentWithBall.Position + opponentToGoalDir * _state.RandomGenerator.Next((int)BLOCK_POSITION_MIN_DIST_FROM_SHOOTER, (int)BLOCK_POSITION_MAX_DIST_FROM_SHOOTER + 1); // Use state's random
                  // Add deterministic lateral offset based on ID to vary slightly?
                  float lateralOffset = ((player.GetPlayerId() % 2 == 0) ? -1f : 1f) * BLOCK_POSITION_LATERAL_OFFSET_RANGE * 0.5f;
                  blockPos.y += lateralOffset;

                  player.TargetPosition = blockPos;
                  player.CurrentAction = PlayerAction.AttemptingBlock;
                  return true;
             }
             return false;
        }

        /// <summary>Decides if marking is the priority and sets player state.</summary>
        /// <returns>True if marking state was set, false otherwise.</returns>
        private bool ShouldMarkOpponent(SimPlayer player, MatchState state) {
            SimPlayer playerToMark = FindOpponentToMark(player, state);
            if (playerToMark != null) {
                 Vector2 tacticalBasePos = _positioner.GetPlayerTargetPosition(player, state);
                 Vector2 ownGoalCenter = player.TeamSimId == 0 ? MatchSimulator.PitchGeometry.HomeGoalCenter : MatchSimulator.PitchGeometry.AwayGoalCenter;
                 Vector2 vectorFromGoalToMark = playerToMark.Position - ownGoalCenter;
                 Vector2 goalSidePos = ownGoalCenter + vectorFromGoalToMark * GOALSIDE_MARKING_FACTOR;

                 float distToMark = Vector2.Distance(player.Position, playerToMark.Position);
                 float closenessFactor = Mathf.Clamp01(1f - distToMark / GOALSIDE_MARKING_LERP_DIST);

                 player.TargetPosition = Vector2.Lerp(tacticalBasePos, goalSidePos, GOALSIDE_MARKING_LERP_BASE + closenessFactor * GOALSIDE_MARKING_LERP_CLOSE_FACTOR);
                 player.TargetPlayer = playerToMark;
                 player.CurrentAction = PlayerAction.MarkingPlayer;
                 return true;
            }
            return false;
        }

        /// <summary>
        /// Detailed check if a tackle attempt is tactically sound based on probabilities and situation.
        /// </summary>
        /// <returns>True if the AI decides to attempt a tackle, false otherwise.</returns>
        private bool CanTackle(SimPlayer tackler, SimPlayer target, MatchState state)
        {
            // Basic State Check & Null Checks moved to ShouldAttemptTackle caller

            // Orientation Check
            Vector2 tacklerToTargetDir = (target.Position - tackler.Position).normalized;
            if (tacklerToTargetDir.sqrMagnitude < MIN_DISTANCE_CHECK_SQ) return false;

             Vector2 tacklerFacingDir = (tackler.Velocity.sqrMagnitude > 1.0f) ? tackler.Velocity.normalized : tacklerToTargetDir;
             float angleDiff = Vector2.Angle(tacklerFacingDir, tacklerToTargetDir);
             if (angleDiff > TACKLE_ORIENTATION_THRESHOLD_DEG) return false;

            // Estimate Probabilities
            var (estimatedSuccessChance, estimatedFoulChance) = _actionResolver.CalculateTackleProbabilities(tackler, target, state);

            // Apply Hard Limits
            float scoreDiff = (tackler.TeamSimId == 0) ? (state.HomeScore - state.AwayScore) : (state.AwayScore - state.HomeScore);
            float minutesRemaining = MatchSimulator.MATCH_DURATION_MINUTES - (state.MatchTimeSeconds / 60f);
            bool desperate = minutesRemaining < 10f && scoreDiff < -1;
            float foulChanceLimit = desperate ? MAX_TACKLE_FOUL_CHANCE_ESTIMATE * 1.2f : MAX_TACKLE_FOUL_CHANCE_ESTIMATE;

            if (estimatedSuccessChance < MIN_TACKLE_SUCCESS_CHANCE_ESTIMATE) return false;
            if (estimatedFoulChance > foulChanceLimit) return false;

            // Tactical Desirability Calculation
            float tackleDesirability = TACKLE_DESIRABILITY_BASE_THRESHOLD;
            tackleDesirability += estimatedSuccessChance * TACKLE_SUCCESS_DESIRABILITY_WEIGHT;
            tackleDesirability -= estimatedFoulChance * TACKLE_FOUL_DESIRABILITY_WEIGHT;
            tackleDesirability -= target.Velocity.magnitude * TACKLE_TARGET_SPEED_PENALTY;

            Vector2 ownGoal = tackler.TeamSimId == 0 ? MatchSimulator.PitchGeometry.HomeGoalCenter : MatchSimulator.PitchGeometry.AwayGoalCenter;
            float distToOwnGoal = Vector2.Distance(target.Position, ownGoal);
            if (distToOwnGoal < MatchSimulator.PitchGeometry.FreeThrowLineRadius + TACKLE_NEAR_GOAL_THRESHOLD_DIST_FACTOR) {
                tackleDesirability += TACKLE_NEAR_GOAL_BONUS;
            }
            if (desperate) { tackleDesirability += TACKLE_DESPERATION_BONUS; }

            // Factor in player attributes (safe access using ?.)
            float decisionMakingInfluence = ((tackler.BaseData?.DecisionMaking ?? 50f) - 50f) / 50f * TACKLE_DECISION_MAKING_FACTOR;
            float aggressionInfluence = ((tackler.BaseData?.Aggression ?? 50f) - 50f) / 50f * TACKLE_AGGRESSION_FACTOR;
            float requiredDesirability = TACKLE_DESIRABILITY_BASE_THRESHOLD + decisionMakingInfluence - aggressionInfluence;

            // Final Decision
            return tackleDesirability > requiredDesirability;
        }


        // --- Evaluation & Helper Methods ---

        /// <summary>Evaluates if shooting is a good option for the player in the current state.</summary>
        private bool EvaluateShootingOpportunity(SimPlayer shooter, MatchState state)
        {
             if (shooter?.BaseData == null || state == null || !shooter.HasBall) return false; // Null checks

             Vector2 targetGoal = shooter.TeamSimId == 0 ? MatchSimulator.PitchGeometry.AwayGoalCenter : MatchSimulator.PitchGeometry.HomeGoalCenter;
             float distance = Vector2.Distance(shooter.Position, targetGoal);
             if (distance < MIN_SHOOTING_DIST || distance > MAX_SHOOTING_DIST) return false;

             bool isAttackingHomeGoal = (shooter.TeamSimId == 1);
             if (MatchSimulator.PitchGeometry.IsInGoalArea(shooter.Position, isAttackingHomeGoal)) return false;

             int blockerCount = CountBlockers(shooter, state);
             if (blockerCount > 1) return false;

             float angle = CalculateShootingAngle(shooter.Position, targetGoal, shooter.TeamSimId);
             if (angle < MIN_ACCEPTABLE_ANGLE_DEGREES) return false;

             float shotDesirability = 1.0f;
             SimPlayer gk = state.GetGoalkeeper(1 - shooter.TeamSimId); // Get opposing GK
             if (gk != null) {
                 float gkCoverFactor = CalculateGKCoverFactor(gk, shooter.Position, targetGoal, angle);
                 shotDesirability *= (1.0f - gkCoverFactor * SHOOT_GK_COVER_PENALTY_FACTOR);
             } else { shotDesirability *= 1.5f; } // No GK bonus

             float attributeFactor = ((shooter.BaseData.ShootingAccuracy * 0.6f + shooter.BaseData.Composure * 0.4f) / SHOOT_ATTRIBUTE_BENCHMARK);
             shotDesirability *= Mathf.Clamp(attributeFactor, SHOOT_ATTRIBUTE_SCALE_MIN, SHOOT_ATTRIBUTE_SCALE_MAX);

             if(IsBack(shooter.BaseData.PrimaryPosition)) { shotDesirability *= SHOOT_POS_MOD_BACK; }
             else if (IsWing(shooter.BaseData.PrimaryPosition)) { shotDesirability *= SHOOT_POS_MOD_WING; }

             shotDesirability *= Mathf.Lerp(1.0f, 1.0f - FATIGUE_SHOOT_PENALTY, 1.0f - shooter.Stamina);

             float minutesPlayed = state.MatchTimeSeconds / 60f;
             float minutesRemaining = MatchSimulator.MATCH_DURATION_MINUTES - minutesPlayed;
             int scoreDiff = (shooter.TeamSimId == 0) ? (state.HomeScore - state.AwayScore) : (state.AwayScore - state.HomeScore);
             if (minutesRemaining < LATE_GAME_MINUTES_THRESHOLD) {
                 if (scoreDiff < DESPERATION_SCORE_DIFF) shotDesirability *= SHOOT_LATE_DESPERATION_MOD;
                 else if (scoreDiff > 2) shotDesirability *= SHOOT_LATE_COMFORTABLE_LEAD_MOD;
             }

             if (blockerCount == 1) shotDesirability *= SHOOT_ONE_BLOCKER_PENALTY;

             float decisionThreshold = SHOOT_DESIRABILITY_BASE_THRESHOLD * Mathf.Lerp(SHOOT_DECISION_MAKING_THRESHOLD_MIN_MOD, SHOOT_DECISION_MAKING_THRESHOLD_MAX_MOD, shooter.BaseData.DecisionMaking / 100f);

             return shotDesirability > decisionThreshold;
         }

        /// <summary>Evaluates potential pass options for the player.</summary>
        private List<PassOption> EvaluatePassOptions(SimPlayer passer, MatchState state, bool safeOnly)
        {
             List<PassOption> options = new List<PassOption>();
             if (passer?.BaseData == null || state == null) return options; // Null checks

             var teammates = state.GetTeamOnCourt(passer.TeamSimId)?.Where(p => p != passer && p != null && p.IsOnCourt && !p.IsSuspended());
             if (teammates == null) return options;

             foreach (var teammate in teammates)
             {
                 float distance = Vector2.Distance(passer.Position, teammate.Position);
                 if (distance < PASS_EVAL_MIN_DIST || distance > PASS_EVAL_MAX_DIST) continue;

                 float score = 1.0f;
                 float nearestDefenderDist = CalculateNearestDefenderDistance(teammate, state);
                 float opennessScore = Mathf.Clamp01(nearestDefenderDist / 5f);
                 score *= Mathf.Lerp(1.0f - PASS_EVAL_OPENNESS_WEIGHT, 1.0f + PASS_EVAL_OPENNESS_WEIGHT, opennessScore);

                 float distanceScore = Mathf.Clamp01(1.0f - (distance / PASS_EVAL_MAX_DIST));
                 score *= Mathf.Lerp(1.0f - PASS_EVAL_DISTANCE_WEIGHT, 1.0f + PASS_EVAL_DISTANCE_WEIGHT, distanceScore);

                 float interceptionRisk = EstimateInterceptionRisk(passer, teammate, state);
                 float safetyScore = 1.0f - interceptionRisk;
                 score *= Mathf.Lerp(1.0f - PASS_EVAL_INTERCEPTION_RISK_WEIGHT, 1.0f + PASS_EVAL_INTERCEPTION_RISK_WEIGHT, safetyScore);

                 Vector2 passVector = teammate.Position - passer.Position;
                 Vector2 ownGoal = passer.TeamSimId == 0 ? MatchSimulator.PitchGeometry.HomeGoalCenter : MatchSimulator.PitchGeometry.AwayGoalCenter;
                 Vector2 attackDir = (ownGoal - passer.Position).normalized * (passer.TeamSimId == 0 ? -1f : 1f);
                 float forwardFactor = Vector2.Dot(passVector.normalized, attackDir);
                 score *= (1.0f + forwardFactor * PASS_FORWARD_BONUS_FACTOR);

                 bool isSafe = interceptionRisk < PASS_INTERCEPTION_RISK_SAFE_THRESHOLD && nearestDefenderDist > PASS_OPENNESS_SAFE_THRESHOLD; // Use constants

                 if (!safeOnly || isSafe) {
                     options.Add(new PassOption { Player = teammate, Score = score, IsSafe = isSafe });
                 }
             }

             options.Sort((a, b) => b.Score.CompareTo(a.Score));
             return options;
         }

        /// <summary>Calculates distance to the nearest opponent for a given player.</summary>
        private float CalculateNearestDefenderDistance(SimPlayer player, MatchState state)
        {
            float nearestDistSq = float.MaxValue;
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            if (opponents == null) return float.MaxValue; // No opponents = infinitely open

            foreach (var opponent in opponents) {
                if (opponent == null || opponent.IsSuspended()) continue;
                nearestDistSq = Mathf.Min(nearestDistSq, Vector2.SqrMagnitude(player.Position - opponent.Position));
            }
            return Mathf.Sqrt(nearestDistSq);
        }

        /// <summary>Estimates the risk of a potential pass being intercepted.</summary>
        private float EstimateInterceptionRisk(SimPlayer passer, SimPlayer target, MatchState state)
        {
              if (passer == null || target == null || state == null) return 1f; // Max risk on error

              float maxRisk = 0f;
              var opponents = state.GetOpposingTeamOnCourt(passer.TeamSimId);
              if (opponents == null) return 0f; // No risk if no opponents

              Vector2 passStart = passer.Position;
              Vector2 passEnd = target.Position;
              float passLength = Vector2.Distance(passStart, passEnd);

              foreach (var opponent in opponents) {
                  if (opponent == null || opponent.IsSuspended()) continue;

                  if(Vector2.Distance(opponent.Position, target.Position) < 1.0f) {
                       maxRisk = Mathf.Max(maxRisk, 0.7f); continue;
                  }

                  float distToLine = SimulationUtils.CalculateDistanceToLine(opponent.Position, passStart, passEnd);

                  if (distToLine < MatchSimulator.INTERCEPTION_RADIUS * 1.5f) {
                       Vector2 vecA = opponent.Position - passStart;
                       Vector2 vecB = passEnd - passStart;
                       float dotPasser = Vector2.Dot(vecA, vecB);
                       bool isBetween = dotPasser > 0f && dotPasser < vecB.sqrMagnitude;

                       if (isBetween) {
                           float positionalRisk = Mathf.Pow(1.0f - distToLine / (MatchSimulator.INTERCEPTION_RADIUS * 1.5f), 2);
                           float skillFactor = Mathf.Lerp(0.7f, 1.3f, (opponent.BaseData?.Anticipation ?? 50f) / 100f); // Use ?.
                           float lengthFactor = Mathf.Clamp01(passLength / PASS_EVAL_MAX_DIST);
                           float currentRisk = INTERCEPTION_BASE_CHANCE * positionalRisk * skillFactor * (1.0f + lengthFactor * 0.5f);
                           maxRisk = Mathf.Max(maxRisk, currentRisk);
                       }
                  }
              }
              return Mathf.Clamp01(maxRisk);
         }

        /// <summary>Checks if the ball in flight is intended for this player.</summary>
        private bool IsPassIncoming(SimPlayer player, MatchState state) {
            if (player == null || state?.Ball == null) return false;
            return state.Ball.IsInFlight && state.Ball.IntendedTarget == player;
        }

        /// <summary>Determines if an interception is physically possible and calculates the chance.</summary>
        private bool CanIntercept(SimPlayer defender, MatchState state, out float calculatedChance)
        {
              calculatedChance = 0f;
              if (defender == null || state?.Ball == null || _actionResolver == null) return false; // Null checks
              if (!state.Ball.IsInFlight || state.Ball.Passer == null || state.Ball.IntendedTarget == null || defender.TeamSimId == state.Ball.Passer.TeamSimId) {
                  return false;
              }

              Vector2 interceptPoint = EstimatePassInterceptPoint(state.Ball, defender);
              float distToInterceptPoint = Vector2.Distance(defender.Position, interceptPoint);
              float ballDistToIntercept = Vector2.Distance(state.Ball.Position, interceptPoint);
              float ballSpeed = state.Ball.Velocity.magnitude;

              if (ballSpeed < 0.1f) return false;

              float timeForBall = ballDistToIntercept / ballSpeed;
              float timeForDefender = distToInterceptPoint / Mathf.Max(0.5f, defender.EffectiveSpeed * 1.1f); // Slightly boosted speed estimate

              if (timeForDefender < timeForBall * INTERCEPT_PHYSICALLY_POSSIBLE_TIME_FACTOR) // Use constant
              {
                   calculatedChance = _actionResolver.CalculateInterceptionChance(defender, state.Ball, state);
                   return calculatedChance > INTERCEPT_MIN_POSSIBLE_CHANCE; // Use constant
              }
              return false;
         }

        /// <summary>Estimates a likely point in space where a player might intercept an incoming pass.</summary>
        private Vector2 EstimatePassInterceptPoint(SimBall ball, SimPlayer receiverOrInterceptor)
        {
               if (ball == null || receiverOrInterceptor == null) return Vector2.zero; // Null checks

               float distanceToBall = Vector2.Distance(receiverOrInterceptor.Position, ball.Position);
               if (distanceToBall < 1.0f) return ball.Position;

               float timeToReachRough = distanceToBall / Mathf.Max(1f, receiverOrInterceptor.EffectiveSpeed);
               float projectionTime = Mathf.Clamp(timeToReachRough * INTERCEPT_ESTIMATE_PROJECTION_FACTOR,
                                                 INTERCEPT_ESTIMATE_PROJECTION_MIN_TIME, INTERCEPT_ESTIMATE_PROJECTION_MAX_TIME); // Use constants

               Vector2 interceptPos = ball.Position + ball.Velocity * projectionTime;

                if(ball.IntendedTarget != null) {
                     interceptPos = Vector2.Lerp(interceptPos, ball.IntendedTarget.Position, 0.1f); // Slight bias
                }

               return interceptPos;
         }

        /// <summary>Estimates where the ball will cross the goal line's X-coordinate.</summary>
        private Vector2 EstimateBallGoalLineImpact(SimBall ball, int defendingTeamSimId)
        {
               if (ball == null) return Vector2.zero;

               float goalLineX = (defendingTeamSimId == 0) ? 0f : MatchSimulator.PitchGeometry.Length;
               Vector2 currentPos = ball.Position;
               Vector2 currentVel = ball.Velocity;

               if (Mathf.Abs(currentVel.x) < 0.1f) { return new Vector2(goalLineX, currentPos.y); }

               float timeToGoalLine = (goalLineX - currentPos.x) / currentVel.x;

               if (timeToGoalLine < -0.1f || timeToGoalLine > 3.0f) {
                   return new Vector2(goalLineX, currentPos.y);
               }

               float avgSpeedFactor = Mathf.Clamp01(1.0f - timeToGoalLine * 0.1f); // Crude drag approximation
               float impactY = currentPos.y + currentVel.y * avgSpeedFactor * timeToGoalLine;

               return new Vector2(goalLineX, impactY);
         }

        /// <summary>Finds the most appropriate opponent player to mark based on threat and proximity.</summary>
        private SimPlayer FindOpponentToMark(SimPlayer player, MatchState state)
        {
              if (player == null || state == null || _positioner == null) return null; // Null checks

              SimPlayer opponentToMark = null;
              float minWeightedScore = float.MaxValue;

              // Cache tactical position
              Vector2 playerTacticalPos = _positioner.GetPlayerTargetPosition(player, state);
              var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId)?.Where(p => p != null && p != state.Ball.Holder && p.IsOnCourt && !p.IsSuspended());

              if (opponents == null || !opponents.Any()) return state.Ball.Holder; // Mark ball carrier if no others

              Vector2 ownGoal = player.TeamSimId == 0 ? MatchSimulator.PitchGeometry.HomeGoalCenter : MatchSimulator.PitchGeometry.AwayGoalCenter;

              foreach(var opp in opponents) {
                  // Calculate Threat Factor
                  float oppDistToGoal = Vector2.Distance(opp.Position, ownGoal);
                  float threatFactor = Mathf.Clamp01(1.0f - oppDistToGoal / (MatchSimulator.PitchGeometry.Length * MARKING_THREAT_DISTANCE_SCALE));
                  threatFactor *= Mathf.Clamp01(1.0f - Mathf.Abs(opp.Position.y - MatchSimulator.PitchGeometry.Center.y) / (MatchSimulator.PitchGeometry.Width * MARKING_THREAT_WIDTH_SCALE));

                  // Calculate Weighted Score (Lower is better)
                  float distToPlayer = Vector2.Distance(player.Position, opp.Position);
                  float distToTactical = Vector2.Distance(playerTacticalPos, opp.Position);
                  float score = (distToPlayer * MARKING_DISTANCE_WEIGHT_CURRENT + distToTactical * MARKING_DISTANCE_WEIGHT_TACTICAL) * (1.0f - threatFactor * MARKING_THREAT_WEIGHT); // Apply weights

                  if (score < minWeightedScore) {
                      minWeightedScore = score;
                      opponentToMark = opp;
                  }
              }
              return opponentToMark ?? state.Ball.Holder; // Fallback
         }

        /// <summary>Counts potential blockers between the shooter and the goal.</summary>
        private int CountBlockers(SimPlayer shooter, MatchState state)
        {
             if (shooter == null || state == null) return 5; // Assume blocked on error

             int blockerCount = 0;
             Vector2 targetGoal = shooter.TeamSimId == 0 ? MatchSimulator.PitchGeometry.AwayGoalCenter : MatchSimulator.PitchGeometry.HomeGoalCenter;
             Vector2 shotDir = (targetGoal - shooter.Position);
             if(shotDir.sqrMagnitude < MIN_DISTANCE_CHECK_SQ) return 5;
             shotDir.Normalize();

             var opponents = state.GetOpposingTeamOnCourt(shooter.TeamSimId);
             if (opponents == null) return 0;

             foreach (var opponent in opponents) {
                 if (opponent == null || !opponent.IsOnCourt || opponent.IsSuspended() || opponent.IsGoalkeeper()) continue;

                 Vector2 shooterToOpponent = opponent.Position - shooter.Position;
                 float distToOpponent = shooterToOpponent.magnitude;

                 if (distToOpponent > 0.5f && distToOpponent < BLOCKER_CHECK_DIST) {
                     float dot = Vector2.Dot(shooterToOpponent.normalized, shotDir);
                     if (dot > 0.8f) { // Alignment check
                          float distToShotLine = SimulationUtils.CalculateDistanceToLine(opponent.Position, shooter.Position, targetGoal);
                          if (distToShotLine < BLOCKER_CHECK_WIDTH) {
                              blockerCount++;
                          }
                     }
                 }
             }
             return blockerCount;
         }

        /// <summary>Calculates the shooting angle towards the goal.</summary>
        private float CalculateShootingAngle(Vector2 shooterPos, Vector2 targetGoalCenter, int shooterTeamSimId)
        {
             float goalLineX = (shooterTeamSimId == 0) ? MatchSimulator.PitchGeometry.Length : 0f;
             float postY1 = targetGoalCenter.y + MatchSimulator.PitchGeometry.GoalWidth / 2f;
             float postY2 = targetGoalCenter.y - MatchSimulator.PitchGeometry.GoalWidth / 2f;
             Vector2 post1Pos = new Vector2(goalLineX, postY1);
             Vector2 post2Pos = new Vector2(goalLineX, postY2);

             Vector2 vectorToPost1 = post1Pos - shooterPos;
             Vector2 vectorToPost2 = post2Pos - shooterPos;

             if (vectorToPost1.sqrMagnitude < MIN_DISTANCE_CHECK_SQ || vectorToPost2.sqrMagnitude < MIN_DISTANCE_CHECK_SQ) return 0f;

             float dot = Vector2.Dot(vectorToPost1.normalized, vectorToPost2.normalized);
             dot = Mathf.Clamp(dot, -1.0f, 1.0f);
             return Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        /// <summary>Calculates a factor representing how well the GK covers the shooting angle.</summary>
        private float CalculateGKCoverFactor(SimPlayer gk, Vector2 shooterPos, Vector2 targetGoalCenter, float shotAngle)
        {
              if (gk?.BaseData == null || shotAngle < 0.1f) return 0f; // Null check + angle check

              Vector2 shooterToGoalDir = (targetGoalCenter - shooterPos).normalized;
              Vector2 shooterToGKDir = (gk.Position - shooterPos).normalized;
              if (shooterToGoalDir.sqrMagnitude < MIN_DISTANCE_CHECK_SQ || shooterToGKDir.sqrMagnitude < MIN_DISTANCE_CHECK_SQ) return 0f;

              float alignmentFactor = Mathf.Clamp01(Vector2.Dot(shooterToGoalDir, shooterToGKDir));
              float distanceFactor = Mathf.Clamp01(1f - Vector2.Distance(gk.Position, shooterPos) / MAX_SHOOTING_DIST);
              float positioningFactor = Mathf.Lerp(0.8f, 1.2f, gk.BaseData.PositioningGK / 100f);
              float effectiveCoverageRatio = alignmentFactor * distanceFactor * positioningFactor;

              return Mathf.Clamp01(effectiveCoverageRatio);
        }

        // --- Helper Classes/Structs ---
        /// <summary>Represents a potential pass option with its calculated score.</summary>
        private class PassOption { public SimPlayer Player; public float Score; public bool IsSafe; }
        /// <summary>Checks if the player position is a winger.</summary>
        private bool IsWing(PlayerPosition pos) => pos == PlayerPosition.LeftWing || pos == PlayerPosition.RightWing;
        /// <summary>Checks if the player position is a backcourt player.</summary>
        private bool IsBack(PlayerPosition pos) => pos == PlayerPosition.LeftBack || pos == PlayerPosition.RightBack || pos == PlayerPosition.CentreBack;
    }
}