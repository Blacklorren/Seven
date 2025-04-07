using UnityEngine;
using HandballManager.Simulation.MatchData;
using System; // For Math

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Calculates the probabilistic outcome of discrete player actions
    /// like passes, shots, and tackles initiated by a player.
    /// Does NOT modify the game state extensively, primarily calculates outcomes or releases the ball.
    /// Reactive events (saves, blocks, interceptions in flight) are handled elsewhere.
    /// State updates based on results are handled by MatchEventHandler.
    /// </summary>
    public class ActionResolver
    {
        // --- Constants ---

        // General
        private const float MAX_PRESSURE_DIST = 2.5f; // Max distance opponent exerts pressure from

        // Pass Constants
        private const float BASE_PASS_ACCURACY = 0.92f;
        private const float PASS_DISTANCE_FACTOR = 0.03f;   // Accuracy penalty per meter
        private const float PASS_PRESSURE_FACTOR = 0.25f;   // Max accuracy penalty from pressure
        private const float PASS_COMPOSURE_FACTOR = 0.5f;   // How much high composure (100) reduces pressure penalty (0=none, 1=full reduction)
        private const float PASS_SKILL_WEIGHT_PASSING = 0.6f;
        private const float PASS_SKILL_WEIGHT_DECISION = 0.2f;
        private const float PASS_SKILL_WEIGHT_TECHNIQUE = 0.2f;
        private const float PASS_ACCURACY_SKILL_MIN_MOD = 0.7f; // Min multiplier from skill (at 0 skill)
        private const float PASS_ACCURACY_SKILL_MAX_MOD = 1.15f; // Max multiplier from skill (at 100 skill)
        private const float PASS_ACCURATE_ANGLE_OFFSET_RANGE = 5f; // Degrees +/- for accurate passes
        private const float PASS_INACCURATE_ANGLE_OFFSET_MIN = 15f; // Min degrees offset
        private const float PASS_INACCURATE_ANGLE_OFFSET_MAX = 45f; // Max degrees offset
        private const float PASS_INACCURATE_SPEED_MIN_FACTOR = 0.3f;
        private const float PASS_INACCURATE_SPEED_MAX_FACTOR = 0.6f;

        // Interception Probability (Used by MatchSimulator's reactive check)
        private const float INTERCEPTION_BASE_CHANCE = 0.12f;
        private const float INTERCEPTION_ATTRIBUTE_WEIGHT = 0.6f;   // Influence of skills vs position
        private const float INTERCEPTION_POSITION_WEIGHT = 0.4f;    // Influence of position vs skills
        private const float INTERCEPTION_SKILL_WEIGHT_ANTICIPATION = 0.6f;
        private const float INTERCEPTION_SKILL_WEIGHT_AGILITY = 0.2f;
        private const float INTERCEPTION_SKILL_WEIGHT_POSITIONING = 0.2f;
        private const float INTERCEPTION_SKILL_MIN_MOD = 0.5f;
        private const float INTERCEPTION_SKILL_MAX_MOD = 1.5f;
        private const float INTERCEPTION_PASS_PROGRESS_BASE_FACTOR = 0.6f;
        private const float INTERCEPTION_PASS_PROGRESS_MIDPOINT_BONUS = 0.4f; // Bonus applied at midpoint (using Sin)
        private const float INTERCEPTION_PASS_SPEED_MAX_PENALTY = 0.5f; // Max penalty (50%) for fastest passes
        private const float INTERCEPTION_CLOSING_FACTOR_MIN_SCALE = 0.5f; // Min multiplier if moving directly away
        private const float INTERCEPTION_CLOSING_FACTOR_MAX_SCALE = 1.2f; // Max multiplier if moving directly towards

        // Shot Constants (Accuracy/Inaccuracy determined here, outcome later)
        private const float SHOT_ACCURACY_BASE = 100f;            // Maximum potential accuracy roll (used with player accuracy stat)
        private const float SHOT_MAX_ANGLE_OFFSET_DEGREES = 15f;  // Max degrees offset for lowest accuracy shot
        private const float SHOT_PRESSURE_INACCURACY_MOD = 1.5f;  // Pressure multiplier on potential angle offset
        private const float SHOT_COMPOSURE_FACTOR = 0.6f;         // How much high composure reduces pressure effect (0=none, 1=full)
        private const float SHOT_MAX_DEVIATION_CLAMP_FACTOR = 1.5f; // Max multiplier clamp for angle deviation

        // Tackle Constants
        private const float BASE_TACKLE_SUCCESS = 0.40f;
        private const float BASE_TACKLE_FOUL_CHANCE = 0.25f;
        private const float TACKLE_ATTRIBUTE_SCALING = 0.5f;        // Max bonus/penalty from skill ratio
        private const float MIN_TACKLE_TARGET_SKILL_DENOMINATOR = 10f; // Safety for division
        private const float TACKLE_SUCCESS_SKILL_RANGE_MOD = 1.5f;  // Multiplier for skill effect range on success
        private const float TACKLE_FOUL_SKILL_RANGE_MOD = 0.5f;     // Multiplier for skill effect range on foul chance
        private const float TACKLE_AGGRESSION_FOUL_FACTOR_MIN = 0.7f;
        private const float TACKLE_AGGRESSION_FOUL_FACTOR_MAX = 1.5f;
        private const float TACKLE_FROM_BEHIND_FOUL_MOD = 1.6f;     // Increased penalty
        private const float TACKLE_HIGH_SPEED_FOUL_MOD = 1.4f;      // Max multiplier for high closing speed foul chance
        private const float TACKLE_HIGH_SPEED_THRESHOLD_FACTOR = 0.6f; // % of Max Player Speed to trigger high speed check
        private const float TACKLE_CLEAR_CHANCE_FOUL_MOD = 2.0f;    // Higher foul chance multiplier if denying chance
        private const float TACKLE_SKILL_WEIGHT_TACKLING = 0.5f;
        private const float TACKLE_SKILL_WEIGHT_STRENGTH = 0.3f;
        private const float TACKLE_SKILL_WEIGHT_ANTICIPATION = 0.2f;
        private const float TARGET_SKILL_WEIGHT_DRIBBLING = 0.4f;
        private const float TARGET_SKILL_WEIGHT_AGILITY = 0.3f;
        private const float TARGET_SKILL_WEIGHT_STRENGTH = 0.2f;
        private const float TARGET_SKILL_WEIGHT_COMPOSURE = 0.1f;

        // Foul Severity Constants
        private const float FOUL_SEVERITY_FROM_BEHIND_BONUS = 0.2f;
        private const float FOUL_SEVERITY_HIGH_SPEED_THRESHOLD_FACTOR = 0.7f; // Speed threshold for severity increase
        private const float FOUL_SEVERITY_HIGH_SPEED_BONUS = 0.15f;
        private const float FOUL_SEVERITY_AGGRESSION_FACTOR = 0.3f; // How much aggression contributes
        private const float FOUL_SEVERITY_DOGSO_BONUS = 0.4f; // Factor increase for denying clear chance
        private const float FOUL_SEVERITY_DOGSO_RED_CARD_BASE_CHANCE = 0.1f;
        private const float FOUL_SEVERITY_DOGSO_RED_CARD_SEVERITY_SCALE = 0.4f;
        private const float FOUL_SEVERITY_DOGSO_RED_CARD_RECKLESS_BONUS = 0.2f;
        private const float FOUL_SEVERITY_RECKLESS_SPEED_THRESHOLD_FACTOR = 0.8f; // Speed threshold for recklessness check
        private const float FOUL_SEVERITY_RECKLESS_AGGRESSION_THRESHOLD = 65;   // Aggression threshold for recklessness check
        private const float FOUL_SEVERITY_TWO_MINUTE_THRESHOLD_BASE = 0.1f;
        private const float FOUL_SEVERITY_TWO_MINUTE_THRESHOLD_SEVERITY_SCALE = 0.6f;
        private const float FOUL_SEVERITY_RED_CARD_THRESHOLD_BASE = 0.01f;
        private const float FOUL_SEVERITY_RED_CARD_THRESHOLD_SEVERITY_SCALE = 0.15f;


        /// <summary>
        /// Resolves a prepared action (Pass, Shot, Tackle) when its timer completes.
        /// </summary>
        public ActionResult ResolvePreparedAction(SimPlayer player, MatchState state)
        {
             if (player == null || state == null) return new ActionResult { Outcome = ActionResultOutcome.Failure, Reason = "Null Player/State" };

             // Store current action before resetting
             PlayerAction actionToResolve = player.CurrentAction;

             // Reset player action state immediately - prevents re-resolving if state changes mid-handling
             // EXCEPT for tackle, where we need the action state for calculation
             if (actionToResolve != PlayerAction.AttemptingTackle) {
                 player.CurrentAction = PlayerAction.Idle;
                 player.ActionTimer = 0f; // Ensure timer is cleared
             }

             switch (actionToResolve)
             {
                case PlayerAction.PreparingPass:
                    // Target validity check
                    if (player.TargetPlayer == null || !player.TargetPlayer.IsOnCourt || player.TargetPlayer.IsSuspended()) {
                        // Already reset action state above
                        return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = player, Reason = "Invalid Pass Target on Release" };
                    }
                    return ResolvePassAttempt(player, player.TargetPlayer, state);

                case PlayerAction.PreparingShot:
                    return ResolveShotAttempt(player, state);

                case PlayerAction.AttemptingTackle:
                    SimPlayer opponentToTackle = player.TargetPlayer;
                     // Re-verify target validity and range
                    if (opponentToTackle == null || opponentToTackle != state.Ball.Holder || Vector2.Distance(player.Position, opponentToTackle.Position) > MatchSimulator.TACKLE_RADIUS * 1.3f) // Slightly wider range check here
                    {
                         player.CurrentAction = PlayerAction.Idle; // Reset action state NOW
                         player.ActionTimer = 0f;
                         return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = player, Reason = "Tackle Target Invalid/Out of Range on Release" };
                    }
                    // ResolveTackleAttempt will reset player state internally AFTER calculations
                    return ResolveTackleAttempt(player, opponentToTackle, state);

                default:
                    Debug.LogWarning($"ActionResolver: Attempting to resolve unhandled prepared action: {actionToResolve}");
                    // State already reset for non-tackle cases
                    return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = player, Reason = "Unhandled Prepared Action" };
            }
        }

        /// <summary>
        /// Resolves a pass attempt at the moment of release.
        /// Determines if the release is accurate (ball becomes InFlight) or inaccurate (Turnover/loose ball).
        /// </summary>
        private ActionResult ResolvePassAttempt(SimPlayer passer, SimPlayer target, MatchState state)
        {
            if (passer == null || target == null || state == null) return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = passer, Reason = "Null input in ResolvePassAttempt" };

            float accuracyChance = BASE_PASS_ACCURACY;

            // Attributes
            float passSkill = (passer.BaseData.Passing * PASS_SKILL_WEIGHT_PASSING +
                               passer.BaseData.DecisionMaking * PASS_SKILL_WEIGHT_DECISION +
                               passer.BaseData.Technique * PASS_SKILL_WEIGHT_TECHNIQUE);
            accuracyChance *= Mathf.Lerp(PASS_ACCURACY_SKILL_MIN_MOD, PASS_ACCURACY_SKILL_MAX_MOD, passSkill / 100f); // Scale based on weighted skill

            // Distance
            float distance = Vector2.Distance(passer.Position, target.Position);
            accuracyChance -= Mathf.Clamp(distance * PASS_DISTANCE_FACTOR, 0f, 0.5f); // Max 50% penalty from distance

            // Pressure
             float pressure = CalculatePressureOnPlayer(passer, state);
             float pressurePenalty = pressure * PASS_PRESSURE_FACTOR;
             // Composure reduces pressure penalty
             pressurePenalty *= (1.0f - Mathf.Lerp(0f, PASS_COMPOSURE_FACTOR, passer.BaseData.Composure / 100f));
             accuracyChance -= pressurePenalty;

            accuracyChance = Mathf.Clamp01(accuracyChance);

            // Note: Passer's action state reset in ResolvePreparedAction before calling this

            if (state.RandomGenerator.NextDouble() < accuracyChance)
            {
                 // ACCURATE RELEASE: Set ball in flight
                 float speed = MatchSimulator.PASS_BASE_SPEED * Mathf.Lerp(0.8f, 1.2f, passer.BaseData.Passing / 100f);
                 Vector2 direction = (target.Position - passer.Position).normalized;
                 if(direction == Vector2.zero) direction = Vector2.right; // Avoid zero direction

                 // Add slight random deviation even on accurate passes
                 float accurateOffset = UnityEngine.Random.Range(-PASS_ACCURATE_ANGLE_OFFSET_RANGE, PASS_ACCURATE_ANGLE_OFFSET_RANGE) * (1f - accuracyChance); // Less deviation for higher accuracy passes
                 direction = Quaternion.Euler(0, 0, accurateOffset) * direction;

                 state.Ball.ReleaseAsPass(passer, target, direction * speed);

                 // Use specific reason for logging/event handling differentiation
                 return new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = passer, SecondaryPlayer = target, Reason = "Pass Released" };
            }
            else
            {
                // INACCURATE RELEASE: Ball becomes loose immediately
                 Vector2 targetDirection = (target.Position - passer.Position).normalized;
                 if(targetDirection == Vector2.zero) targetDirection = Vector2.right; // Avoid zero direction

                 // Wider angle offset for inaccurate passes
                 float angleOffset = UnityEngine.Random.Range(PASS_INACCURATE_ANGLE_OFFSET_MIN, PASS_INACCURATE_ANGLE_OFFSET_MAX) * (1.0f - accuracyChance) * (state.RandomGenerator.Next(0, 2) == 0 ? 1f : -1f); // Random direction +/-
                 Vector2 missDirection = Quaternion.Euler(0, 0, angleOffset) * targetDirection;
                 float missSpeed = MatchSimulator.PASS_BASE_SPEED * UnityEngine.Random.Range(PASS_INACCURATE_SPEED_MIN_FACTOR, PASS_INACCURATE_SPEED_MAX_FACTOR);

                 state.Ball.MakeLoose(passer.Position + missDirection * 0.3f, missDirection * missSpeed, passer.TeamSimId, passer);

                 return new ActionResult {
                    Outcome = ActionResultOutcome.Turnover, // Treat inaccurate pass as immediate turnover
                    PrimaryPlayer = passer, SecondaryPlayer = target,
                    Reason = "Pass Inaccurate", ImpactPosition = state.Ball.Position
                 };
            }
        }

        /// <summary>
        /// Resolves the release of a shot. Sets the ball in flight with calculated inaccuracy.
        /// Does NOT determine goal/save/miss outcome.
        /// </summary>
        private ActionResult ResolveShotAttempt(SimPlayer shooter, MatchState state)
        {
             if (shooter == null || state == null) return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = shooter, Reason = "Null input in ResolveShotAttempt" };

             Vector2 targetGoalCenter = shooter.TeamSimId == 0 ? MatchSimulator.PitchGeometry.AwayGoalCenter : MatchSimulator.PitchGeometry.HomeGoalCenter;

             float speed = MatchSimulator.SHOT_BASE_SPEED * Mathf.Lerp(0.8f, 1.2f, shooter.BaseData.ShootingPower / 100f);
             Vector2 idealDirection = (targetGoalCenter - shooter.Position).normalized;
             if(idealDirection == Vector2.zero) idealDirection = Vector2.right; // Avoid zero direction

             // Calculate inaccuracy angle based on Accuracy and Pressure
             float accuracyFactor = Mathf.Clamp(shooter.BaseData.ShootingAccuracy, 1f, 100f) / SHOT_ACCURACY_BASE; // Player accuracy relative to base
             float pressure = CalculatePressureOnPlayer(shooter, state);
             float composureEffect = Mathf.Lerp(1.0f, 1.0f - SHOT_COMPOSURE_FACTOR, shooter.BaseData.Composure / 100f); // High composure reduces pressure effect

             // Max deviation based on player accuracy
             float maxAngleDeviation = SHOT_MAX_ANGLE_OFFSET_DEGREES * (1.0f - accuracyFactor);
             // Pressure increases the deviation (reduced by composure)
             maxAngleDeviation *= (1.0f + pressure * SHOT_PRESSURE_INACCURACY_MOD * composureEffect);

             // Clamp max deviation for sanity
             maxAngleDeviation = Mathf.Clamp(maxAngleDeviation, 0f, SHOT_MAX_ANGLE_OFFSET_DEGREES * SHOT_MAX_DEVIATION_CLAMP_FACTOR);

             // Apply random offset within the calculated range
             float actualAngleOffset = UnityEngine.Random.Range(-maxAngleDeviation, maxAngleDeviation);
             Vector2 actualDirection = Quaternion.Euler(0, 0, actualAngleOffset) * idealDirection;

             state.Ball.ReleaseAsShot(shooter, actualDirection * speed);

             // Note: Shooter's action state reset in ResolvePreparedAction before calling this
             return new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = shooter, Reason = "Shot Taken" };
        }

        /// <summary>
        /// Calculates and resolves the outcome of a tackle attempt (Success, Foul, Failure).
        /// </summary>
        private ActionResult ResolveTackleAttempt(SimPlayer tackler, SimPlayer target, MatchState state)
        {
              // Additional defensive null check
              if (tackler == null || target == null || state == null) return new ActionResult { Outcome = ActionResultOutcome.Failure, Reason = "Null input in ResolveTackleAttempt" };

              var (successChance, foulChance) = CalculateTackleProbabilities(tackler, target, state);

              // Ensure probabilities don't exceed 1 when combined
              float totalProb = successChance + foulChance;
              if (totalProb > 1.0f) {
                  successChance /= totalProb; // Normalize
                  foulChance /= totalProb;
              }
              float failureChance = Mathf.Max(0f, 1.0f - successChance - foulChance); // Ensure failure chance is non-negative

              double roll = state.RandomGenerator.NextDouble();
              Vector2 impactPos = Vector2.Lerp(tackler.Position, target.Position, 0.5f);

              // Reset actions for both players *now* that calculations are done
               tackler.CurrentAction = PlayerAction.Idle;
               tackler.ActionTimer = 0f;
               // Also reset target's action as the tackle attempt interrupts them
               if (target.CurrentAction != PlayerAction.Suspended) // Don't override suspension
               {
                    target.CurrentAction = PlayerAction.Idle;
                    target.ActionTimer = 0f;
               }

              if (roll < successChance)
              {
                  // TACKLE SUCCESS
                  bool targetHadBall = target.HasBall; // Check before making loose
                   if (targetHadBall) {
                       state.Ball.MakeLoose(impactPos, UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1f, 3f), tackler.TeamSimId, tackler); // Give ball slight rebound velocity
                   } else {
                       // Ensure ball holder is cleared if tackle succeeds without target having ball (shouldn't happen if check was correct)
                       if (state.Ball.Holder == target) state.Ball.Holder = null;
                   }
                  // Even if target didn't have ball, success means tackler initiated clean contact without foul
                  return new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = tackler, SecondaryPlayer = target, ImpactPosition = impactPos, Reason = targetHadBall ? "Tackle Won Ball" : "Tackle Successful (No Ball)" };
              }
              else if (roll < successChance + foulChance)
              {
                  // FOUL COMMITTED
                  FoulSeverity severity = DetermineFoulSeverity(tackler, target, state);

                  // Ball becomes dead on foul
                   if (target.HasBall) target.HasBall = false; // Drop ball
                   if (state.Ball.Holder == target) state.Ball.Holder = null; // Ensure holder is null
                   state.Ball.Stop();
                   state.Ball.Position = impactPos;

                  return new ActionResult { Outcome = ActionResultOutcome.FoulCommitted, PrimaryPlayer = tackler, SecondaryPlayer = target, ImpactPosition = impactPos, FoulSeverity = severity };
              }
              else
              {
                  // TACKLE FAILED (EVADED)
                  // Target player continues (their action was reset, AI will decide next step)
                  return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = tackler, SecondaryPlayer = target, Reason = "Tackle Evaded" };
              }
         }

        /// <summary>
        /// Calculates the probabilities of success and foul for a potential tackle attempt.
        /// Public so AI can use it for decision making.
        /// </summary>
        public (float successChance, float foulChance) CalculateTackleProbabilities(SimPlayer tackler, SimPlayer target, MatchState state)
        {
            if (tackler == null || target == null || state == null) return (0f, 0f);

            float successChance = BASE_TACKLE_SUCCESS;
            float foulChance = BASE_TACKLE_FOUL_CHANCE;

            // --- Attribute Modifier ---
            // Tackling, Strength, Anticipation vs Dribbling, Agility, Strength, Composure
            float tacklerSkill = (tackler.BaseData.Tackling * TACKLE_SKILL_WEIGHT_TACKLING +
                                  tackler.BaseData.Strength * TACKLE_SKILL_WEIGHT_STRENGTH +
                                  tackler.BaseData.Anticipation * TACKLE_SKILL_WEIGHT_ANTICIPATION);
            float targetSkill = (target.BaseData.Dribbling * TARGET_SKILL_WEIGHT_DRIBBLING +
                                 target.BaseData.Agility * TARGET_SKILL_WEIGHT_AGILITY +
                                 target.BaseData.Strength * TARGET_SKILL_WEIGHT_STRENGTH +
                                 target.BaseData.Composure * TARGET_SKILL_WEIGHT_COMPOSURE);
            float ratio = tacklerSkill / Mathf.Max(MIN_TACKLE_TARGET_SKILL_DENOMINATOR, targetSkill); // Use constant for safety

            // Apply ratio with scaling factor, capped to prevent extreme results
            successChance *= Mathf.Clamp(1.0f + (ratio - 1.0f) * TACKLE_ATTRIBUTE_SCALING,
                                         1.0f - TACKLE_ATTRIBUTE_SCALING * TACKLE_SUCCESS_SKILL_RANGE_MOD,
                                         1.0f + TACKLE_ATTRIBUTE_SCALING * TACKLE_SUCCESS_SKILL_RANGE_MOD); // Wider range for effect

            // Foul chance modified by inverse ratio - better dribbler draws more fouls? Less effect?
            float foulSkillRatio = targetSkill / Mathf.Max(MIN_TACKLE_TARGET_SKILL_DENOMINATOR, tacklerSkill);
            foulChance *= Mathf.Clamp(1.0f + (foulSkillRatio - 1.0f) * TACKLE_ATTRIBUTE_SCALING * TACKLE_FOUL_SKILL_RANGE_MOD,
                                      1.0f - TACKLE_ATTRIBUTE_SCALING * TACKLE_FOUL_SKILL_RANGE_MOD * 0.5f, // Ensure range doesn't go too low easily
                                      1.0f + TACKLE_ATTRIBUTE_SCALING * TACKLE_FOUL_SKILL_RANGE_MOD); // Less extreme effect on foul chance

            // --- Situational Modifiers for Foul Chance ---
            foulChance *= Mathf.Lerp(TACKLE_AGGRESSION_FOUL_FACTOR_MIN, TACKLE_AGGRESSION_FOUL_FACTOR_MAX, tackler.BaseData.Aggression / 100f); // Aggression effect

            bool isFromBehind = IsTackleFromBehind(tackler, target);
            if (isFromBehind) foulChance *= TACKLE_FROM_BEHIND_FOUL_MOD;

            float closingSpeed = CalculateClosingSpeed(tackler, target);
            float highSpeedThreshold = MatchSimulator.MAX_PLAYER_SPEED * TACKLE_HIGH_SPEED_THRESHOLD_FACTOR;
            if (closingSpeed > highSpeedThreshold) {
                foulChance *= Mathf.Lerp(1.0f, TACKLE_HIGH_SPEED_FOUL_MOD, Mathf.Clamp01((closingSpeed - highSpeedThreshold) / (MatchSimulator.MAX_PLAYER_SPEED * (1.0f - TACKLE_HIGH_SPEED_THRESHOLD_FACTOR)))); // Scale effect
            }

            bool clearScoringChance = IsClearScoringChance(target, state);
            if (clearScoringChance) foulChance *= TACKLE_CLEAR_CHANCE_FOUL_MOD;

            // Clamp final probabilities
            successChance = Mathf.Clamp01(successChance);
            foulChance = Mathf.Clamp01(foulChance);

            return (successChance, foulChance);
        }

        /// <summary>
        /// Determines the severity of a foul based on context.
        /// </summary>
        private FoulSeverity DetermineFoulSeverity(SimPlayer tackler, SimPlayer target, MatchState state)
        {
             if (tackler == null || target == null || state == null) return FoulSeverity.FreeThrow; // Default on error

             FoulSeverity severity = FoulSeverity.FreeThrow; // Default
             float severityRoll = (float)state.RandomGenerator.NextDouble();

             // Base factors influencing severity increase
             float baseSeverityFactor = 0f;
             if (IsTackleFromBehind(tackler, target)) baseSeverityFactor += FOUL_SEVERITY_FROM_BEHIND_BONUS;

             float closingSpeed = CalculateClosingSpeed(tackler, target);
             if (closingSpeed > MatchSimulator.MAX_PLAYER_SPEED * FOUL_SEVERITY_HIGH_SPEED_THRESHOLD_FACTOR) baseSeverityFactor += FOUL_SEVERITY_HIGH_SPEED_BONUS;

             baseSeverityFactor += Mathf.Clamp((tackler.BaseData.Aggression - 50f) / 50f, -1f, 1f) * FOUL_SEVERITY_AGGRESSION_FACTOR; // Aggression effect (+/-)

             // Check for Clear Scoring Chance denial
             bool clearScoringChance = IsClearScoringChance(target, state);
             if (clearScoringChance) {
                 baseSeverityFactor += FOUL_SEVERITY_DOGSO_BONUS; // Significant increase for DOGSO fouls

                 // Check for Red Card (DOGSO + Reckless/Excessive Force Indicators)
                  bool reckless = (IsTackleFromBehind(tackler, target) && tackler.BaseData.Aggression > FOUL_SEVERITY_RECKLESS_AGGRESSION_THRESHOLD) ||
                                 closingSpeed > MatchSimulator.MAX_PLAYER_SPEED * FOUL_SEVERITY_RECKLESS_SPEED_THRESHOLD_FACTOR;
                  float redCardChanceDOGSO = FOUL_SEVERITY_DOGSO_RED_CARD_BASE_CHANCE
                                            + baseSeverityFactor * FOUL_SEVERITY_DOGSO_RED_CARD_SEVERITY_SCALE
                                            + (reckless ? FOUL_SEVERITY_DOGSO_RED_CARD_RECKLESS_BONUS : 0f);
                  if (severityRoll < Mathf.Clamp01(redCardChanceDOGSO)) { // Clamp chance for safety
                     return FoulSeverity.RedCard;
                  }
             }

             // Determine Suspension/Red based on roll and accumulated factors
             // Calculate thresholds *after* potential red card check for DOGSO
             float twoMinuteThreshold = FOUL_SEVERITY_TWO_MINUTE_THRESHOLD_BASE + baseSeverityFactor * FOUL_SEVERITY_TWO_MINUTE_THRESHOLD_SEVERITY_SCALE;
             float redCardThreshold = FOUL_SEVERITY_RED_CARD_THRESHOLD_BASE + baseSeverityFactor * FOUL_SEVERITY_RED_CARD_THRESHOLD_SEVERITY_SCALE;

             // Clamp thresholds before comparison
             twoMinuteThreshold = Mathf.Clamp01(twoMinuteThreshold);
             redCardThreshold = Mathf.Clamp01(redCardThreshold);


             if (severityRoll < redCardThreshold) { // Check red first as it's rarer
                  severity = FoulSeverity.RedCard;
             } else if (severityRoll < twoMinuteThreshold) {
                  severity = FoulSeverity.TwoMinuteSuspension;
             }
             // Else: remains FreeThrow

             // TODO: Check for offensive foul? (e.g., charging into stationary defender)

             return severity;
        }


        /// <summary>
        /// Calculates the probability of a defender intercepting a specific pass in flight.
        /// Used reactively by MatchSimulator.
        /// </summary>
        public float CalculateInterceptionChance(SimPlayer defender, SimBall ball, MatchState state)
        {
             // Add validation
             if (defender == null || ball == null || state == null) return 0f;
             if (!ball.IsInFlight || ball.IntendedTarget == null || ball.Passer == null || defender.TeamSimId == ball.Passer.TeamSimId) return 0f;

             float baseChance = INTERCEPTION_BASE_CHANCE;

             // Skill Modifier (Anticipation, Agility, Positioning)
             float defenderSkill = (defender.BaseData.Anticipation * INTERCEPTION_SKILL_WEIGHT_ANTICIPATION +
                                    defender.BaseData.Agility * INTERCEPTION_SKILL_WEIGHT_AGILITY +
                                    defender.BaseData.Positioning * INTERCEPTION_SKILL_WEIGHT_POSITIONING);
             float skillMod = Mathf.Lerp(INTERCEPTION_SKILL_MIN_MOD, INTERCEPTION_SKILL_MAX_MOD, defenderSkill / 100f); // Scale effect

             // Position Modifier (Closeness to pass line and ball)
             float distToLine = SimulationUtils.CalculateDistanceToLine(defender.Position, ball.PassOrigin, ball.IntendedTarget.Position); // Use Utility
             float lineProximityFactor = Mathf.Clamp01(1.0f - (distToLine / MatchSimulator.INTERCEPTION_RADIUS));
             lineProximityFactor *= lineProximityFactor; // Square for emphasis

             float distToBall = Vector2.Distance(defender.Position, ball.Position);
             float ballProximityFactor = Mathf.Clamp01(1.0f - (distToBall / (MatchSimulator.INTERCEPTION_RADIUS * 1.3f))); // Slightly larger radius check


             // Pass Properties Modifier
             float passDistTotal = Vector2.Distance(ball.PassOrigin, ball.IntendedTarget.Position);
             if (passDistTotal < 1.0f) passDistTotal = 1.0f;
             float passDistTravelled = Vector2.Distance(ball.PassOrigin, ball.Position);
             // Harder to intercept very early or very late in pass trajectory, easier mid-flight
             float passProgress = Mathf.Clamp01(passDistTravelled / passDistTotal);
             float passProgressFactor = INTERCEPTION_PASS_PROGRESS_BASE_FACTOR + (INTERCEPTION_PASS_PROGRESS_MIDPOINT_BONUS * Mathf.Sin(passProgress * Mathf.PI)); // Peaks mid-pass (at 0.5 progress)

             // Ball Speed Modifier (Faster passes harder to intercept)
             float ballSpeedFactor = Mathf.Clamp(1.0f - (ball.Velocity.magnitude / (MatchSimulator.PASS_BASE_SPEED * 1.5f)), 1.0f - INTERCEPTION_PASS_SPEED_MAX_PENALTY, 1.0f);


             // Final Chance Calculation - Combine factors more dynamically
             float finalChance = baseChance
                               * Mathf.Lerp(1.0f, skillMod, INTERCEPTION_ATTRIBUTE_WEIGHT) // Weighted attribute effect
                               * Mathf.Lerp(1.0f, lineProximityFactor * ballProximityFactor, INTERCEPTION_POSITION_WEIGHT) // Weighted position effect
                               * passProgressFactor
                               * ballSpeedFactor;

             // Movement Direction Modifier
              if (defender.Velocity.sqrMagnitude > 1f) {
                  Vector2 defenderToBallDir = (ball.Position - defender.Position).normalized;
                  float closingFactor = Vector2.Dot(defender.Velocity.normalized, defenderToBallDir); // -1 away, 0 perpendicular, 1 towards
                  finalChance *= Mathf.Lerp(INTERCEPTION_CLOSING_FACTOR_MIN_SCALE, INTERCEPTION_CLOSING_FACTOR_MAX_SCALE, (closingFactor + 1f) / 2f); // Penalize moving away more, reward moving towards
              }

             return Mathf.Clamp01(finalChance);
        }


        // --- Helper Methods ---

        private float CalculatePressureOnPlayer(SimPlayer player, MatchState state) {
            float pressure = 0f;
            if (player == null || state == null) return 0f; // Null check
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            if (opponents == null) return 0f; // Null check for opponent list

            foreach(var opponent in opponents) {
                if (opponent == null || opponent.IsSuspended()) continue; // Null check for opponent + suspended check
                float dist = Vector2.Distance(player.Position, opponent.Position);
                if (dist < MAX_PRESSURE_DIST) {
                    // Pressure increases more sharply when very close
                    pressure += Mathf.Pow(1.0f - (dist / MAX_PRESSURE_DIST), 2);
                }
            }
            return Mathf.Clamp01(pressure); // Max pressure = 1.0 (from one player directly on top)
        }

        private bool IsTackleFromBehind(SimPlayer tackler, SimPlayer target) {
             if (tackler == null || target == null) return false; // Null check
             if (target.Velocity.sqrMagnitude < 0.5f) return false; // Target needs to be moving with some pace
             Vector2 targetMovementDir = target.Velocity.normalized;
             Vector2 approachDir = (target.Position - tackler.Position);
             if(approachDir.sqrMagnitude < 0.01f) return false; // Avoid issues if overlapping
             approachDir.Normalize();
             // Angle between approach vector and *opposite* of target movement
             float angle = Vector2.Angle(approachDir, -targetMovementDir);
             return angle < 75f; // Slightly wider angle for 'behind'
         }

         private float CalculateClosingSpeed(SimPlayer tackler, SimPlayer target) {
              if (tackler == null || target == null) return 0f; // Null check
              Vector2 relativeVelocity = tackler.Velocity - target.Velocity;
              Vector2 axisToTarget = (target.Position - tackler.Position);
              if (axisToTarget.sqrMagnitude < 0.01f) return 0f;
              // Project relative velocity onto the axis connecting the players
              // Positive means getting closer along this axis
              return Vector2.Dot(relativeVelocity, -axisToTarget.normalized); // Note: Changed axis to -axis for closing speed
         }

         private bool IsClearScoringChance(SimPlayer target, MatchState state) {
             if (target == null || state == null || !target.HasBall) return false; // Null checks
             Vector2 opponentGoal = target.TeamSimId == 0 ? MatchSimulator.PitchGeometry.AwayGoalCenter : MatchSimulator.PitchGeometry.HomeGoalCenter;
             float distToGoal = Vector2.Distance(target.Position, opponentGoal);

             // Check 1: Within scoring range? (e.g., inside 12m)
             if (distToGoal > MatchSimulator.PitchGeometry.FreeThrowLineRadius + 3f) return false;

             // Check 2: Reasonable angle/central position?
             // Allow wider angles if closer
             float maxAngleOffset = Mathf.Lerp(6f, 9f, Mathf.Clamp01(distToGoal / (MatchSimulator.PitchGeometry.FreeThrowLineRadius + 3f)));
             if (Mathf.Abs(target.Position.y - MatchSimulator.PitchGeometry.Center.y) > maxAngleOffset) return false; // Too wide

             // Check 3: Number of defenders (excluding GK) significantly obstructing the path
             int defendersBlocking = 0;
             var opponents = state.GetOpposingTeamOnCourt(target.TeamSimId);
             if (opponents == null) return true; // If no opponents, it's clear

             Vector2 targetToGoalVec = opponentGoal - target.Position;
             if (targetToGoalVec.sqrMagnitude < 0.01f) return false; // At goal already?

             foreach(var opp in opponents) {
                 if (opp == null || opp.IsGoalkeeper() || opp.IsSuspended()) continue; // Null check + role check
                 // Is opponent significantly between target and goal?
                 Vector2 targetToOppVec = opp.Position - target.Position;
                 float oppDistToGoal = Vector2.Distance(opp.Position, opponentGoal);

                 // Check if opponent is ahead of target towards goal (dot product > 0) AND closer/similar distance to goal
                 float dot = Vector2.Dot(targetToOppVec.normalized, targetToGoalVec.normalized);
                 if (dot > 0.5f && oppDistToGoal < distToGoal * 1.1f) // Opponent somewhat aligned and not much further from goal
                 {
                       // Check lateral distance from direct line to goal
                       float distToLine = SimulationUtils.CalculateDistanceToLine(opp.Position, target.Position, opponentGoal); // Use Utility
                       if (distToLine < 2.5f) // Wider cone check for blocking path
                       {
                            defendersBlocking++;
                       }
                 }
             }
             // Clear chance if 0 or 1 field defender is potentially obstructing
             return defendersBlocking <= 1;
         }

        // Removed duplicate CalculateDistanceToLine - Call SimulationUtils.CalculateDistanceToLine instead

    } // End of ActionResolver class
}