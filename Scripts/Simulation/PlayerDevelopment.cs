using HandballManager.Data;
using HandballManager.Core; // For Enums if needed later
using UnityEngine; // For Debug.Log, Mathf, Random
using System; // For Math

namespace HandballManager.Simulation
{
    /// <summary>
    /// Handles long-term player attribute development and regression,
    /// typically processed annually during the off-season.
    /// </summary>
    public class PlayerDevelopment
    {
        // --- Age Constants ---
        private const int PEAK_AGE_START = 24; // Start of typical peak years
        private const int PEAK_AGE_END = 29;   // End of typical peak years
        private const int DECLINE_AGE_START = 30;// Age where physical decline often becomes noticeable

        // --- Development Modifiers ---
        private const float DEVELOPING_BASE_FACTOR = 1.0f; // Base chance/magnitude multiplier for young players
        private const float PEAK_BASE_FACTOR = 0.3f;       // Base chance/magnitude multiplier for peak players
        private const float DECLINE_BASE_FACTOR = -0.8f;     // Base negative factor for declining players
        private const float POTENTIAL_GAP_FACTOR = 0.05f;   // How much each point of potential gap influences development chance/magnitude
        private const float AGE_DECLINE_RAMP_FACTOR = 0.1f; // How much the decline factor increases each year past DECLINE_AGE_START

        // --- Attribute Category Modifiers ---
        // How susceptible each category is to change in different phases
        // Values > 1 mean more likely/bigger change, < 1 mean less likely/smaller change
        private static readonly DevelopmentModifiers DevelopingModifiers = new DevelopmentModifiers(physical: 1.2f, technical: 1.0f, mental: 0.8f, goalkeeping: 1.0f);
        private static readonly DevelopmentModifiers PeakModifiers = new DevelopmentModifiers(physical: 0.4f, technical: 1.0f, mental: 1.1f, goalkeeping: 1.0f); // Physical slows, mental can grow
        private static readonly DevelopmentModifiers DecliningModifiers = new DevelopmentModifiers(physical: 1.5f, technical: 0.8f, mental: 0.6f, goalkeeping: 0.9f); // Physical declines fastest

        /// <summary>
        /// Processes annual development changes for a single player.
        /// Modifies player attributes based on age, potential, and development phase.
        /// Should be called once per year, typically during the off-season.
        /// </summary>
        /// <param name="player">The player data to process development for.</param>
        public void ProcessAnnualDevelopment(PlayerData player)
        {
            if (player == null) return;

            // --- Pre-Development State ---
            int previousAge = player.Age;
            int previousCA = player.CalculateCurrentAbility(); // Ensure CA is up-to-date before development

            // --- Age Increment ---
            player.Age++;

            // Optional: Check for retirement based on age/injury/other factors
            // if (ShouldRetire(player)) { /* Handle retirement */ return; }

            // --- Determine Development Phase & Factors ---
            DevelopmentPhase phase = GetDevelopmentPhase(player.Age);
            DevelopmentModifiers categoryModifiers = GetCategoryModifiers(phase);
            float baseDevelopmentFactor = GetBaseDevelopmentFactor(phase, player.Age);
            int potentialGap = player.PotentialAbility - previousCA;

            // --- Log Initial State ---
            // Debug.Log($"[PlayerDevelopment] Processing: {player.FullName} | Age: {previousAge}->{player.Age} | Phase: {phase} | CA: {previousCA} | PA: {player.PotentialAbility} | Gap: {potentialGap}");

            // --- Apply Attribute Changes ---
            // Apply changes category by category using modifiers
            ApplyAttributeChanges(player, phase, categoryModifiers.Physical, baseDevelopmentFactor, potentialGap, AttributeCategory.Physical);
            ApplyAttributeChanges(player, phase, categoryModifiers.Technical, baseDevelopmentFactor, potentialGap, AttributeCategory.Technical);
            ApplyAttributeChanges(player, phase, categoryModifiers.Mental, baseDevelopmentFactor, potentialGap, AttributeCategory.Mental);
            if (player.PrimaryPosition == PlayerPosition.Goalkeeper) {
                ApplyAttributeChanges(player, phase, categoryModifiers.Goalkeeping, baseDevelopmentFactor, potentialGap, AttributeCategory.Goalkeeping);
            }

            // --- Post-Development State ---
            int newCA = player.CalculateCurrentAbility(); // Recalculate CA based on new attributes

            // Optional: Add special events like breakthroughs or setbacks
            // HandleSpecialEvents(player, previousCA, newCA, phase);

            // --- Log Final State ---
             Debug.Log($"[PlayerDevelopment] Completed: {player.FullName} | New CA: {newCA} (Change: {newCA - previousCA:+0;-#})");

            // TODO: Integrate more factors in ApplyAttributeChanges/ApplyChange:
            // - Training performance over the season.
            // - Match experience (minutes played, average rating).
            // - Quality of coaching staff and facilities.
            // - Player personality traits (Professionalism, Ambition, Determination).
            // - Major injuries potentially reducing PA or specific attributes permanently/temporarily.
        }


        /// <summary>
        /// Determines the player's current development stage based on age.
        /// </summary>
        private DevelopmentPhase GetDevelopmentPhase(int age)
        {
            if (age < PEAK_AGE_START) return DevelopmentPhase.Developing;
            if (age <= PEAK_AGE_END) return DevelopmentPhase.Peak;
            return DevelopmentPhase.Declining;
        }

        /// <summary>Gets the base development factor based on phase and age (for decline ramp).</summary>
        private float GetBaseDevelopmentFactor(DevelopmentPhase phase, int age) {
             switch (phase) {
                  case DevelopmentPhase.Developing: return DEVELOPING_BASE_FACTOR;
                  case DevelopmentPhase.Peak: return PEAK_BASE_FACTOR;
                  case DevelopmentPhase.Declining:
                       // Decline gets harsher with age
                       return DECLINE_BASE_FACTOR - ((age - DECLINE_AGE_START) * AGE_DECLINE_RAMP_FACTOR);
                  default: return 0f;
             }
        }

         /// <summary>Gets the category-specific modifiers based on phase.</summary>
        private DevelopmentModifiers GetCategoryModifiers(DevelopmentPhase phase) {
             switch (phase) {
                  case DevelopmentPhase.Developing: return DevelopingModifiers;
                  case DevelopmentPhase.Peak: return PeakModifiers;
                  case DevelopmentPhase.Declining: return DecliningModifiers;
                  default: return new DevelopmentModifiers(1f, 1f, 1f, 1f); // Should not happen
             }
        }

        /// <summary>
        /// Applies changes to a specific category of attributes.
        /// </summary>
        private void ApplyAttributeChanges(PlayerData player, DevelopmentPhase phase, float categoryModifier, float baseFactor, int potentialGap, AttributeCategory category)
        {
             // Combine base factor and category factor
             float combinedFactor = baseFactor * categoryModifier;

             // Get attribute setters for the specified category
             var attributeSetters = GetAttributeSetters(player, category);

             // Apply change to each attribute in the category
             foreach (var kvp in attributeSetters)
             {
                 int currentValue = kvp.Value(); // Get current value using getter Func
                 int newValue = ApplyChange(currentValue, phase, combinedFactor, potentialGap);
                 if (newValue != currentValue)
                 {
                     kvp.Key(newValue); // Apply new value using setter Action
                     // Optional: Log individual attribute change
                     // Debug.Log($"    {player.Name} -> {category} ({kvp.Key.Method.Name.Replace("set_","")}): {currentValue} -> {newValue}");
                 }
             }
        }


        /// <summary>
        /// Calculates the new value for a single attribute based on development factors.
        /// </summary>
        /// <param name="currentValue">The current attribute value (1-100).</param>
        /// <param name="phase">The player's development phase.</param>
        /// <param name="factor">Combined development factor (base * category, positive for growth, negative for decline).</param>
        /// <param name="potentialGap">Difference between PA and CA (positive if room to grow).</param>
        /// <returns>The potentially modified attribute value (clamped 1-100).</returns>
        private int ApplyChange(int currentValue, DevelopmentPhase phase, float factor, int potentialGap)
        {
             // --- Determine Likelihood of Change ---
             // Base chance influenced by the magnitude of the factor
             float changeProbability = 0.1f + Mathf.Abs(factor) * 0.15f; // Base 10% + 15% per point of factor magnitude
             // Potential gap significantly increases chance ONLY during Developing/Peak phases if factor is positive
             if (phase != DevelopmentPhase.Declining && factor > 0) {
                 changeProbability += Mathf.Max(0, potentialGap) * POTENTIAL_GAP_FACTOR * 0.5f; // Gap adds to probability
             }
             // Clamp probability
             changeProbability = Mathf.Clamp(changeProbability, 0.05f, 0.8f); // Ensure minimum 5% and max 80% chance

             if (UnityEngine.Random.value > changeProbability)
             {
                 return currentValue; // No change this year for this attribute
             }

             // --- Determine Magnitude of Change ---
             int changeAmount = 0;

             if (phase == DevelopmentPhase.Declining)
             {
                 // Decline: Magnitude based on negative factor (gets larger with age)
                 // Factor is already negative here. Use Abs for magnitude calculation.
                 float declineMagnitude = Mathf.Abs(factor) * UnityEngine.Random.Range(0.6f, 1.4f);
                 changeAmount = -Mathf.RoundToInt(declineMagnitude);
                 // Ensure minimum decline of -1 if a change was triggered
                 if (changeAmount == 0) changeAmount = -1;
             }
             else // Developing or Peak phase
             {
                 // Growth: Possible only if potential gap exists OR factor allows minimal change at peak
                 if (potentialGap > 0 && factor > 0)
                 {
                     // Magnitude influenced by factor AND remaining potential gap
                     float growthMagnitude = factor * UnityEngine.Random.Range(0.7f, 1.3f);
                     // Scale magnitude down significantly as player approaches potential
                     growthMagnitude *= Mathf.Clamp01((float)potentialGap / 15f); // Stronger effect when gap is large (>15), tapers off near PA
                     changeAmount = Mathf.RoundToInt(growthMagnitude);
                     // Small chance of +1 even with tiny calculated magnitude if factor is positive
                     if (changeAmount == 0 && factor > 0.1f && UnityEngine.Random.value < 0.2f) changeAmount = 1;
                 }
                 else if (phase == DevelopmentPhase.Peak && factor > 0) // At peak, potential for small gains even if CA=PA? (e.g. mental refinement)
                 {
                      // Tiny chance of +1 for non-physical attributes? Or handle via base factor being low?
                      // Base factor is low, so growthMagnitude will be small. Keep logic above.
                      changeAmount = 0; // No growth if gap <= 0 generally
                 }
                 else // Should not happen if factor is positive, but safety check
                 {
                      changeAmount = 0;
                 }

                 // Possible slight physical decline at end of peak?
                 // This is now handled by the category modifiers having lower physical factor during peak/decline.
            }

             int newValue = currentValue + changeAmount;

             // Clamp the result between 1 and 100 (attribute limits)
             newValue = Mathf.Clamp(newValue, 1, 100);

             return newValue;
        }


        // --- Helper Methods and Enums ---

        /// <summary>Holds category-specific development modifiers.</summary>
        private struct DevelopmentModifiers {
            public float Physical; public float Technical; public float Mental; public float Goalkeeping;
            public DevelopmentModifiers(float physical, float technical, float mental, float goalkeeping) {
                 Physical = physical; Technical = technical; Mental = mental; Goalkeeping = goalkeeping;
            }
        }

        /// <summary>Attribute categories for applying specific modifiers.</summary>
        private enum AttributeCategory { Physical, Technical, Mental, Goalkeeping }

        /// <summary>Player development phases based on age.</summary>
        private enum DevelopmentPhase { Developing, Peak, Declining }

        /// <summary>
        /// Gets a dictionary mapping attribute setter Actions to getter Funcs for a given category.
        /// This avoids reflection but requires manual maintenance.
        /// </summary>
        private Dictionary<Action<int>, Func<int>> GetAttributeSetters(PlayerData p, AttributeCategory category)
        {
            var setters = new Dictionary<Action<int>, Func<int>>();
            switch (category)
            {
                case AttributeCategory.Physical:
                    setters.Add(v => p.Speed = v, () => p.Speed);
                    setters.Add(v => p.Agility = v, () => p.Agility);
                    setters.Add(v => p.Strength = v, () => p.Strength);
                    setters.Add(v => p.Jumping = v, () => p.Jumping);
                    setters.Add(v => p.Stamina = v, () => p.Stamina);
                    setters.Add(v => p.NaturalFitness = v, () => p.NaturalFitness);
                    setters.Add(v => p.Resilience = v, () => p.Resilience); // Injury resistance might change slightly
                    break;
                case AttributeCategory.Technical:
                    setters.Add(v => p.ShootingPower = v, () => p.ShootingPower);
                    setters.Add(v => p.ShootingAccuracy = v, () => p.ShootingAccuracy);
                    setters.Add(v => p.Passing = v, () => p.Passing);
                    setters.Add(v => p.Dribbling = v, () => p.Dribbling);
                    setters.Add(v => p.Technique = v, () => p.Technique);
                    setters.Add(v => p.Tackling = v, () => p.Tackling);
                    setters.Add(v => p.Blocking = v, () => p.Blocking);
                    break;
                case AttributeCategory.Mental:
                    setters.Add(v => p.Aggression = v, () => p.Aggression); // Less likely to change drastically? Apply lower factor implicitly?
                    setters.Add(v => p.Bravery = v, () => p.Bravery);
                    setters.Add(v => p.Composure = v, () => p.Composure);
                    setters.Add(v => p.Concentration = v, () => p.Concentration);
                    setters.Add(v => p.Anticipation = v, () => p.Anticipation);
                    setters.Add(v => p.DecisionMaking = v, () => p.DecisionMaking);
                    setters.Add(v => p.Teamwork = v, () => p.Teamwork);
                    setters.Add(v => p.WorkRate = v, () => p.WorkRate);
                    setters.Add(v => p.Leadership = v, () => p.Leadership);
                    setters.Add(v => p.Positioning = v, () => p.Positioning);
                    break;
                case AttributeCategory.Goalkeeping:
                    setters.Add(v => p.Reflexes = v, () => p.Reflexes);
                    setters.Add(v => p.Handling = v, () => p.Handling);
                    setters.Add(v => p.PositioningGK = v, () => p.PositioningGK);
                    setters.Add(v => p.OneOnOnes = v, () => p.OneOnOnes);
                    setters.Add(v => p.PenaltySaving = v, () => p.PenaltySaving);
                    setters.Add(v => p.Throwing = v, () => p.Throwing);
                    setters.Add(v => p.Communication = v, () => p.Communication);
                    break;
            }
            return setters;
        }

        // Optional: Placeholder for retirement logic
        // private bool ShouldRetire(PlayerData player) { return player.Age > 36 && UnityEngine.Random.value < 0.1f; }

        // Optional: Placeholder for special event logic
        // private void HandleSpecialEvents(PlayerData player, int oldCA, int newCA, DevelopmentPhase phase) { /* Check for breakthroughs etc. */ }

    }
}