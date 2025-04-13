using HandballManager.Data;
using HandballManager.Core; // For Enums like TrainingFocus, PlayerPosition
using UnityEngine; // For Debug.Log, Mathf, Random
using System; // For Math, Func, Action
using System.Collections.Generic; // For List
using System.Linq; // For Linq operations

namespace HandballManager.Simulation
{
    /// <summary>
    /// Simulates the effects of weekly training on player attributes, condition, and injury risk.
    /// </summary>
    public class TrainingSimulator
    {
        // --- Constants ---
        // Attribute Gain
        private const float BASE_ATTRIBUTE_GAIN_CHECK_CHANCE = 0.15f; // Base chance per relevant attribute *to be considered* for a gain
        private const float GAIN_SUCCESS_PROBABILITY_BASE = 0.1f;   // Base probability of +1 IF the check passes (scaled by factors)
        private const float POTENTIAL_AWARE_GAIN_SCALING = 0.08f;   // How much potential gap influences gain success probability

        // Condition & Injury
        private const float BASE_CONDITION_CHANGE = -0.05f; // Base fatigue from a normal week
        private const float CONDITION_RECOVERY_FACTOR = 0.03f; // Base slight recovery if condition change ends up positive
        private const float BASE_INJURY_RISK = 0.005f; // Base chance of minor injury per player per week

        // --- Modifiers ---
        // Intensity (Example multipliers)
        private const float LOW_INTENSITY_MOD = 0.6f;
        private const float NORMAL_INTENSITY_MOD = 1.0f;
        private const float HIGH_INTENSITY_MOD = 1.4f;

        // Facility Quality (Example scale 1-20 -> ~0.7 - 1.3 multiplier)
        private float FacilityModifier(int facilityLevel) => 0.7f + (Mathf.Clamp(facilityLevel, 1, 20) - 1) * (0.6f / 19.0f);

        // Coach Quality (Example scale 1-100 -> ~0.8 - 1.2 multiplier)
         private float CoachModifier(int coachSkill) => 0.8f + (Mathf.Clamp(coachSkill, 1, 100) - 1) * (0.4f / 99.0f);

        // Player Factors
        private const float AGE_GAIN_FACTOR_PEAK = 24f; // Age where gain factor starts decreasing
        private const float POTENTIAL_GAP_GAIN_THRESHOLD = 2; // Minimum potential gap for significant gain chance boost


        /// <summary>
        /// Simulates one week of training for the given team.
        /// </summary>
        /// <param name="team">The team data to process training for.</param>
        /// <param name="focus">The primary training focus for the week.</param>
        /// <param name="intensityLevel">Enum defining training intensity.</param>
        public void SimulateWeekTraining(TeamData team, TrainingFocus focus = TrainingFocus.General, Intensity intensityLevel = Intensity.Normal)
        {
            if (team == null || team.Roster == null) return;

            // Convert intensity enum to multiplier
            float intensityMod = GetIntensityMultiplier(intensityLevel);

            // Only log occasionally to reduce spam
             // if (GameManager.Instance?.TimeManager?.CurrentDate.Day <= 7) Debug.Log($"[TrainingSimulator] Simulating training week for {team.Name}. Focus: {focus}, Intensity: {intensityLevel}");

            // Get Team-level modifiers
            float facilityMod = FacilityModifier(team.Facilities?.TrainingFacilities ?? 10); // Use default 10 if null
            // TODO: Get relevant coach based on focus & team staff
            int relevantCoachSkill = GetRelevantCoachSkill(team, focus); // Placeholder function
            float coachMod = CoachModifier(relevantCoachSkill);

            // Calculate Intensity-specific modifiers
            float intensityGainMod = intensityMod;          // Intensity affects gains directly
            float intensityConditionMod = intensityMod;    // Intensity affects fatigue/recovery balance
            float intensityInjuryMod = intensityMod;        // Intensity affects injury risk

            // Process each player
            foreach (PlayerData player in team.Roster)
            {
                // Skip players with major injuries
                if (player.CurrentInjuryStatus == InjuryStatus.MajorInjury || player.CurrentInjuryStatus == InjuryStatus.CareerThreatening)
                {
                    // Player rests, slight condition recovery might happen elsewhere or subtly here
                    ApplyConditionChange(player, 0f, facilityMod, 0.1f); // Minimal intensity for recovery calculation
                    continue;
                }

                float currentGainMod = intensityGainMod;
                float currentCondMod = intensityConditionMod;
                float currentInjuryMod = intensityInjuryMod;

                // Adjust modifiers for minor/moderate injuries
                 if (player.CurrentInjuryStatus == InjuryStatus.ModerateInjury)
                 {
                     // No attribute gains, reduced intensity for condition/injury
                     currentGainMod = 0f;
                     currentCondMod *= 0.3f; // Significantly reduced workload
                     currentInjuryMod *= 0.5f; // Still some risk, but less intense
                 }
                 else if (player.CurrentInjuryStatus == InjuryStatus.MinorInjury)
                 {
                     // Reduced gains, slightly reduced intensity, higher re-injury risk multiplier
                     currentGainMod *= 0.4f;
                     currentCondMod *= 0.6f;
                     currentInjuryMod *= 1.3f; // Higher risk factor when training with minor injury
                 }

                // Apply weekly effects
                if (currentGainMod > 0) {
                     SimulateAttributeGains(player, focus, currentGainMod, facilityMod, coachMod);
                }
                ApplyConditionChange(player, currentCondMod, facilityMod, player.NaturalFitness);
                CheckForTrainingInjury(player, currentInjuryMod, facilityMod, player.Resilience, player.Condition);
            }

            // TODO: Generate and return/store a training report summary (e.g., who improved, any injuries)
            // return GenerateTrainingReport(...);
        }

        /// <summary>
        /// Gets the multiplier associated with the training intensity level.
        /// </summary>
        private float GetIntensityMultiplier(Intensity level) {
             switch(level) {
                 case Intensity.Low: return LOW_INTENSITY_MOD;
                 case Intensity.Normal: return NORMAL_INTENSITY_MOD;
                 case Intensity.High: return HIGH_INTENSITY_MOD;
                 default: return NORMAL_INTENSITY_MOD;
             }
        }

        /// <summary>Placeholder: Gets the relevant coaching skill for the focus.</summary>
        private int GetRelevantCoachSkill(TeamData team, TrainingFocus focus) {
            // TODO: Implement logic to find the best coach for the specific focus in team.Staff
            //       Consider Head Coach, Assistant, Youth Coach, specialist coaches.
            //       Return an average or the best relevant skill (e.g., CoachingAttack, CoachingFitness).
            StaffData coach = team.Staff?.FirstOrDefault(s => s.Role == StaffRole.HeadCoach || s.Role == StaffRole.AssistantCoach);
            int skill = coach?.CoachingTactical ?? 50; // Default to Tactical 50 if no coach found

            // Example refinement based on focus:
            switch(focus) {
                case TrainingFocus.Fitness: skill = coach?.CoachingFitness ?? 50; break;
                case TrainingFocus.AttackingMovement: skill = coach?.CoachingAttack ?? 50; break;
                case TrainingFocus.DefensiveShape: skill = coach?.CoachingDefense ?? 50; break;
                case TrainingFocus.ShootingPractice: skill = coach?.CoachingTechnical ?? 50; break; // Or Attack?
                case TrainingFocus.Goalkeeping: skill = coach?.CoachingGoalkeepers ?? 50; break;
                // General might use an average or tactical
            }
            return skill;
        }


        /// <summary>
        /// Simulates potential attribute gains for a player based on focus and modifiers.
        /// Uses a two-step probability: chance to check for gain, then chance to succeed (+1).
        /// </summary>
        private void SimulateAttributeGains(PlayerData player, TrainingFocus focus, float intensityMod, float facilityMod, float coachMod)
        {
            // Calculate overall gain chance *check* modifier
            // Younger players improve faster, closer to potential = slower.
            float ageFactor = Mathf.Clamp(1.0f + (AGE_GAIN_FACTOR_PEAK - player.Age) * 0.03f, 0.5f, 1.5f); // Bonus/penalty based on age vs peak
            int potentialGap = player.PotentialAbility - player.CurrentAbility;
            // Ensure potentialFactor doesn't penalize players already at PA from maintaining skills slightly
             float potentialFactor = (potentialGap <= 0) ? 0.1f : // Very low base factor if at/above PA
                                    Mathf.Clamp01(1.0f - Mathf.Exp(-potentialGap * 0.1f)); // Exponential approach to 1.0 as gap widens

            // TODO: Add Professionalism/Ambition factor?

            float overallCheckChanceMod = intensityMod * facilityMod * coachMod * ageFactor; // Potential factor applied later to *success* prob

            // Determine which attributes are relevant to the focus
            var relevantAttributes = GetRelevantAttributes(player, focus);

            // Loop through relevant attributes and check for gains
            foreach (var attrInfo in relevantAttributes)
            {
                 Func<int> getter = attrInfo.getter;
                 Action<int> setter = attrInfo.setter;
                 int currentValue = getter();
                 if (currentValue >= player.PotentialAbility && currentValue >= 99) continue; // Skip if already maxed or at PA ceiling

                 // 1. Check if this attribute is considered for improvement this week
                 float checkChance = BASE_ATTRIBUTE_GAIN_CHECK_CHANCE * overallCheckChanceMod;
                 // Bonus chance if attribute is low relative to potential?
                 checkChance *= Mathf.Lerp(1.0f, 1.5f, Mathf.Clamp01((float)(player.PotentialAbility - currentValue) / 30f)); // Higher chance if far below potential

                 if (UnityEngine.Random.value < Mathf.Clamp01(checkChance))
                 {
                     // 2. If check passes, calculate probability of actual +1 gain
                     float successProbability = GAIN_SUCCESS_PROBABILITY_BASE;
                     // Scale success probability heavily by potential gap and modifiers
                     successProbability *= Mathf.Clamp(overallCheckChanceMod, 0.5f, 1.5f); // Re-apply mods
                     successProbability *= Mathf.Clamp(potentialFactor * 5f, 0.1f, 2.0f); // Boost strongly by potential factor (up to 2x)
                     // Success more likely if attribute is significantly below current ability estimate? (Helps focus training)
                     successProbability *= Mathf.Lerp(1.0f, 1.3f, Mathf.Clamp01((float)(player.CurrentAbility - currentValue) / 20f));


                     if (UnityEngine.Random.value < Mathf.Clamp(successProbability, 0.01f, 0.9f)) // Ensure min 1%, max 90% success prob
                     {
                         int newValue = Mathf.Clamp(currentValue + 1, 1, 100); // Gain +1
                         // Ensure gain doesn't exceed Potential Ability
                         newValue = Mathf.Min(newValue, player.PotentialAbility);

                         if (newValue > currentValue) // Only apply if it actually increased
                         {
                             setter(newValue);
                             // Optional Log: Debug.Log($" -- {player.Name} improved {attrInfo.name} to {newValue} (Focus: {focus})");
                             player.CalculateCurrentAbility(); // Recalculate CA immediately for subsequent checks
                         }
                     }
                 }
            }
        }


        /// <summary>
        /// Simulates changes to player condition based on intensity, facilities, and fitness.
        /// </summary>
        private void ApplyConditionChange(PlayerData player, float intensityCondMod, float facilityMod, int naturalFitness)
        {
             // Higher intensity = more fatigue. Better facilities/natural fitness = less fatigue/faster recovery.
             float nfFactor = Mathf.Lerp(1.3f, 0.7f, naturalFitness / 100f); // High NF reduces fatigue impact
             float facilityEffect = Mathf.Lerp(1.1f, 0.9f, facilityMod); // Better facilities slightly reduce fatigue impact

             float conditionChange = BASE_CONDITION_CHANGE * intensityCondMod * nfFactor * facilityEffect;

             // Add slight potential recovery if net change is positive (low intensity/high NF)
             if (conditionChange > -0.01f) {
                  conditionChange += CONDITION_RECOVERY_FACTOR * (1.0f - player.Condition) * nfFactor; // Recover portion of missing condition
             }

             player.Condition = Mathf.Clamp(player.Condition + conditionChange, 0.1f, 1.0f); // Clamp condition
        }

        /// <summary>
        /// Checks if a player sustains an injury during training based on risk factors.
        /// </summary>
        private void CheckForTrainingInjury(PlayerData player, float intensityInjuryMod, float facilityMod, int resilience, float currentCondition)
        {
             // Higher intensity, lower resilience, poor facilities increase risk. Low condition increases risk.
             float resilienceFactor = Mathf.Lerp(1.5f, 0.5f, resilience / 100f); // High resilience reduces risk
             float conditionFactor = Mathf.Lerp(2.5f, 1.0f, currentCondition); // Low condition dramatically increases risk (up to 2.5x)
             float facilityEffect = Mathf.Lerp(1.2f, 0.8f, facilityMod); // Better facilities reduce risk

             float injuryRisk = BASE_INJURY_RISK * intensityInjuryMod * resilienceFactor * conditionFactor * facilityEffect;

             if (UnityEngine.Random.value < Mathf.Clamp(injuryRisk, 0.0001f, 0.1f)) // Clamp max risk per week?
             {
                 // Injury Occurred! Determine severity.
                 float severityRoll = UnityEngine.Random.value;
                 // Severity influenced by intensity and maybe resilience?
                 float highIntensityFactor = Mathf.Clamp01((intensityInjuryMod - 1.0f) * 0.5f); // Factor increases above normal intensity

                 // Define thresholds (these need balancing)
                 float majorThreshold = 0.03f + highIntensityFactor * 0.05f; // Base 3% + intensity mod
                 float moderateThreshold = 0.20f + highIntensityFactor * 0.15f; // Base 20% + intensity mod

                 if (severityRoll < majorThreshold) {
                     int duration = UnityEngine.Random.Range(60, 180);
                     player.InflictInjury(InjuryStatus.MajorInjury, duration, "Major Training Injury");
                 } else if (severityRoll < moderateThreshold) {
                     int duration = UnityEngine.Random.Range(14, 59);
                      player.InflictInjury(InjuryStatus.ModerateInjury, duration, "Moderate Training Injury");
                 } else {
                     int duration = UnityEngine.Random.Range(3, 13);
                      player.InflictInjury(InjuryStatus.MinorInjury, duration, "Minor Training Strain");
                 }
             }
        }


        /// <summary>
        /// Helper structure to hold attribute accessors and name for logging.
        /// </summary>
        private struct AttributeInfo {
             public string name; public Func<int> getter; public Action<int> setter;
             public AttributeInfo(string n, Func<int> g, Action<int> s) { name = n; getter = g; setter = s; }
        }

        /// <summary>
        /// Helper to get delegates for accessing attributes relevant to a training focus.
        /// </summary>
        private List<AttributeInfo> GetRelevantAttributes(PlayerData p, TrainingFocus focus)
        {
             var attributes = new List<AttributeInfo>();
             Action<string, Func<int>, Action<int>> AddAttribute = (n, getter, setter) => { attributes.Add(new AttributeInfo(n, getter, setter)); };

             // Define attributes relevant to each focus
             switch(focus)
             {
                 case TrainingFocus.General:
                     AddAttribute("ShootingAcc", () => p.ShootingAccuracy, v => p.ShootingAccuracy = v); AddAttribute("Passing", () => p.Passing, v => p.Passing = v); AddAttribute("Dribbling", () => p.Dribbling, v => p.Dribbling = v); AddAttribute("Technique", () => p.Technique, v => p.Technique = v);
                     AddAttribute("Tackling", () => p.Tackling, v => p.Tackling = v); AddAttribute("Blocking", () => p.Blocking, v => p.Blocking = v);
                     AddAttribute("Speed", () => p.Speed, v => p.Speed = v); AddAttribute("Agility", () => p.Agility, v => p.Agility = v); // Less focus on strength/stamina in general maybe?
                     AddAttribute("Stamina", () => p.Stamina, v => p.Stamina = v);
                     AddAttribute("Composure", () => p.Composure, v => p.Composure = v); AddAttribute("Concentration", () => p.Concentration, v => p.Concentration = v); AddAttribute("Anticipation", () => p.Anticipation, v => p.Anticipation = v); AddAttribute("DecisionMaking", () => p.DecisionMaking, v => p.DecisionMaking = v); AddAttribute("Teamwork", () => p.Teamwork, v => p.Teamwork = v);
                     break;
                 case TrainingFocus.Fitness:
                      AddAttribute("Speed", () => p.Speed, v => p.Speed = v); AddAttribute("Agility", () => p.Agility, v => p.Agility = v); AddAttribute("Strength", () => p.Strength, v => p.Strength = v); AddAttribute("Jumping", () => p.Jumping, v => p.Jumping = v); AddAttribute("Stamina", () => p.Stamina, v => p.Stamina = v); AddAttribute("NaturalFitness", () => p.NaturalFitness, v => p.NaturalFitness = v); AddAttribute("WorkRate", () => p.WorkRate, v => p.WorkRate = v); AddAttribute("Resilience", () => p.Resilience, v => p.Resilience = v); // Training fitness might improve injury resistance
                      break;
                 case TrainingFocus.AttackingMovement:
                      AddAttribute("Speed", () => p.Speed, v => p.Speed = v); AddAttribute("Agility", () => p.Agility, v => p.Agility = v); AddAttribute("Anticipation", () => p.Anticipation, v => p.Anticipation = v); AddAttribute("DecisionMaking", () => p.DecisionMaking, v => p.DecisionMaking = v); AddAttribute("Teamwork", () => p.Teamwork, v => p.Teamwork = v); AddAttribute("WorkRate", () => p.WorkRate, v => p.WorkRate = v); AddAttribute("Dribbling", () => p.Dribbling, v => p.Dribbling = v); AddAttribute("Passing", () => p.Passing, v => p.Passing = v); // Movement involves passing/dribbling into space
                      AddAttribute("Positioning", () => p.Positioning, v => p.Positioning = v); // Positioning is key for attack movement too
                      break;
                 case TrainingFocus.DefensiveShape:
                      AddAttribute("Tackling", () => p.Tackling, v => p.Tackling = v); AddAttribute("Blocking", () => p.Blocking, v => p.Blocking = v); AddAttribute("Positioning", () => p.Positioning, v => p.Positioning = v); AddAttribute("Anticipation", () => p.Anticipation, v => p.Anticipation = v); AddAttribute("Concentration", () => p.Concentration, v => p.Concentration = v); AddAttribute("DecisionMaking", () => p.DecisionMaking, v => p.DecisionMaking = v); AddAttribute("Teamwork", () => p.Teamwork, v => p.Teamwork = v); AddAttribute("Strength", () => p.Strength, v => p.Strength = v); // Strength relevant for holding shape
                      AddAttribute("Aggression", () => p.Aggression, v => p.Aggression = v); // Controlled aggression part of defense
                      break;
                 case TrainingFocus.ShootingPractice:
                      AddAttribute("ShootingPower", () => p.ShootingPower, v => p.ShootingPower = v); AddAttribute("ShootingAccuracy", () => p.ShootingAccuracy, v => p.ShootingAccuracy = v); AddAttribute("Technique", () => p.Technique, v => p.Technique = v); AddAttribute("Composure", () => p.Composure, v => p.Composure = v); // Composure under pressure when shooting
                      AddAttribute("Strength", () => p.Strength, v => p.Strength = v); // Strength contributes to power
                      break;
                 case TrainingFocus.Goalkeeping:
                      if (p.PrimaryPosition == PlayerPosition.Goalkeeper) {
                          AddAttribute("Reflexes", () => p.Reflexes, v => p.Reflexes = v); AddAttribute("Handling", () => p.Handling, v => p.Handling = v); AddAttribute("PositioningGK", () => p.PositioningGK, v => p.PositioningGK = v); AddAttribute("OneOnOnes", () => p.OneOnOnes, v => p.OneOnOnes = v); AddAttribute("PenaltySaving", () => p.PenaltySaving, v => p.PenaltySaving = v); AddAttribute("Throwing", () => p.Throwing, v => p.Throwing = v); AddAttribute("Communication", () => p.Communication, v => p.Communication = v); AddAttribute("Agility", () => p.Agility, v => p.Agility = v); AddAttribute("Jumping", () => p.Jumping, v => p.Jumping = v); AddAttribute("Concentration", () => p.Concentration, v => p.Concentration = v); AddAttribute("Composure", () => p.Composure, v => p.Composure = v);
                      } else {
                           // Non-GKs doing GK training? Maybe slight general benefit? Fallback to general.
                           return GetRelevantAttributes(p, TrainingFocus.General);
                      }
                      break;
                 case TrainingFocus.SetPieces:
                       AddAttribute("Passing", () => p.Passing, v => p.Passing = v); // For throwers/passers
                       AddAttribute("ShootingAccuracy", () => p.ShootingAccuracy, v => p.ShootingAccuracy = v); // For penalty takers
                       AddAttribute("Technique", () => p.Technique, v => p.Technique = v);
                       AddAttribute("DecisionMaking", () => p.DecisionMaking, v => p.DecisionMaking = v); // Choosing right set piece option
                       AddAttribute("Composure", () => p.Composure, v => p.Composure = v); // For penalty takers
                       break;
                 case TrainingFocus.YouthDevelopment:
                       // This focus might apply different modifiers or target different attributes
                       // For now, treat as General for attribute gains. Logic might differ elsewhere.
                       return GetRelevantAttributes(p, TrainingFocus.General);

                 default: // Fallback to General
                     return GetRelevantAttributes(p, TrainingFocus.General);
             }
             return attributes;
        }

        /// <summary> Enum for Training Intensity </summary>
        public enum Intensity { Low, Normal, High }

    } // End TrainingSimulator Class
}