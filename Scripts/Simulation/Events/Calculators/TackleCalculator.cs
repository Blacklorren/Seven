using HandballManager.Simulation.Constants;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Utils; // For ActionCalculatorUtils
using HandballManager.Simulation.Core.Constants; // For SimConstants
using UnityEngine;
using System;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations and resolution related to tackling actions.
    /// </summary>
    public class TackleCalculator
    {
        private readonly FoulCalculator _foulCalculator; // Dependency

        public TackleCalculator(FoulCalculator foulCalculator)
        {
            _foulCalculator = foulCalculator ?? throw new ArgumentNullException(nameof(foulCalculator));
        }

        /// <summary>
        /// Calculates and resolves the outcome of a tackle attempt (Success, Foul, Failure).
        /// </summary>
        public ActionResult ResolveTackleAttempt(SimPlayer tackler, MatchState state)
        {
             SimPlayer target = tackler?.TargetPlayer; // Get target from tackler state

             // Re-verify target validity and range
             if (tackler == null || target == null || state == null || target != state.Ball.Holder ||
                 Vector2.Distance(tackler.Position, target.Position) > ActionResolverConstants.TACKLE_RADIUS * ActionResolverConstants.TACKLE_RANGE_CHECK_BUFFER)
             {
                  if(tackler != null) { // Reset tackler if possible
                      tackler.CurrentAction = PlayerAction.Idle;
                      tackler.ActionTimer = 0f;
                  }
                  return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = tackler, Reason = "Tackle Target Invalid/Out of Range on Release" };
             }

             var (successChance, foulChance) = CalculateTackleProbabilities(tackler, target, state);

             float totalProb = successChance + foulChance;
             if (totalProb > 1.0f) { successChance /= totalProb; foulChance /= totalProb; }
             // float failureChance = Mathf.Max(0f, 1.0f - successChance - foulChance); // Not explicitly needed for roll check

             double roll = state.RandomGenerator.NextDouble();
             Vector2 impactPos = Vector2.Lerp(tackler.Position, target.Position, 0.5f);

             // Reset actions for both players *after* calculations
              tackler.CurrentAction = PlayerAction.Idle;
              tackler.ActionTimer = 0f;
              if (target.CurrentAction != PlayerAction.Suspended) {
                   target.CurrentAction = PlayerAction.Idle;
                   target.ActionTimer = 0f;
              }

             if (roll < successChance) {
                 // SUCCESS
                 bool targetHadBall = target.HasBall; // Must check BEFORE MakeLoose
                 if (targetHadBall) {
                     state.Ball.MakeLoose(ActionCalculatorUtils.GetPosition3D(target) + UnityEngine.Random.insideUnitSphere * 0.1f, // Use util, add 3D randomness
                                        UnityEngine.Random.insideUnitSphere.normalized * UnityEngine.Random.Range(1f, 3f), // 3D velocity
                                        tackler.TeamSimId, tackler);
                 } else if (state.Ball.Holder == target) {
                     state.Ball.Holder = null; // Safety cleanup
                 }
                 return new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = tackler, SecondaryPlayer = target, ImpactPosition = impactPos, Reason = targetHadBall ? "Tackle Won Ball" : "Tackle Successful (No Ball)" };
             }
             else if (roll < successChance + foulChance) {
                 // FOUL
                 FoulSeverity severity = _foulCalculator.DetermineFoulSeverity(tackler, target, state); // Use FoulCalculator

                 if (target.HasBall) target.HasBall = false;
                 if (state.Ball.Holder == target) state.Ball.Holder = null;
                 state.Ball.Stop();
                 state.Ball.Position = ActionCalculatorUtils.GetPosition3D(target); // Ball dead at target pos

                 return new ActionResult { Outcome = ActionResultOutcome.FoulCommitted, PrimaryPlayer = tackler, SecondaryPlayer = target, ImpactPosition = impactPos, FoulSeverity = severity };
             }
             else {
                 // FAILURE (EVADED)
                 return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = tackler, SecondaryPlayer = target, Reason = "Tackle Evaded", ImpactPosition = impactPos };
             }
        }

        /// <summary>
        /// Calculates the probabilities of success and foul for a potential tackle attempt.
        /// </summary>
        public (float successChance, float foulChance) CalculateTackleProbabilities(SimPlayer tackler, SimPlayer target, MatchState state)
        {
            if (tackler?.BaseData == null || target?.BaseData == null || state == null) return (0f, 0f);

            float successChance = ActionResolverConstants.BASE_TACKLE_SUCCESS;
            float foulChance = ActionResolverConstants.BASE_TACKLE_FOUL_CHANCE;

            // Attributes
            float tacklerSkill = (tackler.BaseData.Tackling * ActionResolverConstants.TACKLE_SKILL_WEIGHT_TACKLING +
                                  tackler.BaseData.Strength * ActionResolverConstants.TACKLE_SKILL_WEIGHT_STRENGTH +
                                  tackler.BaseData.Anticipation * ActionResolverConstants.TACKLE_SKILL_WEIGHT_ANTICIPATION);
            float targetSkill = (target.BaseData.Dribbling * ActionResolverConstants.TARGET_SKILL_WEIGHT_DRIBBLING +
                                 target.BaseData.Agility * ActionResolverConstants.TARGET_SKILL_WEIGHT_AGILITY +
                                 target.BaseData.Strength * ActionResolverConstants.TARGET_SKILL_WEIGHT_STRENGTH +
                                 target.BaseData.Composure * ActionResolverConstants.TARGET_SKILL_WEIGHT_COMPOSURE);

            float ratio = tacklerSkill / Mathf.Max(ActionResolverConstants.MIN_TACKLE_TARGET_SKILL_DENOMINATOR, targetSkill);
            successChance *= Mathf.Clamp(1.0f + (ratio - 1.0f) * ActionResolverConstants.TACKLE_ATTRIBUTE_SCALING,
                                         1.0f - ActionResolverConstants.TACKLE_ATTRIBUTE_SCALING * ActionResolverConstants.TACKLE_SUCCESS_SKILL_RANGE_MOD,
                                         1.0f + ActionResolverConstants.TACKLE_ATTRIBUTE_SCALING * ActionResolverConstants.TACKLE_SUCCESS_SKILL_RANGE_MOD);

            float foulSkillRatio = targetSkill / Mathf.Max(ActionResolverConstants.MIN_TACKLE_TARGET_SKILL_DENOMINATOR, tacklerSkill);
            foulChance *= Mathf.Clamp(1.0f + (foulSkillRatio - 1.0f) * ActionResolverConstants.TACKLE_ATTRIBUTE_SCALING * ActionResolverConstants.TACKLE_FOUL_SKILL_RANGE_MOD,
                                      1.0f - ActionResolverConstants.TACKLE_ATTRIBUTE_SCALING * ActionResolverConstants.TACKLE_FOUL_SKILL_RANGE_MOD * 0.5f,
                                      1.0f + ActionResolverConstants.TACKLE_ATTRIBUTE_SCALING * ActionResolverConstants.TACKLE_FOUL_SKILL_RANGE_MOD);

            // Situationals
            foulChance *= Mathf.Lerp(ActionResolverConstants.TACKLE_AGGRESSION_FOUL_FACTOR_MIN, ActionResolverConstants.TACKLE_AGGRESSION_FOUL_FACTOR_MAX, tackler.BaseData.Aggression / 100f);
            if (ActionCalculatorUtils.IsTackleFromBehind(tackler, target)) foulChance *= ActionResolverConstants.TACKLE_FROM_BEHIND_FOUL_MOD; // Use Util
            float closingSpeed = ActionCalculatorUtils.CalculateClosingSpeed(tackler, target); // Use Util
            float highSpeedThreshold = ActionResolverConstants.MAX_PLAYER_SPEED * ActionResolverConstants.TACKLE_HIGH_SPEED_THRESHOLD_FACTOR;
            if (closingSpeed > highSpeedThreshold) {
                foulChance *= Mathf.Lerp(1.0f, ActionResolverConstants.TACKLE_HIGH_SPEED_FOUL_MOD, Mathf.Clamp01((closingSpeed - highSpeedThreshold) / (ActionResolverConstants.MAX_PLAYER_SPEED * (1.0f - ActionResolverConstants.TACKLE_HIGH_SPEED_THRESHOLD_FACTOR))));
            }
            if (ActionCalculatorUtils.IsClearScoringChance(target, state)) foulChance *= ActionResolverConstants.TACKLE_CLEAR_CHANCE_FOUL_MOD; // Use Util

            successChance = Mathf.Clamp01(successChance);
            foulChance = Mathf.Clamp01(foulChance);

            return (successChance, foulChance);
        }
    }
}