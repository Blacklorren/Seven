using HandballManager.Data;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Interfaces;
using System;
using UnityEngine;

namespace HandballManager.Simulation.Events
{
    public class DefaultMatchFinalizer : IMatchFinalizer
    {
        public MatchResult FinalizeResult(MatchState state, DateTime matchDate)
        {
            // --- Logic copied from MatchSimulator ---
             if (state == null) {
                 Debug.LogError("[DefaultMatchFinalizer] Cannot finalize result, MatchState is null!");
                 return new MatchResult(-99, -98, "ERROR", "NULL_STATE") { MatchDate = matchDate }; // Use passed date
             }

             MatchResult result = new MatchResult(
                 state.HomeTeamData?.TeamID ?? -1,
                 state.AwayTeamData?.TeamID ?? -2,
                 state.HomeTeamData?.Name ?? "Home_Err",
                 state.AwayTeamData?.Name ?? "Away_Err"
             ) {
                 HomeScore = state.HomeScore,
                 AwayScore = state.AwayScore,
                 MatchDate = matchDate,
                 HomeStats = state.CurrentHomeStats ?? new TeamMatchStats(),
                 AwayStats = state.CurrentAwayStats ?? new TeamMatchStats()
             };

             // Final Validation
             if (result.HomeScore != result.HomeStats.GoalsScored) { Debug.LogWarning($"[Validation] Final score mismatch! Home Score: {result.HomeScore} vs Stats Goals: {result.HomeStats.GoalsScored}"); }
             if (result.AwayScore != result.AwayStats.GoalsScored) { Debug.LogWarning($"[Validation] Final score mismatch! Away Score: {result.AwayScore} vs Stats Goals: {result.AwayStats.GoalsScored}"); }
             return result;
        }
    }
}