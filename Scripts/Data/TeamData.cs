using System.Collections.Generic;
using HandballManager.Gameplay; // For Tactic
using HandballManager.Core; // For Enums?
using UnityEngine; // Required for Debug.Log, potentially Serializable
using System; // For Serializable
using System.Linq; // For Linq operations on lists

namespace HandballManager.Data
{
    /// <summary>
    /// Represents all data associated with a single handball team.
    /// </summary>
    [Serializable]
    public class TeamData
    {
        public int TeamID { get; set; }
        public string Name { get; set; } = "Default Team";
        public string ShortName { get; set; } = "DEF"; // 3-letter short name for scoreboards etc.
        /// <summary>Team reputation influencing transfers, sponsorships etc. (e.g., 1-10000).</summary>
        public int Reputation { get; set; } = 1000;
        public List<PlayerData> Roster { get; set; } = new List<PlayerData>();
        public List<StaffData> Staff { get; set; } = new List<StaffData>();
        public List<Tactic> Tactics { get; set; } = new List<Tactic>(); // Store multiple tactics
        public Guid? CurrentTacticID { get; set; } // ID of the currently selected tactic from the Tactics list

        // Financial Data
        public float Budget { get; set; } = 500000f; // Available transfer/wage budget balance
        public float WageBudget { get; set; } = 60000f; // The allowed weekly/monthly wage spending limit set by board
        public float CurrentWageBill { get; private set; } // Calculated weekly/monthly wage total (private set)
        public float TransferBudget { get; set; } = 200000f; // Portion of budget specifically for transfers (optional complexity)

        // Club Info
        public int? LeagueID { get; set; } // ID of the league the team plays in (nullable if unassigned)
        public ClubFacilities Facilities { get; set; } = new ClubFacilities(); // Training, Youth facilities etc.
        // public StadiumData Stadium { get; set; } // Separate class/struct for stadium info?
        // public Color PrimaryColor { get; set; } = Color.white; // Team colors for UI/kits
        // public Color SecondaryColor { get; set; } = Color.black;

        // --- Properties ---

        /// <summary>
        /// Gets the currently active tactic from the Tactics list.
        /// Returns the first tactic if the ID is not found or not set.
        /// </summary>
        public Tactic CurrentTactic
        {
            get
            {
                if (CurrentTacticID.HasValue && Tactics != null)
                {
                    Tactic foundTactic = Tactics.FirstOrDefault(t => t.TacticID == CurrentTacticID.Value);
                    if (foundTactic != null) return foundTactic;
                }
                // Fallback: return first tactic or a new default one
                return Tactics?.FirstOrDefault() ?? new Tactic();
            }
            set // Allow setting by assigning a Tactic object; finds or adds it and sets the ID.
            {
                if (value == null)
                {
                    CurrentTacticID = null;
                    return;
                }
                if (Tactics == null) Tactics = new List<Tactic>();

                // Check if this tactic (by ID) is already in the list
                int index = Tactics.FindIndex(t => t.TacticID == value.TacticID);
                if (index == -1)
                {
                    // Tactic not found, add it to the list
                    Tactics.Add(value);
                } else {
                    // Tactic found, replace it in case it was modified
                    Tactics[index] = value;
                }
                CurrentTacticID = value.TacticID;
            }
        }


        // --- Constructor ---
        public TeamData()
        {
            // Ensure there's at least one default tactic when a team is created
            if (Tactics == null || Tactics.Count == 0)
            {
                 Tactics = new List<Tactic>();
                 Tactic defaultTactic = new Tactic { TacticName = "Default Balanced" };
                 Tactics.Add(defaultTactic);
                 CurrentTacticID = defaultTactic.TacticID;
            }
             TeamID = GetNextUniqueID(); // Assign unique ID
             if (ShortName == "DEF" && Name != "Default Team") // Basic auto short name
             {
                 ShortName = Name.Length >= 3 ? Name.Substring(0, 3).ToUpper() : Name.ToUpper();
             }
        }


        // --- Methods ---

        /// <summary>
        /// Calculates the total weekly/monthly wage bill for the team based on players and staff.
        /// Call this method whenever roster/staff wages change.
        /// </summary>
        /// <returns>The calculated total wage bill.</returns>
        public float UpdateWageBill()
        {
            float totalWages = 0;
            if (Roster != null)
            {
                foreach (var player in Roster)
                {
                    if(player != null) totalWages += player.Wage;
                }
            }
            if (Staff != null)
            {
                foreach (var staffMember in Staff)
                {
                     if(staffMember != null) totalWages += staffMember.Wage;
                }
            }
            CurrentWageBill = totalWages;
             // Debug.Log($"{Name} updated wage bill: {CurrentWageBill:C}");
             return CurrentWageBill;
        }

        /// <summary>
        /// Adds a player to the team's roster if not already present.
        /// Updates player's team ID and recalculates wage bill.
        /// </summary>
        public bool AddPlayer(PlayerData player)
        {
            if (player == null) return false;
            if (Roster == null) Roster = new List<PlayerData>();

            if (!Roster.Exists(p => p.PlayerID == player.PlayerID))
            {
                Roster.Add(player);
                player.CurrentTeamID = this.TeamID; // Assign player to this team
                UpdateWageBill(); // Recalculate wages
                Debug.Log($"Player {player.Name} (ID: {player.PlayerID}) added to team {this.Name} (ID: {this.TeamID}).");
                return true;
            }
            else
            {
                 Debug.LogWarning($"Player {player.Name} (ID: {player.PlayerID}) is already on team {this.Name}.");
                 // Ensure team ID is correct if already present
                 if(player.CurrentTeamID != this.TeamID) player.CurrentTeamID = this.TeamID;
                 return false;
            }
        }

        /// <summary>
        /// Removes a player from the team's roster by PlayerData object.
        /// Updates player's team ID to null and recalculates wage bill.
        /// </summary>
         public bool RemovePlayer(PlayerData player)
        {
            if (player == null || Roster == null) return false;
            return RemovePlayerByID(player.PlayerID);
        }

        /// <summary>
        /// Removes a player from the team's roster by ID.
        /// Updates player's team ID to null and recalculates wage bill.
        /// </summary>
         public bool RemovePlayerByID(int playerID)
         {
             if (Roster == null) return false;

             PlayerData playerToRemove = Roster.FirstOrDefault(p => p.PlayerID == playerID);

             if (playerToRemove != null)
             {
                 Roster.Remove(playerToRemove);
                 playerToRemove.CurrentTeamID = null; // Player becomes a free agent (or moves via TransferManager)
                 UpdateWageBill(); // Recalculate wages
                 Debug.Log($"Player {playerToRemove.Name} (ID: {playerID}) removed from team {this.Name}.");
                 return true;
             }
              Debug.LogWarning($"Player with ID {playerID} not found on team {this.Name}.");
             return false;
         }

         // TODO: Add methods for managing staff (AddStaff, RemoveStaff) similar to players.
         // TODO: Add methods for managing tactics (AddTactic, RemoveTactic, SelectTactic).
         // TODO: Add methods for financial adjustments (e.g., AdjustBudgetAllocation between wage/transfer).
         // TODO: Method to get average player rating or other team stats.


         // Basic placeholder for unique IDs - replace with a robust system if needed
         private static int _nextId = 1;
         private static int GetNextUniqueID() { return _nextId++; }
    }


    /// <summary>
    /// Represents the quality levels of various club facilities.
    /// </summary>
    [Serializable]
    public class ClubFacilities
    {
        [Range(1, 20)] public int TrainingFacilities { get; set; } = 10; // Scale 1-20 like FM?
        [Range(1, 20)] public int YouthFacilities { get; set; } = 10;
        [Range(1, 20)] public int YouthRecruitment { get; set; } = 10; // Quality of youth scouting network
        [Range(1, 20)] public int YouthCoaching { get; set; } = 10; // Quality specifically for youth teams
        [Range(1, 20)] public int DataAnalysisFacilities { get; set; } = 10; // For scouting/match analysis
    }
}