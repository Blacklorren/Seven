using HandballManager.Core; // For Enums
using System; // For Serializable
using System.Collections.Generic; // For potentially storing player instructions

namespace HandballManager.Gameplay
{
    /// <summary>
    /// Defines the tactical settings for a team.
    /// This can be saved, loaded, and assigned to a TeamData object.
    /// </summary>
    [Serializable]
    public class Tactic
    {
        public string TacticName { get; set; } = "Default Tactic";
        public Guid TacticID { get; private set; } = Guid.NewGuid(); // Unique ID for saving/loading specific tactics

        // --- Core Formations ---
        /// <summary>Formation used during offensive phases. Could be enum or struct later.</summary>
        public string OffensiveFormation { get; set; } = "Standard Backcourt"; // Example string, could be more structured
        /// <summary>The primary defensive system employed.</summary>
        public DefensiveSystem DefensiveSystem { get; set; } = DefensiveSystem.SixZero;

        // --- General Team Instructions ---
        /// <summary>Speed at which the team builds up attacks.</summary>
        public TacticPace Pace { get; set; } = TacticPace.Normal;
        /// <summary>General aggression/intensity level (0.0 = Passive, 1.0 = Very Aggressive). Affects tackling, pressing.</summary>
        public float TeamAggressionLevel { get; set; } = 0.5f;
        /// <summary>Primary area to focus offensive plays (e.g., through wings, backcourt players, pivot).</summary>
        public OffensiveFocusPlay FocusPlay { get; set; } = OffensiveFocusPlay.Balanced;
        /// <summary>How often players should attempt risky/creative passes or shots (0.0 = Safe, 1.0 = High Risk).</summary>
        public float RiskTakingLevel { get; set; } = 0.5f;
        /// <summary>How quickly the team transitions from defense to attack (Fast Break propensity).</summary>
        public TacticPace CounterAttackSpeed { get; set; } = TacticPace.Normal;
        /// <summary>How high up the pitch the defensive line pushes (0.0 = Deep/Passive, 1.0 = High Press).</summary>
        public float DefensiveLineHeight { get; set; } = 0.5f;
         /// <summary>Width of the team shape in defense (0.0 = Narrow, 1.0 = Wide).</summary>
        public float DefensiveWidth { get; set; } = 0.5f;
        /// <summary>Whether to use man-marking or zonal marking primarily.</summary>
        public MarkingType MarkingStyle { get; set; } = MarkingType.Zonal;

        // --- Player Instructions (Optional - advanced feature) ---
        // This might map PlayerID or Position to specific overrides
        // public Dictionary<int, PlayerInstructionOverrides> PlayerInstructions { get; set; } = new Dictionary<int, PlayerInstructionOverrides>();
        // public Dictionary<PlayerPosition, PlayerInstructionOverrides> PositionalInstructions { get; set; } = new Dictionary<PlayerPosition, PlayerInstructionOverrides>();

        // --- Set Pieces (Placeholder) ---
        public int PrimaryPenaltyTakerPlayerID { get; set; } = -1; // -1 = Default/Coach picks
        // public List<string> OffensiveSetPlays { get; set; } // Names or IDs of predefined plays
        // public List<string> DefensiveSetPlays { get; set; }

        // --- Constructor ---
        public Tactic()
        {
            // Initialize with default values (already done with inline initializers)
        }

         public override string ToString()
         {
             return $"{TacticName} (Off: {OffensiveFormation}, Def: {DefensiveSystem}, Pace: {Pace})";
         }
    }

    /// <summary>
    /// Enum for marking types.
    /// </summary>
    [Serializable]
    public enum MarkingType
    {
        Zonal,
        ManMarking
    }

    // --- Example Structure for Player-Specific Instructions (Optional) ---
    /*
    [Serializable]
    public class PlayerInstructionOverrides
    {
        public bool? UseAggressiveTackling { get; set; } // Nullable bool means use team default
        public OffensiveFocusPlay? OverrideFocusPlay { get; set; } // e.g., Force winger to cut inside
        public float? OverrideRiskTaking { get; set; }
        // Add more specific instructions...
    }
    */
}