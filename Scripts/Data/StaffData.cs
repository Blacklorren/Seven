using HandballManager.Core; // For Enums like StaffRole
using System; // For Serializable, DateTime
using UnityEngine; // For Mathf.Clamp

namespace HandballManager.Data
{
    /// <summary>
    /// Represents data for a non-playing staff member (Coach, Physio, Scout).
    /// </summary>
    [Serializable]
    public class StaffData
    {
        public int StaffID { get; set; } // Or Guid
        public string Name { get; set; } = "Default Staff";
        public int Age { get; set; } = 35;
        public string Nationality { get; set; } = "Unknown";
        public StaffRole Role { get; set; } = StaffRole.AssistantCoach;
        public int? CurrentTeamID { get; set; } // Nullable if unemployed
        public DateTime ContractExpiryDate { get; set; } = DateTime.MinValue;
        public float Wage { get; set; } = 2000f; // Per week/month? Standardize.
        public int Reputation { get; set; } = 50; // Staff reputation (1-100)

        // --- Role-Specific Skills (Scale 1-100 suggested, could be 1-20 like FM) ---
        // Use 1-100 for consistency with player attributes for now.

        // Coaching Attributes
        [Range(1, 100)] public int CoachingAttack { get; set; } = 50;
        [Range(1, 100)] public int CoachingDefense { get; set; } = 50;
        [Range(1, 100)] public int CoachingGoalkeepers { get; set; } = 50;
        [Range(1, 100)] public int CoachingFitness { get; set; } = 50;
        [Range(1, 100)] public int CoachingTechnical { get; set; } = 50; // Skill training
        [Range(1, 100)] public int CoachingTactical { get; set; } = 50; // Tactical understanding/instruction
        [Range(1, 100)] public int CoachingYouth { get; set; } = 50; // Working with young players

        // Physio Attributes
        [Range(1, 100)] public int Physiotherapy { get; set; } = 50; // Effectiveness in treating injuries & prevention
        [Range(1, 100)] public int SportsScience { get; set; } = 50; // Injury prevention, fitness regimes

        // Scout Attributes
        [Range(1, 100)] public int JudgingPlayerAbility { get; set; } = 50; // Assessing current skill level accurately
        [Range(1, 100)] public int JudgingPlayerPotential { get; set; } = 50; // Assessing future potential accurately
        [Range(1, 100)] public int TacticalKnowledge { get; set; } = 50; // Understanding tactical nuances (useful for scouts/coaches)
        // public ScoutingRange NetworkRange { get; set; } // How wide their network is (enum: Local, Regional, National, Continental, Worldwide)

        // Common Mental/Management Attributes
        [Range(1, 100)] public int Adaptability { get; set; } = 50; // Coping with changes, new environments
        [Range(1, 100)] public int Determination { get; set; } = 50; // Drive to succeed
        [Range(1, 100)] public int ManManagement { get; set; } = 50; // Dealing with players/staff personalities & morale
        [Range(1, 100)] public int LevelOfDiscipline { get; set; } = 50; // Strictness vs leniency
        [Range(1, 100)] public int Motivation { get; set; } = 50; // Ability to motivate players

        // Potential (similar to players, for staff development?)
        // public int PotentialRating { get; set; } = 60;

        // --- Constructor ---
        public StaffData()
        {
           // Default initialization if needed (mostly handled by inline initializers)
           StaffID = GetNextUniqueID(); // Assign unique ID on creation
        }

        // TODO: Add methods if needed, e.g.,
        // - Calculate overall effectiveness for a specific role based on weighted attributes.
        // - Method for staff development/attribute changes over time.

         // Basic placeholder for unique IDs - replace with a robust system if needed
         private static int _nextId = 5000; // Start staff IDs high to avoid player collision
         private static int GetNextUniqueID() { return _nextId++; }
    }

    /* Example ScoutingRange Enum
    public enum ScoutingRange {
        Local,
        Regional,
        National,
        Continental,
        Worldwide
    }
    */
}