using System;
using System.Collections.Generic;
using System.Linq;
using HandballManager.Data; // For TeamData
using HandballManager.Core.MatchData; // For MatchInfo struct
using UnityEngine;

namespace HandballManager.Management
{
    /// <summary>
    /// Manages the creation and retrieval of match fixtures for leagues/competitions.
    /// Handles schedule generation, match retrieval, and rescheduling.
    /// </summary>
    public class ScheduleManager
    {
        // Stores the generated schedule. Key is LeagueID or CompetitionID.
        // Value is a list of all matches for that competition.
        private Dictionary<int, List<MatchInfo>> _schedules = new Dictionary<int, List<MatchInfo>>();
        private bool _scheduleGenerated = false; // Flag to track if schedule has been generated

        // Constants for schedule generation
        private const int DEFAULT_DAYS_BETWEEN_MATCHES = 7; // Weekly matches by default
        private const DayOfWeek DEFAULT_MATCH_DAY = DayOfWeek.Saturday; // Default match day

        /// <summary>
        /// Generates a new schedule for all known leagues/teams.
        /// Creates a round-robin schedule for each league in the game.
        /// </summary>
        public void GenerateNewSchedule()
        {
            Debug.Log("[ScheduleManager] Generating new schedule...");
            _schedules.Clear();
            _scheduleGenerated = false;

            var gameManager = HandballManager.Core.GameManager.Instance;
            if (gameManager == null || gameManager.AllTeams == null)
            {
                Debug.LogError("[ScheduleManager] Cannot generate schedule - GameManager or Team List not available.");
                return;
            }

            // Get all leagues with teams
            var leagueIds = gameManager.AllTeams
                .Where(t => t.LeagueID.HasValue)
                .Select(t => t.LeagueID.Value)
                .Distinct()
                .ToList();

            if (leagueIds.Count == 0)
            {
                Debug.LogWarning("[ScheduleManager] No leagues found to schedule.");
                return;
            }

            // Generate schedule for each league
            foreach (int leagueId in leagueIds)
            {
                GenerateLeagueSchedule(leagueId, gameManager);
            }

            _scheduleGenerated = true;
            Debug.Log($"[ScheduleManager] Schedule generation complete for {leagueIds.Count} leagues.");

            // TODO: Implement cup competitions if needed
        }

        /// <summary>
        /// Generates a round-robin schedule for a specific league.
        /// </summary>
        /// <param name="leagueId">The ID of the league to generate a schedule for.</param>
        /// <param name="gameManager">Reference to the game manager.</param>
        private void GenerateLeagueSchedule(int leagueId, HandballManager.Core.GameManager gameManager)
        {
            // Get teams in this league
            List<TeamData> teamsInLeague = gameManager.AllTeams
                .Where(t => t.LeagueID.HasValue && t.LeagueID.Value == leagueId)
                .ToList();

            if (teamsInLeague.Count < 2)
            {
                Debug.LogWarning($"[ScheduleManager] Not enough teams ({teamsInLeague.Count}) in League {leagueId} to generate schedule.");
                return;
            }

            List<MatchInfo> leagueSchedule = new List<MatchInfo>();
            DateTime startDate = gameManager.TimeManager.CurrentDate; // Start scheduling from current date

            // Ensure start date is a typical match day
            while (startDate.DayOfWeek != DEFAULT_MATCH_DAY)
            {
                startDate = startDate.AddDays(1);
            }

            // Add dummy team if odd number for round robin
            bool addedDummy = false;
            if (teamsInLeague.Count % 2 != 0)
            {
                teamsInLeague.Add(null); // Null represents a bye
                addedDummy = true;
            }

            int numTeams = teamsInLeague.Count;
            int numRounds = numTeams - 1;
            int matchesPerRound = numTeams / 2;

            List<TeamData> roundRobinTeams = new List<TeamData>(teamsInLeague);
            DateTime currentMatchDate = startDate;

            // Generate double round-robin (home and away matches)
            for (int round = 0; round < numRounds * 2; round++)
            {
                bool isReturnLeg = round >= numRounds;

                for (int match = 0; match < matchesPerRound; match++)
                {
                    TeamData team1 = roundRobinTeams[match];
                    TeamData team2 = roundRobinTeams[numTeams - 1 - match];

                    // Skip bye matches
                    if (team1 != null && team2 != null)
                    {
                        TeamData home = isReturnLeg ? team2 : team1;
                        TeamData away = isReturnLeg ? team1 : team2;

                        leagueSchedule.Add(new MatchInfo
                        {
                            Date = currentMatchDate,
                            HomeTeamID = home.TeamID,
                            AwayTeamID = away.TeamID
                        });
                    }
                }

                // Rotate teams for next round (except the first team)
                TeamData lastTeam = roundRobinTeams[numTeams - 1];
                for (int i = numTeams - 1; i > 1; i--)
                {
                    roundRobinTeams[i] = roundRobinTeams[i - 1];
                }
                roundRobinTeams[1] = lastTeam;

                // Advance date for next round
                currentMatchDate = currentMatchDate.AddDays(DEFAULT_DAYS_BETWEEN_MATCHES);
            }

            _schedules.Add(leagueId, leagueSchedule);
            Debug.Log($"[ScheduleManager] Generated {leagueSchedule.Count} matches for League {leagueId}.");
        }

        /// <summary>
        /// Gets all matches scheduled for a specific date across all competitions.
        /// </summary>
        /// <param name="date">The date to check.</param>
        /// <returns>A list of MatchInfo objects for that date.</returns>
        public List<MatchInfo> GetMatchesForDate(DateTime date)
        {
            List<MatchInfo> matchesOnDate = new List<MatchInfo>();
            DateTime targetDate = date.Date; // Compare dates only

            if (!_scheduleGenerated)
            {
                // Optionally generate if not done yet? Or rely on GameManager calling Generate first.
                // Debug.LogWarning("[ScheduleManager] Schedule not generated yet. Returning empty list.");
                return matchesOnDate;
            }

            foreach (var kvp in _schedules)
            {
                matchesOnDate.AddRange(kvp.Value.Where(match => match.Date.Date == targetDate));
            }

            // if (matchesOnDate.Any()) Debug.Log($"[ScheduleManager] Found {matchesOnDate.Count} matches for {targetDate.ToShortDateString()}");

            return matchesOnDate;
        }

        /// <summary>
        /// Handles rescheduling of a postponed match to a new date.
        /// </summary>
        /// <param name="postponedMatch">The match info to reschedule.</param>
        /// <param name="newDate">The proposed new date for the match.</param>
        /// <returns>True if rescheduling was successful, false otherwise.</returns>
        public bool HandleRescheduling(MatchInfo postponedMatch, DateTime newDate)
        {
            Debug.Log($"[ScheduleManager] Rescheduling match {postponedMatch.HomeTeamID} vs {postponedMatch.AwayTeamID} to {newDate.ToShortDateString()}.");

            // Normalize date to start of day
            DateTime targetDate = newDate.Date;

            // Check if the new date is valid (not in the past)
            var gameManager = HandballManager.Core.GameManager.Instance;
            if (gameManager != null && targetDate < gameManager.TimeManager.CurrentDate)
            {
                Debug.LogWarning($"[ScheduleManager] Cannot reschedule to a date in the past: {targetDate.ToShortDateString()}");
                return false;
            }

            // Find the match in the schedule
            bool matchFound = false;
            MatchInfo updatedMatch = postponedMatch;
            updatedMatch.Date = targetDate;

            foreach (var kvp in _schedules)
            {
                int leagueId = kvp.Key;
                List<MatchInfo> leagueSchedule = kvp.Value;

                // Find the match in this league's schedule
                for (int i = 0; i < leagueSchedule.Count; i++)
                {
                    MatchInfo match = leagueSchedule[i];
                    if (match.HomeTeamID == postponedMatch.HomeTeamID &&
                        match.AwayTeamID == postponedMatch.AwayTeamID &&
                        match.Date.Date == postponedMatch.Date.Date)
                    {
                        // Check for conflicts on the new date
                        bool hasConflict = CheckForMatchConflicts(leagueId, updatedMatch);
                        if (hasConflict)
                        {
                            Debug.LogWarning($"[ScheduleManager] Conflict detected for rescheduled match on {targetDate.ToShortDateString()}");
                            return false;
                        }

                        // Update the match date
                        leagueSchedule[i] = updatedMatch;
                        matchFound = true;
                        Debug.Log($"[ScheduleManager] Successfully rescheduled match to {targetDate.ToShortDateString()}");
                        break;
                    }
                }

                if (matchFound) break;
            }

            if (!matchFound)
            {
                Debug.LogWarning($"[ScheduleManager] Could not find match to reschedule: {postponedMatch.HomeTeamID} vs {postponedMatch.AwayTeamID}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if there are any conflicts for a team on the specified date.
        /// </summary>
        /// <param name="leagueId">The league ID to check.</param>
        /// <param name="match">The match to check for conflicts.</param>
        /// <returns>True if there is a conflict, false otherwise.</returns>
        private bool CheckForMatchConflicts(int leagueId, MatchInfo match)
        {
            // Get all matches on the same date
            List<MatchInfo> matchesOnDate = GetMatchesForDate(match.Date);

            // Check if either team is already playing on this date
            return matchesOnDate.Any(m =>
                (m.HomeTeamID == match.HomeTeamID || m.AwayTeamID == match.HomeTeamID ||
                 m.HomeTeamID == match.AwayTeamID || m.AwayTeamID == match.AwayTeamID) &&
                !(m.HomeTeamID == match.HomeTeamID && m.AwayTeamID == match.AwayTeamID && m.Date.Date == match.Date.Date));
        }

        /// <summary>
        /// Handles the transition between seasons, clearing the old schedule.
        /// </summary>
        public void HandleSeasonTransition()
        {
            Debug.Log("[ScheduleManager] Handling season transition (clearing old schedule).");
            // Clear the schedule data
            _schedules.Clear();
            _scheduleGenerated = false;

            // Note: GenerateNewSchedule is typically called by GameManager after this method
        }

        /// <summary>
        /// Gets upcoming matches for a specific team.
        /// </summary>
        /// <param name="teamId">The team ID to find matches for.</param>
        /// <param name="startDate">The date to start looking from (defaults to current date).</param>
        /// <param name="maxMatches">Maximum number of matches to return.</param>
        /// <returns>A list of upcoming matches for the specified team.</returns>
        public List<MatchInfo> GetUpcomingMatchesForTeam(int teamId, DateTime? startDate = null, int maxMatches = 5)
        {
            if (!_scheduleGenerated)
            {
                return new List<MatchInfo>();
            }

            var gameManager = HandballManager.Core.GameManager.Instance;
            DateTime searchDate = startDate ?? gameManager?.TimeManager.CurrentDate ?? DateTime.Now;

            List<MatchInfo> upcomingMatches = new List<MatchInfo>();

            // Search through all leagues
            foreach (var kvp in _schedules)
            {
                // Find matches where this team is playing (home or away) and the date is in the future
                var teamMatches = kvp.Value
                    .Where(m => (m.HomeTeamID == teamId || m.AwayTeamID == teamId) && m.Date.Date >= searchDate.Date)
                    .OrderBy(m => m.Date)
                    .Take(maxMatches - upcomingMatches.Count);

                upcomingMatches.AddRange(teamMatches);

                if (upcomingMatches.Count >= maxMatches)
                {
                    break;
                }
            }

            return upcomingMatches.OrderBy(m => m.Date).Take(maxMatches).ToList();
        }

        /// <summary>
        /// Finds the next available date for a match that doesn't conflict with existing matches.
        /// </summary>
        /// <param name="teamIds">The team IDs involved in the match.</param>
        /// <param name="startDate">The date to start searching from.</param>
        /// <param name="preferredDay">The preferred day of week for the match.</param>
        /// <returns>The next available date that doesn't have conflicts.</returns>
        public DateTime FindNextAvailableMatchDate(int[] teamIds, DateTime startDate, DayOfWeek preferredDay = DayOfWeek.Saturday)
        {
            DateTime candidateDate = startDate.Date;

            // Ensure we start on the preferred day of week
            while (candidateDate.DayOfWeek != preferredDay)
            {
                candidateDate = candidateDate.AddDays(1);
            }

            bool foundValidDate = false;
            int maxIterations = 52; // Prevent infinite loop, search up to a year ahead
            int iterations = 0;

            while (!foundValidDate && iterations < maxIterations)
            {
                // Get all matches on this date
                List<MatchInfo> matchesOnDate = GetMatchesForDate(candidateDate);

                // Check if any of our teams are already playing on this date
                bool hasConflict = matchesOnDate.Any(m =>
                    teamIds.Contains(m.HomeTeamID) || teamIds.Contains(m.AwayTeamID));

                if (!hasConflict)
                {
                    foundValidDate = true;
                }
                else
                {
                    // Try the next preferred day
                    candidateDate = candidateDate.AddDays(7);
                    iterations++;
                }
            }

            return candidateDate;
        }

        /// <summary>
        /// Gets all schedules data for saving.
        /// </summary>
        /// <returns>The schedule dictionary for serialization.</returns>
        public Dictionary<int, List<MatchInfo>> GetSchedulesForSave()
        {
            return new Dictionary<int, List<MatchInfo>>(_schedules);
        }

        /// <summary>
        /// Restores schedules from saved data.
        /// </summary>
        /// <param name="savedSchedules">The saved schedule data.</param>
        public void RestoreSchedulesFromSave(Dictionary<int, List<MatchInfo>> savedSchedules)
        {
            if (savedSchedules == null || savedSchedules.Count == 0)
            {
                Debug.Log("[ScheduleManager] No saved schedules to restore.");
                return;
            }

            _schedules = new Dictionary<int, List<MatchInfo>>(savedSchedules);
            _scheduleGenerated = true;
            Debug.Log($"[ScheduleManager] Restored {_schedules.Count} league schedules.");
        }
    }
}