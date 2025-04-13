using HandballManager.Data;
using HandballManager.Simulation.Core.MatchData;
using System;
using UnityEngine;

namespace HandballManager.Simulation.Events
{
    public class DefaultMatchFinalizer : IMatchFinalizer
    {
        /// <summary>Error when home team data is missing</summary>
        private const int ErrMissingHomeTeam = -99;
        /// <summary>Error when away team data is missing</summary>
        private const int ErrMissingAwayTeam = -98;
        private const string ErrorTeamName = "INVALID_TEAM";

        /// <summary>
        /// Finalizes match results with validation and error handling
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="matchDate">Timestamp for the match result</param>
        public MatchResult FinalizeResult(MatchState state, DateTime matchDate)
        {
            // --- Logic copied from MatchSimulator ---
             if (state == null) {
                 Debug.LogError("[DefaultMatchFinalizer] Cannot finalize result, MatchState is null!");
                 return new MatchResult(ErrMissingHomeTeam, ErrMissingAwayTeam, ErrorTeamName, "NULL_STATE") { MatchDate = matchDate }; // Use passed date
             }

             if (state.HomeTeamData == null || state.AwayTeamData == null) {
                 Debug.LogError($"[DefaultMatchFinalizer] Missing team data - Home: {state.HomeTeamData?.TeamID ?? -1}, Away: {state.AwayTeamData?.TeamID ?? -1}");
                 return new MatchResult(ErrMissingHomeTeam, ErrMissingAwayTeam, ErrorTeamName, "MISSING_TEAM_DATA") {
                     MatchDate = matchDate
                 };
             }

             MatchResult result = new MatchResult(
                 state.HomeTeamData.TeamID,
                 state.AwayTeamData.TeamID,
                 state.HomeTeamData.Name,
                 state.AwayTeamData.Name
             ) {
                 HomeScore = state.HomeScore,
                 AwayScore = state.AwayScore,
                 MatchDate = matchDate,
                 HomeStats = state.CurrentHomeStats ?? new TeamMatchStats(),
                 AwayStats = state.CurrentAwayStats ?? new TeamMatchStats()
             };

             // Enforce score consistency
             result.HomeStats.GoalsScored = result.HomeScore;
             result.AwayStats.GoalsScored = result.AwayScore;
             
             // Final Validation (now should never trigger)
             Debug.Assert(result.HomeScore == result.HomeStats.GoalsScored, "Home score mismatch");
             Debug.Assert(result.AwayScore == result.AwayStats.GoalsScored, "Away score mismatch");
             return result;
        }
    }
}