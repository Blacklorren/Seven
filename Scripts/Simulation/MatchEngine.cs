using UnityEngine; // For Debug.Log
using HandballManager.Data;          // Core data structures (TeamData, MatchResult, TeamMatchStats) <-- Added Data
using HandballManager.Gameplay;    // Gameplay elements (Tactic)
using HandballManager.Simulation;    // The detailed simulator (MatchSimulator)
using HandballManager.Simulation.MatchData; // For SimPlayer, PlayerPosition enum access is often here or Core
using HandballManager.Core; // For PlayerPosition enum access
using System;                       // For Exception handling
using System.Linq;                  // For Linq checks on rosters

namespace HandballManager.Simulation // Placing it within the Simulation namespace seems appropriate
{
    /// <summary>
    /// Orchestrates the running of a single match simulation.
    /// Acts as the primary interface between the game management layer (GameManager)
    /// and the detailed simulation logic (MatchSimulator).
    /// </summary>
    public class MatchEngine
    {
        // MatchSimulator is instantiated per-match currently, so no persistent instance field needed here.
        // private MatchSimulator _matchSimulator;

        /// <summary>
        /// Initializes a new instance of the MatchEngine.
        /// </summary>
        public MatchEngine()
        {
            // Constructor can be empty if MatchSimulator is created per-match.
        }

        /// <summary>
        /// Simulates a single match between two teams using the provided tactics.
        /// </summary>
        /// <param name="homeTeam">The home team's data.</param>
        /// <param name="awayTeam">The away team's data.</param>
        /// <param name="homeTactic">The tactic used by the home team.</param>
        /// <param name="awayTactic">The tactic used by the away team.</param>
        /// <param name="seed">Optional random seed for deterministic simulation. -1 uses Simulator's default (usually time-based).</param>
        /// <returns>A MatchResult object containing the score and detailed statistics.</returns>
        public MatchResult SimulateMatch(TeamData homeTeam, TeamData awayTeam, Tactic homeTactic, Tactic awayTactic, int seed = -1)
        {
            // --- Input Validation ---
            if (homeTeam == null || awayTeam == null)
            {
                Debug.LogError("[MatchEngine] Cannot simulate match: Home or Away TeamData is null.");
                return CreateErrorResult("Null Team Data");
            }
            if (homeTactic == null || awayTactic == null)
            {
                Debug.LogWarning($"[MatchEngine] Simulating match for {homeTeam.Name} vs {awayTeam.Name} with potentially null tactics. Using defaults.");
                // Use default tactics if none provided (or handle as error depending on design)
                homeTactic ??= new Tactic { TacticName = "Default Home" }; // Ensure non-null tactic object
                awayTactic ??= new Tactic { TacticName = "Default Away" };
            }

            // --- Roster Validation (Issue 5 Fix) ---
            const int MIN_PLAYERS_REQUIRED = 7;
            if (homeTeam.Roster == null || homeTeam.Roster.Count < MIN_PLAYERS_REQUIRED)
            {
                return CreateErrorResult($"Home Team ({homeTeam.Name}) roster invalid or has < {MIN_PLAYERS_REQUIRED} players ({homeTeam.Roster?.Count ?? 0}).");
            }
            if (awayTeam.Roster == null || awayTeam.Roster.Count < MIN_PLAYERS_REQUIRED)
            {
                return CreateErrorResult($"Away Team ({awayTeam.Name}) roster invalid or has < {MIN_PLAYERS_REQUIRED} players ({awayTeam.Roster?.Count ?? 0}).");
            }
            // Check for at least one Goalkeeper per team (could make more robust by checking non-injured GKs)
            if (!homeTeam.Roster.Any(p => p.PrimaryPosition == PlayerPosition.Goalkeeper))
            {
                return CreateErrorResult($"Home Team ({homeTeam.Name}) has no Goalkeeper in roster.");
            }
            if (!awayTeam.Roster.Any(p => p.PrimaryPosition == PlayerPosition.Goalkeeper))
            {
                return CreateErrorResult($"Away Team ({awayTeam.Name}) has no Goalkeeper in roster.");
            }
            // ------------------------------------


            // --- Simulation Setup ---
            // Create a new simulator instance for each match to ensure clean state.
            // The MatchSimulator constructor handles the seed logic (where -1 means use internal default).
             MatchSimulator matchSimulator = new MatchSimulator(seed);
             MatchResult result = null; // Initialize result

            Debug.Log($"[MatchEngine] Starting simulation: {homeTeam.Name} vs {awayTeam.Name} (Seed: {seed})");

            // --- Run Simulation ---
            try
            {
                 // The MatchSimulator handles internal setup (lineups etc.) and running the simulation loop
                 result = matchSimulator.SimulateMatch(homeTeam, awayTeam, homeTactic, awayTactic);

                 // Post-simulation processing could happen here if needed,
                 // e.g., calculating player ratings based on MatchResult stats,
                 // but the core result finalization (incl. stats) is now done in MatchSimulator.FinalizeResult
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchEngine] CRITICAL ERROR during match simulation: {ex.Message}\n{ex.StackTrace}");
                result = CreateErrorResult($"Simulation Exception: {ex.Message}");
            }
            finally
            {
                // --- Cleanup (Issue 4 Fix) ---
                // As MatchSimulator currently doesn't implement IDisposable and is created locally,
                // no explicit disposal is needed; it will be garbage collected.
                // If MatchSimulator were to manage unmanaged resources later, it should implement IDisposable
                // and be disposed here using a 'using' statement or try/finally block.
                matchSimulator = null; // Explicitly nullify reference (minor helper for GC potentially)
            }


            Debug.Log($"[MatchEngine] Simulation finished: {result}"); // Log the final result scoreline

            // --- Return Result ---
            // The MatchSimulator's FinalizeResult method now populates the stats correctly.
            return result ?? CreateErrorResult("Simulation returned null result unexpectedly."); // Added null check
        }

        /// <summary>
        /// Creates a dummy MatchResult indicating an error occurred.
        /// </summary>
        /// <param name="reason">The reason for the error.</param>
        /// <returns>A MatchResult with error indicators.</returns>
        private MatchResult CreateErrorResult(string reason)
        {
             // Use distinct IDs or names to signal an error state
             // Using -1, -2 for IDs is generally safe if valid team IDs are positive.
            MatchResult errorResult = new MatchResult(-1, -2, "ERROR", "ERROR")
            {
                HomeScore = 0,
                AwayScore = 0,
                HomeStats = new TeamMatchStats(), // Ensure stats objects exist even on error
                AwayStats = new TeamMatchStats(),
                MatchDate = Core.GameManager.Instance?.TimeManager?.CurrentDate ?? DateTime.Now.Date // Attempt to get game date
            };
             // Could add the error reason to a potential 'Notes' field in MatchResult if desired
             Debug.LogError($"[MatchEngine] Created Error Result: {reason}");
            return errorResult;
        }

        // Potential future additions:
        // - Method to simulate only part of a match (e.g., for live viewing)
        // - Pre-match condition/morale adjustments based on external factors
        // - Post-match player rating calculations
        // - Applying fatigue directly to TeamData based on simulation results (better than GameManager's current method)
    }
}