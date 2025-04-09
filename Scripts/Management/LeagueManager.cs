using System;
using System.Collections.Generic;
using System.Linq;
using HandballManager.Data; // For MatchResult, TeamData
using UnityEngine;

namespace HandballManager.Management
{
    /// <summary>
    /// Represents a single entry in the league table.
    /// Needs to be Serializable if saved directly.
    /// </summary>
    [Serializable]
    public class LeagueStandingEntry
    {
        public int TeamID;
        public string TeamName;
        public int Played = 0;
        public int Wins = 0;
        public int Draws = 0;
        public int Losses = 0;
        public int GoalsFor = 0;
        public int GoalsAgainst = 0;
        public int GoalDifference => GoalsFor - GoalsAgainst;
        public int Points => (Wins * 2) + (Draws * 1); // Standard Handball points (2 for win, 1 for draw)

        // Add constructor?
    }

    /// <summary>
    /// Manages league standings, processes results, and handles season finalization.
    /// Placeholder implementation.
    /// </summary>
    public class LeagueManager
    {
        // Stores league standings. Key is LeagueID, Value is the list of entries for that league.
        private Dictionary<int, List<LeagueStandingEntry>> _leagueTables = new Dictionary<int, List<LeagueStandingEntry>>();

        // Optional: Store top scorers etc.
        // private Dictionary<int, List<PlayerSeasonStats>> _playerStats = new Dictionary<int, List<PlayerSeasonStats>>();

        /// <summary>
        /// Processes a match result, updating the relevant league table.
        /// </summary>
        /// <param name="result">The completed match result.</param>
        public void ProcessMatchResult(MatchResult result)
        {
            // TODO: Determine the league ID for this match (e.g., from teams or a central match registry)
            int leagueId = 1; // Placeholder - Assume league 1 for now
             var homeTeam = HandballManager.Core.GameManager.Instance?.AllTeams.FirstOrDefault(t => t.TeamID == result.HomeTeamID);
             if (homeTeam != null) leagueId = homeTeam.LeagueID ?? 1; // Try to get from team data


            if (!_leagueTables.ContainsKey(leagueId))
            {
                InitializeLeagueTable(leagueId); // Initialize if not present
            }

            List<LeagueStandingEntry> table = _leagueTables[leagueId];

            LeagueStandingEntry homeEntry = table.FirstOrDefault(e => e.TeamID == result.HomeTeamID);
            LeagueStandingEntry awayEntry = table.FirstOrDefault(e => e.TeamID == result.AwayTeamID);

            if (homeEntry == null || awayEntry == null)
            {
                Debug.LogWarning($"[LeagueManager] Could not find team entries in league table {leagueId} for match: {result}. Re-initializing table.");
                // Attempt to re-initialize or handle error
                 InitializeLeagueTable(leagueId, true); // Force re-init
                 homeEntry = table.FirstOrDefault(e => e.TeamID == result.HomeTeamID);
                 awayEntry = table.FirstOrDefault(e => e.TeamID == result.AwayTeamID);
                 if (homeEntry == null || awayEntry == null) {
                      Debug.LogError($"[LeagueManager] Failed to find/create entries for teams {result.HomeTeamID}/{result.AwayTeamID} in league {leagueId}.");
                      return;
                 }
            }

            // Update Played, Goals
            homeEntry.Played++;
            awayEntry.Played++;
            homeEntry.GoalsFor += result.HomeScore;
            homeEntry.GoalsAgainst += result.AwayScore;
            awayEntry.GoalsFor += result.AwayScore;
            awayEntry.GoalsAgainst += result.HomeScore;

            // Update Win/Draw/Loss and Points
            if (result.HomeScore > result.AwayScore)
            {
                homeEntry.Wins++;
                awayEntry.Losses++;
            }
            else if (result.AwayScore > result.HomeScore)
            {
                awayEntry.Wins++;
                homeEntry.Losses++;
            }
            else
            {
                homeEntry.Draws++;
                awayEntry.Draws++;
            }

             // Don't call UpdateStandings here, let the weekly handler do it.
             // Debug.Log($"[LeagueManager] Processed result for League {leagueId}: {result}");
        }

        /// <summary>
        /// Calculates and sorts the league standings based on points, goal difference, etc.
        /// Usually called weekly by GameManager.
        /// </summary>
        public void UpdateStandings()
        {
            // Debug.Log("[LeagueManager] Updating all league standings...");
            foreach (var leagueId in _leagueTables.Keys.ToList()) // Iterate over keys copy
            {
                UpdateStandingsForLeague(leagueId);
            }
        }

        /// <summary>
        /// Updates standings for a specific league.
        /// </summary>
        public void UpdateStandingsForLeague(int leagueId)
        {
             if (!_leagueTables.ContainsKey(leagueId)) return; // No table for this league

             var table = _leagueTables[leagueId];

             // Sort the table based on Handball rules:
             // 1. Points (desc)
             // 2. Goal Difference (desc)
             // 3. Goals For (desc)
             // 4. Head-to-head (TODO: Complex, requires storing all results)
             // 5. Team Name (asc) - Tiebreaker
             var sortedTable = table.OrderByDescending(e => e.Points)
                                  .ThenByDescending(e => e.GoalDifference)
                                  .ThenByDescending(e => e.GoalsFor)
                                  // .ThenBy(...) // Add H2H later if needed
                                  .ThenBy(e => e.TeamName)
                                  .ToList();

             _leagueTables[leagueId] = sortedTable; // Replace with sorted list
             // Debug.Log($"[LeagueManager] Updated standings for League {leagueId}.");
        }


        /// <summary>
        /// Performs end-of-season processing like awarding titles, promotions, relegations.
        /// </summary>
        public void FinalizeSeason()
        {
            Debug.Log("[LeagueManager] Finalizing season (Placeholder)...");
            foreach (var leagueId in _leagueTables.Keys)
            {
                if (_leagueTables[leagueId].Any())
                {
                    string winner = _leagueTables[leagueId][0].TeamName;
                    Debug.Log($"League {leagueId} Winner: {winner}");
                    // TODO: Implement actual promotion/relegation logic based on final standings.
                    // This would involve potentially moving teams between league tables or updating their LeagueID.
                }
            }
            // Clear tables for the new season? Or reset stats? Resetting stats is safer.
            ResetTablesForNewSeason();
        }

         /// <summary>
        /// Resets the statistics in all league tables for the start of a new season.
        /// Keeps the teams in the table structure.
        /// </summary>
        private void ResetTablesForNewSeason()
        {
            Debug.Log("[LeagueManager] Resetting league table statistics for new season.");
            foreach(var table in _leagueTables.Values)
            {
                foreach(var entry in table)
                {
                    entry.Played = 0;
                    entry.Wins = 0;
                    entry.Draws = 0;
                    entry.Losses = 0;
                    entry.GoalsFor = 0;
                    entry.GoalsAgainst = 0;
                }
            }
        }

        /// <summary>
        /// Placeholder for tracking league-wide player statistics.
        /// </summary>
        public void TrackLeagueStats()
        {
            // Debug.Log("[LeagueManager] Placeholder: Tracking league stats (e.g., top scorers).");
            // TODO: Aggregate player stats after each match.
        }

        /// <summary>
        /// Placeholder for providing data to UI components.
        /// </summary>
        /// <param name="leagueId">The ID of the league table to retrieve.</param>
        /// <returns>The list of standing entries, or null if not found.</returns>
        public List<LeagueStandingEntry> GetLeagueTableForUI(int leagueId)
        {
            // Debug.Log($"[LeagueManager] Placeholder: Providing league table {leagueId} for UI.");
            _leagueTables.TryGetValue(leagueId, out var table);
            if(table == null && leagueId > 0) {
                 Debug.LogWarning($"[LeagueManager] UI requested table for league {leagueId}, but it doesn't exist yet. Initializing.");
                 InitializeLeagueTable(leagueId);
                 _leagueTables.TryGetValue(leagueId, out table);
            }
            return table; // Return the current state (might not be sorted if called mid-week)
        }

        /// <summary>
        /// Initializes the league table structure for a given league ID based on teams in GameManager.
        /// </summary>
        private void InitializeLeagueTable(int leagueId, bool forceReinit = false)
        {
            if (_leagueTables.ContainsKey(leagueId) && !forceReinit) return; // Already exists

            Debug.Log($"[LeagueManager] Initializing league table for LeagueID: {leagueId}");
            var gameManager = HandballManager.Core.GameManager.Instance;
             if (gameManager == null || gameManager.AllTeams == null) {
                Debug.LogError("[LeagueManager] Cannot initialize league table - GameManager or Team List not available.");
                return;
            }

            List<LeagueStandingEntry> newTable = new List<LeagueStandingEntry>();
            List<TeamData> teamsInLeague = gameManager.AllTeams.Where(t => t.LeagueID == leagueId).ToList();

            foreach (var team in teamsInLeague)
            {
                // Avoid adding duplicates if re-initializing
                if (!newTable.Any(e => e.TeamID == team.TeamID)) {
                    newTable.Add(new LeagueStandingEntry
                    {
                        TeamID = team.TeamID,
                        TeamName = team.Name
                        // Stats default to 0
                    });
                }
            }

            if(teamsInLeague.Count > 0 && newTable.Count == 0) {
                 Debug.LogWarning($"[LeagueManager] No teams found or added for League {leagueId} during initialization.");
            } else if (teamsInLeague.Count != newTable.Count && forceReinit) {
                Debug.LogWarning($"[LeagueManager] Mismatch between teams in league {leagueId} ({teamsInLeague.Count}) and entries created ({newTable.Count}) during re-initialization.");
            }


            _leagueTables[leagueId] = newTable; // Add or replace the table
        }

        /// <summary>
        /// Used by SaveGame to get the current state.
        /// </summary>
        public Dictionary<int, List<LeagueStandingEntry>> GetTablesForSave() {
            return _leagueTables;
        }

        /// <summary>
        /// Used by LoadGame to restore the state.
        /// </summary>
        public void RestoreTablesFromSave(Dictionary<int, List<LeagueStandingEntry>> loadedTables) {
             _leagueTables = loadedTables ?? new Dictionary<int, List<LeagueStandingEntry>>();
             Debug.Log($"[LeagueManager] Restored {_leagueTables.Count} league tables from save.");
        }

    }
}