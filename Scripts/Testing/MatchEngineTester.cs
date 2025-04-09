--- START OF FILE MatchEngineTester.cs ---

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using HandballManager.Data;          // Access core data structures
using HandballManager.Simulation;     // Access MatchEngine & simulation data
using HandballManager.Gameplay;     // Access Tactic
using HandballManager.Core;         // Access Enums
using System;                       // For Math, Random, DateTime
using System.IO;                    // For File Export
using System.Text;                  // For StringBuilder

/// <summary>
/// A MonoBehaviour script designed specifically to test the MatchEngine.
/// Allows configuration of team abilities, tactics, and simulation count.
/// Runs simulations, analyzes results (including team stats), logs them,
/// and optionally exports summary data to a CSV file.
/// Addresses several readability, configuration, and error handling issues.
/// Attach this script to a GameObject in a dedicated test scene.
///
/// !! IMPORTANT !! This script ASSUMES that MatchResult has been extended
/// to include 'HomeStats' and 'AwayStats' properties of type 'TeamMatchStats',
/// and that the MatchEngine populates these stats correctly.
/// </summary>
// Added namespace to match file location
namespace HandballManager.Testing 
{
    // Existing using directives are correct and cover all dependencies:
    // - HandballManager.Data (TeamData/PlayerData)
    // - HandballManager.Simulation (MatchEngine)
    // - HandballManager.Gameplay (Tactic)
    // - HandballManager.Core (Enums)
    // - System.IO and System.Text for file operations
    // - UnityEngine for Debug.Log
    
    public class MatchEngineTester : MonoBehaviour 
    {
        #region Inspector Configuration Fields

        [Header("Test Configuration")]
        [Tooltip("Number of matches to simulate per parameter step.")]
        [SerializeField] private int numberOfSimulationsPerStep = 50;

        [Tooltip("Seed for the random number generator used in tests. -1 uses a time-based seed.")]
        [SerializeField] private int randomSeed = -1;

        [Header("Team Setup")]
        [Tooltip("Average base ability score for Away Team players.")]
        [Range(30, 90)]
        [SerializeField] private int awayTeamAvgAbility = 70;

        [Tooltip("How much player abilities can vary from the average.")]
        [Range(0, 15)]
        [SerializeField] private int abilityVariance = 8;

        [Header("Progressive Testing (Home Team Ability)")]
        [Tooltip("Run tests for a range of Home Team average abilities.")]
        [SerializeField] private bool testAbilityRange = true;
        [Tooltip("Starting average ability for the Home Team range test.")]
        [Range(30, 90)]
        [SerializeField] private int homeAbilityStart = 60;
        [Tooltip("Ending average ability for the Home Team range test.")]
        [Range(30, 90)]
        [SerializeField] private int homeAbilityEnd = 80;
        [Tooltip("Step size for increasing Home Team average ability during range test.")]
        [Range(1, 10)]
        [SerializeField] private int homeAbilityStep = 5;

        [Header("Tactics Configuration")]
        [SerializeField] private TacticPace homeTacticPace = TacticPace.Normal;
        [SerializeField] private DefensiveSystem homeDefensiveSystem = DefensiveSystem.SixZero;
        [SerializeField] private OffensiveFocusPlay homeOffensiveFocus = OffensiveFocusPlay.Balanced;
        [SerializeField] private TacticPace awayTacticPace = TacticPace.Normal;
        [SerializeField] private DefensiveSystem awayDefensiveSystem = DefensiveSystem.SixZero;
        [SerializeField] private OffensiveFocusPlay awayOffensiveFocus = OffensiveFocusPlay.Balanced;

        [Header("Output Configuration")]
        [Tooltip("Export summary analysis results to a CSV file?")]
        [SerializeField] private bool exportResultsToFile = true;
        [Tooltip("Filename for the exported CSV results.")]
        [SerializeField] private string exportFilename = "MatchEngineTestResults.csv";

        #endregion

        #region Private Fields

        private MatchEngine matchEngine;
        private System.Random testRandom; // Random generator for test setup
        private static int _nextPlayerId = 10000; // Use a shared counter for unique IDs in tests
        private List<string[]> summaryExportData; // Holds data for CSV export (Header + rows)

        #endregion

        #region Constants for Realism Checks

        // Define constants for typical stat ranges
        private const double TYPICAL_MIN_GOALS = 50;
        private const double TYPICAL_MAX_GOALS = 75;
        private const double TYPICAL_MIN_DRAW_PCT = 5;
        private const double TYPICAL_MAX_DRAW_PCT = 20;
        private const double TYPICAL_MIN_SHOTS_TEAM = 45;
        private const double TYPICAL_MAX_SHOTS_TEAM = 70;
        private const double TYPICAL_MIN_SHOOT_PCT = 45;
        private const double TYPICAL_MAX_SHOOT_PCT = 65;
        private const double TYPICAL_MIN_SAVE_PCT = 25;
        private const double TYPICAL_MAX_SAVE_PCT = 40;
        private const double TYPICAL_MIN_TURNOVERS_TEAM = 8;
        private const double TYPICAL_MAX_TURNOVERS_TEAM = 18;
        private const double TYPICAL_MIN_SUSP_TEAM = 1;
        private const double TYPICAL_MAX_SUSP_TEAM = 5;

        #endregion

        #region Unity Methods

        void Start()
        {
            Debug.Log("===== MatchEngine Test Started =====");
            LogConfiguration(); // Log the settings being used

            // Initialize Match Engine
            matchEngine = new MatchEngine();

            // Initialize Random Generator for Test Setup
            testRandom = (randomSeed == -1) ? new System.Random() : new System.Random(randomSeed);

            // Initialize export data list with header row
            summaryExportData = new List<string[]> { GetCsvHeader() };

            // Run simulations based on configuration
            if (testAbilityRange)
            {
                RunProgressiveSimulations();
            }
            else
            {
                // Use homeAbilityStart as the fixed value if not testing range
                RunBatchSimulations(homeAbilityStart, numberOfSimulationsPerStep);
            }

            // Export results if enabled
            if (exportResultsToFile)
            {
                ExportSummaryToCsv();
            }

            Debug.Log("===== MatchEngine Test Finished =====");
        }

        #endregion

        #region Simulation Execution

        /// <summary>
        /// Runs simulations progressively, varying the home team's average ability.
        /// </summary>
        private void RunProgressiveSimulations()
        {
            Debug.Log($"Starting progressive simulations from Home Ability {homeAbilityStart} to {homeAbilityEnd} (Step: {homeAbilityStep})...");

            int totalSimsRun = 0;
            System.Diagnostics.Stopwatch totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int currentHomeAbility = homeAbilityStart; currentHomeAbility <= homeAbilityEnd; currentHomeAbility += homeAbilityStep)
            {
                Debug.Log($"--- Running Batch for Home Ability: {currentHomeAbility} ---");
                RunBatchSimulations(currentHomeAbility, numberOfSimulationsPerStep);
                totalSimsRun += numberOfSimulationsPerStep; // Assume all sims in batch completed for total count
            }

            totalStopwatch.Stop();
            Debug.Log($"--- Progressive Simulation Complete ---");
            Debug.Log($"Total Simulations Run: {totalSimsRun} in {totalStopwatch.Elapsed.TotalSeconds:F2} seconds.");
        }


        /// <summary>
        /// Runs a specific batch of match simulations with the given parameters.
        /// </summary>
        private void RunBatchSimulations(int currentHomeAbility, int numSims)
        {
            if (numSims <= 0)
            {
                Debug.LogError("Number of simulations per step must be positive.");
                return;
            }

            // Setup Teams & Tactics based on current parameters and Inspector settings
            TeamData homeTeam = SetupTestTeam("Home Test Team", currentHomeAbility, 1);
            TeamData awayTeam = SetupTestTeam("Away Test Team", awayTeamAvgAbility, 2);
            Tactic homeTactic = CreateConfiguredTactic("Home Tactic", homeTacticPace, homeDefensiveSystem, homeOffensiveFocus);
            Tactic awayTactic = CreateConfiguredTactic("Away Tactic", awayTacticPace, awayDefensiveSystem, awayOffensiveFocus);

            if (homeTeam == null || awayTeam == null)
            {
                Debug.LogError($"Failed to create test teams for Home Ability {currentHomeAbility}. Skipping batch.");
                return;
            }

            List<MatchResult> validResults = new List<MatchResult>(); // Only store valid results
            int failedSimulations = 0;
            const int LOG_INTERVAL = 10;

            Debug.Log($"Starting {numSims} simulations for Home Ability {currentHomeAbility}...");
            System.Diagnostics.Stopwatch batchStopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < numSims; i++)
            {
                // Use a consistent seed *per simulation* within the batch if desired, or let MatchEngine handle it
                // int simulationSeed = (randomSeed == -1) ? -1 : randomSeed + i; // Example: Vary seed per sim
                int simulationSeed = randomSeed; // Or use the same seed for all sims in the batch

                MatchResult result = matchEngine.SimulateMatch(homeTeam, awayTeam, homeTactic, awayTactic, simulationSeed);

                // --- Improved Error Handling ---
                bool isValidResult = ValidateResult(result, i + 1);
                if (isValidResult)
                {
                    validResults.Add(result);
                }
                else
                {
                    failedSimulations++;
                    // Continue to next simulation instead of adding dummy data
                }

                if ((i + 1) % LOG_INTERVAL == 0 || (i + 1) == numSims)
                {
                    Debug.Log($"  Simulation {i + 1}/{numSims} complete...");
                }
            }

            batchStopwatch.Stop();
            Debug.Log($"Finished batch ({numSims} sims) for Home Ability {currentHomeAbility} in {batchStopwatch.Elapsed.TotalSeconds:F2} seconds. " +
                      $"({failedSimulations} sims failed validation).");

            // Analyze and Log Results for this batch
            if (validResults.Any())
            {
                AnalyzeResults(validResults, currentHomeAbility); // Pass parameter context
            }
            else
            {
                Debug.LogWarning($"No valid results to analyze for Home Ability {currentHomeAbility}.");
            }
        }

        /// <summary>
        /// Validates a MatchResult object.
        /// </summary>
        /// <returns>True if the result is considered valid, false otherwise.</returns>
        private bool ValidateResult(MatchResult result, int simulationNumber)
        {
             if (result == null) {
                 Debug.LogError($"Simulation {simulationNumber} failed: Result object is null.");
                 return false;
             }
             // Check for specific error indicators if MatchEngine uses them
             if (result.HomeTeamID < 0 || result.AwayTeamID < 0 || result.HomeTeamName == "ERROR") {
                 Debug.LogError($"Simulation {simulationNumber} failed: MatchEngine returned an error result.");
                 return false;
             }
             // Check for stats objects (as per original assumption)
             if (result.HomeStats == null || result.AwayStats == null)
             {
                 Debug.LogError($"Simulation {simulationNumber} failed: MatchResult did not contain HomeStats/AwayStats objects.");
                 return false;
             }
             // Check score vs stats consistency
             if (result.HomeStats.GoalsScored != result.HomeScore || result.AwayStats.GoalsScored != result.AwayScore)
             {
                 Debug.LogWarning($"Simulation {simulationNumber} inconsistency: Score ({result.HomeScore}-{result.AwayScore}) doesn't match Stats Goals ({result.HomeStats.GoalsScored}-{result.AwayStats.GoalsScored}).");
                 // Decide if this inconsistency invalidates the result for analysis (optional)
                 // return false; // Option: Treat as invalid
             }
             return true; // Result seems valid enough for analysis
        }

        #endregion

        #region Results Analysis & Export

        /// <summary>
        /// Analyzes the collected match results for a specific batch and logs statistics.
        /// </summary>
        private void AnalyzeResults(List<MatchResult> results, int homeAbilityParam)
        {
            // This check should ideally not be needed due to RunBatchSimulations check, but safety first.
            if (results == null || !results.Any())
            {
                Debug.LogWarning($"AnalyzeResults called with no valid results for Home Ability {homeAbilityParam}.");
                return;
            }

            Debug.Log($"\n----- Analysis for Home Ability: {homeAbilityParam} ({results.Count} valid matches) -----");

            int numMatches = results.Count;

            // Aggregate Score Statistics
            List<int> homeScores = results.Select(r => r.HomeScore).ToList();
            List<int> awayScores = results.Select(r => r.AwayScore).ToList();
            List<int> totalGoals = results.Select(r => r.HomeScore + r.AwayScore).ToList();

            double avgHomeScore = homeScores.Average();
            double avgAwayScore = awayScores.Average();
            double avgTotalGoals = totalGoals.Average();

            double stdDevHome = CalculateStandardDeviation(homeScores);
            double stdDevAway = CalculateStandardDeviation(awayScores);
            double stdDevTotal = CalculateStandardDeviation(totalGoals);

            int minHome = homeScores.Min();
            int maxHome = homeScores.Max();
            int minAway = awayScores.Min();
            int maxAway = awayScores.Max();
            int minTotal = totalGoals.Min();
            int maxTotal = totalGoals.Max();

            // Aggregate Team Statistics
            double avgHomeShots = results.Average(r => r.HomeStats.ShotsTaken);
            double avgAwayShots = results.Average(r => r.AwayStats.ShotsTaken);
            double avgHomeSOT = results.Average(r => r.HomeStats.ShotsOnTarget);
            double avgAwaySOT = results.Average(r => r.AwayStats.ShotsOnTarget);
            double avgHomeSaves = results.Average(r => r.HomeStats.SavesMade);
            double avgAwaySaves = results.Average(r => r.AwayStats.SavesMade);
            double avgHomeTurnovers = results.Average(r => r.HomeStats.Turnovers);
            double avgAwayTurnovers = results.Average(r => r.AwayStats.Turnovers);
            double avgHomeFouls = results.Average(r => r.HomeStats.FoulsCommitted);
            double avgAwayFouls = results.Average(r => r.AwayStats.FoulsCommitted);
            double avgHomeSuspensions = results.Average(r => r.HomeStats.TwoMinuteSuspensions);
            double avgAwaySuspensions = results.Average(r => r.AwayStats.TwoMinuteSuspensions);

            // Calculate overall percentages using totals
            long totalHomeShots = results.Sum(r => (long)r.HomeStats.ShotsTaken); // Use long for sums
            long totalAwayShots = results.Sum(r => (long)r.AwayStats.ShotsTaken);
            long totalHomeGoals = results.Sum(r => (long)r.HomeStats.GoalsScored);
            long totalAwayGoals = results.Sum(r => (long)r.AwayStats.GoalsScored);
            long totalHomeSOT = results.Sum(r => (long)r.HomeStats.ShotsOnTarget);
            long totalAwaySOT = results.Sum(r => (long)r.AwayStats.ShotsOnTarget);
            long totalHomeSaves = results.Sum(r => (long)r.HomeStats.SavesMade);
            long totalAwaySaves = results.Sum(r => (long)r.AwayStats.SavesMade);

            float overallHomeShootPct = totalHomeShots == 0 ? 0f : (float)totalHomeGoals / totalHomeShots * 100f;
            float overallAwayShootPct = totalAwayShots == 0 ? 0f : (float)totalAwayGoals / totalAwayShots * 100f;
            float overallHomeSotPct = totalHomeShots == 0 ? 0f : (float)totalHomeSOT / totalHomeShots * 100f;
            float overallAwaySotPct = totalAwayShots == 0 ? 0f : (float)totalAwaySOT / totalAwayShots * 100f;
            float overallHomeSavePct = totalAwaySOT == 0 ? 0f : (float)totalHomeSaves / totalAwaySOT * 100f; // Home saves vs Away SOT
            float overallAwaySavePct = totalHomeSOT == 0 ? 0f : (float)totalAwaySaves / totalHomeSOT * 100f; // Away saves vs Home SOT

            // Calculate Win/Draw/Loss Percentage
            int homeWins = results.Count(r => r.HomeScore > r.AwayScore);
            int awayWins = results.Count(r => r.AwayScore > r.HomeScore);
            int draws = results.Count(r => r.HomeScore == r.AwayScore);

            double homeWinPct = (double)homeWins / numMatches * 100.0;
            double awayWinPct = (double)awayWins / numMatches * 100.0;
            double drawPct = (double)draws / numMatches * 100.0;

            // --- Log Summary ---
            Debug.Log($"--- Scores ---");
            Debug.Log($"Avg H Score: {avgHomeScore:F2} (StdDev:{stdDevHome:F2}|Min:{minHome}|Max:{maxHome})");
            Debug.Log($"Avg A Score: {avgAwayScore:F2} (StdDev:{stdDevAway:F2}|Min:{minAway}|Max:{maxAway})");
            Debug.Log($"Avg Total Goals: {avgTotalGoals:F2} (StdDev:{stdDevTotal:F2}|Min:{minTotal}|Max:{maxTotal})");
            Debug.Log($"--- Outcomes ---");
            Debug.Log($"H Wins: {homeWins} ({homeWinPct:F1}%) | A Wins: {awayWins} ({awayWinPct:F1}%) | Draws: {draws} ({drawPct:F1}%)");
            Debug.Log($"--- Team Statistics (Averages Per Match) ---");
            Debug.Log($"            |   Home   |   Away   |");
            Debug.Log($"------------|----------|----------|");
            Debug.Log($"Shots       | {avgHomeShots,8:F1} | {avgAwayShots,8:F1} |");
            Debug.Log($"SOT         | {avgHomeSOT,8:F1} | {avgAwaySOT,8:F1} |");
            Debug.Log($"Saves       | {avgHomeSaves,8:F1} | {avgAwaySaves,8:F1} |");
            Debug.Log($"Turnovers   | {avgHomeTurnovers,8:F1} | {avgAwayTurnovers,8:F1} |");
            Debug.Log($"Fouls       | {avgHomeFouls,8:F1} | {avgAwayFouls,8:F1} |");
            Debug.Log($"2min Susp   | {avgHomeSuspensions,8:F1} | {avgAwaySuspensions,8:F1} |");
            Debug.Log($"--- Overall Percentages ---");
            Debug.Log($"Shooting %  | {overallHomeShootPct,8:F1}% | {overallAwayShootPct,8:F1}% |");
            Debug.Log($"SOT %       | {overallHomeSotPct,8:F1}% | {overallAwaySotPct,8:F1}% |");
            Debug.Log($"Save %      | {overallHomeSavePct,8:F1}% | {overallAwaySavePct,8:F1}% |");
            Debug.Log("---------------------------------");

            // --- Realism Check Notes ---
            Debug.Log("--- Realism Check Notes (Approximate Handball Values) ---");
            // Use defined constants for checks
            CheckStatRealism("Avg Total Goals", avgTotalGoals, TYPICAL_MIN_GOALS, TYPICAL_MAX_GOALS);
            CheckStatRealism("Draw %", drawPct, TYPICAL_MIN_DRAW_PCT, TYPICAL_MAX_DRAW_PCT);
            CheckStatRealism("Avg Shots (Team)", (avgHomeShots + avgAwayShots) / 2.0, TYPICAL_MIN_SHOTS_TEAM, TYPICAL_MAX_SHOTS_TEAM);
            CheckStatRealism("Overall Shooting %", (overallHomeShootPct + overallAwayShootPct) / 2.0f, TYPICAL_MIN_SHOOT_PCT, TYPICAL_MAX_SHOOT_PCT);
            CheckStatRealism("Overall Save %", (overallHomeSavePct + overallAwaySavePct) / 2.0f, TYPICAL_MIN_SAVE_PCT, TYPICAL_MAX_SAVE_PCT);
            CheckStatRealism("Avg Turnovers (Team)", (avgHomeTurnovers + avgAwayTurnovers) / 2.0, TYPICAL_MIN_TURNOVERS_TEAM, TYPICAL_MAX_TURNOVERS_TEAM);
            CheckStatRealism("Avg 2min Susp (Team)", (avgHomeSuspensions + avgAwaySuspensions) / 2.0, TYPICAL_MIN_SUSP_TEAM, TYPICAL_MAX_SUSP_TEAM);
            Debug.Log("---------------------------------");


            // --- Add Data Row for Export ---
            if (exportResultsToFile)
            {
                summaryExportData.Add(new string[] {
                    homeAbilityParam.ToString(), // Home Ability (Parameter for this batch)
                    awayTeamAvgAbility.ToString(), // Away Ability (Constant for this batch)
                    numMatches.ToString(), // Valid Matches Analyzed
                    $"{homeTacticPace}/{homeDefensiveSystem}", // Home Tactic Summary
                    $"{awayTacticPace}/{awayDefensiveSystem}", // Away Tactic Summary
                    avgHomeScore.ToString("F2"),
                    avgAwayScore.ToString("F2"),
                    avgTotalGoals.ToString("F2"),
                    stdDevTotal.ToString("F2"),
                    homeWinPct.ToString("F1"),
                    awayWinPct.ToString("F1"),
                    drawPct.ToString("F1"),
                    avgHomeShots.ToString("F1"),
                    avgAwayShots.ToString("F1"),
                    overallHomeShootPct.ToString("F1"),
                    overallAwayShootPct.ToString("F1"),
                    overallHomeSavePct.ToString("F1"),
                    overallAwaySavePct.ToString("F1"),
                    avgHomeTurnovers.ToString("F1"),
                    avgAwayTurnovers.ToString("F1"),
                    avgHomeSuspensions.ToString("F1"),
                    avgAwaySuspensions.ToString("F1")
                });
            }
        }

        /// <summary>
        /// Gets the header row for the CSV export file.
        /// </summary>
        private string[] GetCsvHeader()
        {
            return new string[] {
                "HomeAvgAbility", "AwayAvgAbility", "ValidSims", "HomeTactic", "AwayTactic",
                "AvgHomeScore", "AvgAwayScore", "AvgTotalGoals", "StdDevTotalGoals",
                "HomeWinPct", "AwayWinPct", "DrawPct",
                "AvgHomeShots", "AvgAwayShots", "HomeShootPct", "AwayShootPct",
                "HomeSavePct", "AwaySavePct", "AvgHomeTurnovers", "AvgAwayTurnovers",
                "AvgHomeSuspensions", "AvgAwaySuspensions"
            };
        }

        /// <summary>
        /// Writes the collected summary data to a CSV file.
        /// </summary>
        private void ExportSummaryToCsv()
        {
            if (summaryExportData == null || summaryExportData.Count <= 1) // Should have header + at least one data row
            {
                Debug.LogWarning("No summary data available to export.");
                return;
            }

            string filePath = Path.Combine(Application.persistentDataPath, exportFilename);
            StringBuilder sb = new StringBuilder();

            try
            {
                Debug.Log($"Attempting to export summary results to: {filePath}");
                foreach (var row in summaryExportData)
                {
                    sb.AppendLine(string.Join(",", row));
                }
                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"Successfully exported {summaryExportData.Count - 1} summary data rows to {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to export results to CSV: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Helper to log a simple realism check message. Uses class constants.
        /// </summary>
        private void CheckStatRealism(string statName, double value, double typicalMin, double typicalMax)
        {
            string message;
            if (value >= typicalMin && value <= typicalMax)
            {
                message = $"OK ({value:F1} is within typical {typicalMin:F1}-{typicalMax:F1} range)";
            }
            else if (value < typicalMin)
            {
                message = $"WARNING - Potentially LOW ({value:F1} < typical min {typicalMin:F1})";
            }
            else // value > typicalMax
            {
                message = $"WARNING - Potentially HIGH ({value:F1} > typical max {typicalMax:F1})";
            }
            Debug.Log($"{statName}: {message}");
        }


        #endregion

        #region Helper Methods for Setup

        /// <summary>
        /// Logs the current test configuration settings.
        /// </summary>
        private void LogConfiguration()
        {
            Debug.Log("--- Test Configuration ---");
            Debug.Log($"Simulations Per Step: {numberOfSimulationsPerStep}");
            Debug.Log($"Random Seed: {(randomSeed == -1 ? "Time-based" : randomSeed.ToString())}");
            Debug.Log($"Test Ability Range: {testAbilityRange}");
            if(testAbilityRange) Debug.Log($"Home Ability Range: {homeAbilityStart} to {homeAbilityEnd} (Step: {homeAbilityStep})");
            else Debug.Log($"Fixed Home Ability: {homeAbilityStart}");
            Debug.Log($"Away Ability: {awayTeamAvgAbility}");
            Debug.Log($"Ability Variance: +/- {abilityVariance}");
            Debug.Log($"Home Tactic: {homeTacticPace} Pace, {homeDefensiveSystem} Def, {homeOffensiveFocus} Focus");
            Debug.Log($"Away Tactic: {awayTacticPace} Pace, {awayDefensiveSystem} Def, {awayOffensiveFocus} Focus");
            Debug.Log($"Export Results: {exportResultsToFile} (Filename: {exportFilename})");
            Debug.Log("-------------------------");
        }

        /// <summary>
        /// Creates a placeholder team with randomized players around a target ability.
        /// </summary>
        private TeamData SetupTestTeam(string name, int averageAbility, int teamId)
        {
            TeamData team = new TeamData { TeamID = teamId, Name = name, Reputation = 5000, Budget = 1000000, LeagueID = 1 };
            // Tactic is now set in RunBatchSimulations based on config
            team.Roster = new List<PlayerData>();

            int playersToCreate = 14; // Typical matchday squad size
            int gkCreated = 0;
            var positions = Enum.GetValues(typeof(PlayerPosition)).Cast<PlayerPosition>().ToList();

            for (int i = 0; i < playersToCreate; i++)
            {
                PlayerPosition pos;
                // Ensure at least 2 goalkeepers
                if (gkCreated < 2 && i >= playersToCreate - 2)
                {
                    pos = PlayerPosition.Goalkeeper;
                }
                else
                {
                    // Try to get a balanced roster - cycle through positions
                    pos = positions[i % positions.Count];
                    if (pos == PlayerPosition.Goalkeeper)
                    {
                        if (gkCreated >= 2)
                        {
                            // Skip GK if we already have 2, pick next non-GK
                            pos = positions[(i + 1) % positions.Count];
                            if(pos == PlayerPosition.Goalkeeper) pos = positions[(i + 2) % positions.Count];
                            if(pos == PlayerPosition.Goalkeeper) pos = PlayerPosition.CentreBack; // Failsafe
                        }
                    }
                }

                // Assign GK count *after* position selection
                if(pos == PlayerPosition.Goalkeeper) gkCreated++;


                // Create Player
                int ca = averageAbility + testRandom.Next(-abilityVariance, abilityVariance + 1); // Use variance field
                int pa = ca + testRandom.Next(3, 12); // Smaller potential gap for testing?
                ca = Mathf.Clamp(ca, 30, 99); // Clamp CA
                pa = Mathf.Clamp(pa, ca, 100); // Clamp PA, ensure PA >= CA

                PlayerData player = CreatePlaceholderPlayer(name + $" Player {i+1}", pos, ca, pa, teamId);
                team.AddPlayer(player); // AddPlayer method now handles setting TeamID internally
            }

             if (team.Roster.Count(p => p.PrimaryPosition == PlayerPosition.Goalkeeper) < 1) {
                 Debug.LogError($"Team {name} setup failed: No Goalkeeper created!"); return null;
             }
             if (team.Roster.Count < 7) {
                 Debug.LogError($"Team {name} setup failed: Only {team.Roster.Count} players created (need at least 7)!"); return null;
             }

            team.UpdateWageBill();
            // Debug.Log($"Created Test Team: {name} (ID: {teamId}), Players: {team.Roster.Count}, Avg Target Ability: {averageAbility}");
            return team;
        }

        /// <summary>
        /// Creates a placeholder player with randomized attributes based on CA estimate.
        /// Refactored for better readability and assigns PlayerID.
        /// </summary>
        private PlayerData CreatePlaceholderPlayer(string name, PlayerPosition pos, int caEstimate, int pa, int? teamId)
        {
            PlayerData player = new PlayerData();

            // --- Basic Info ---
            player.PlayerID = _nextPlayerId++; // ISSUE 1 & 3 FIX: Assign and increment unique ID
            string[] nameParts = name.Split(' ');
            player.FirstName = nameParts[0];
            player.LastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "Player";
            player.Age = testRandom.Next(19, 31);
            player.PrimaryPosition = pos;
            player.PotentialAbility = pa;
            player.CurrentTeamID = teamId; // Set TeamID (AddPlayer in TeamData might override this, but good practice)

            // --- Contract & Status ---
            player.Wage = 1000 + (caEstimate * testRandom.Next(40, 70));
            player.Morale = (float)testRandom.NextDouble() * 0.4f + 0.5f; // Range 0.5 to 0.9
            player.Condition = 1.0f;
            player.Resilience = testRandom.Next(40, 90);
            player.InjuryStatus = InjuryStatus.Healthy; // Assume healthy for tests
            player.TransferStatus = TransferStatus.Unavailable; // Default status

            // --- Attributes (Refactored for Readability) ---
            // ISSUE 2 FIX: Improved Readability
            Func<int, int, int> GetRandomAttr = (baseVal, variance) =>
                Mathf.Clamp(baseVal + testRandom.Next(-variance, variance + 1), 10, 99);

            int baseSkill = caEstimate;
            int skillVariance = 15; // How much individual skills vary around the base CA estimate

            // Technical
            player.ShootingAccuracy = GetRandomAttr(baseSkill, skillVariance);
            player.ShootingPower = GetRandomAttr(baseSkill, skillVariance);
            player.Passing = GetRandomAttr(baseSkill, skillVariance);
            player.Technique = GetRandomAttr(baseSkill, skillVariance);
            player.Dribbling = GetRandomAttr(baseSkill, skillVariance);
            player.Tackling = GetRandomAttr(baseSkill - 5, skillVariance); // Slightly lower defensive base
            player.Blocking = GetRandomAttr(baseSkill - 5, skillVariance);

            // Physical
            player.Speed = GetRandomAttr(baseSkill, skillVariance);
            player.Agility = GetRandomAttr(baseSkill, skillVariance);
            player.Strength = GetRandomAttr(baseSkill, skillVariance);
            player.Jumping = GetRandomAttr(baseSkill - 5, skillVariance);
            player.Stamina = GetRandomAttr(baseSkill + 5, skillVariance); // Slightly higher stamina base
            player.NaturalFitness = GetRandomAttr(baseSkill, skillVariance);
            // Resilience set earlier

            // Mental
            player.Composure = GetRandomAttr(baseSkill, skillVariance);
            player.Concentration = GetRandomAttr(baseSkill, skillVariance);
            player.Anticipation = GetRandomAttr(baseSkill, skillVariance);
            player.DecisionMaking = GetRandomAttr(baseSkill, skillVariance);
            player.Teamwork = GetRandomAttr(baseSkill, skillVariance);
            player.WorkRate = GetRandomAttr(baseSkill, skillVariance);
            player.Positioning = GetRandomAttr(baseSkill - 5, skillVariance); // Slightly lower positioning base
            player.Aggression = GetRandomAttr(baseSkill - 10, skillVariance); // Lower aggression base
            player.Bravery = GetRandomAttr(baseSkill, skillVariance);
            player.Leadership = GetRandomAttr(baseSkill - 15, skillVariance); // Lower leadership base

            // Goalkeeping (Set defaults, then override if GK)
            player.Reflexes = 10; player.Handling = 10; player.PositioningGK = 10;
            player.OneOnOnes = 10; player.PenaltySaving = 10; player.Throwing = 10;
            player.Communication = 10;

            if (pos == PlayerPosition.Goalkeeper)
            {
                int gkVariance = 10;
                player.Reflexes = GetRandomAttr(baseSkill + 10, gkVariance); // Boost GK stats
                player.Handling = GetRandomAttr(baseSkill + 5, gkVariance);
                player.PositioningGK = GetRandomAttr(baseSkill + 5, gkVariance);
                player.OneOnOnes = GetRandomAttr(baseSkill, gkVariance);
                player.PenaltySaving = GetRandomAttr(baseSkill - 5, gkVariance); // Slightly lower base
                player.Throwing = GetRandomAttr(baseSkill - 5, gkVariance);
                player.Communication = GetRandomAttr(baseSkill, gkVariance);
            }

            // Assign a random personality trait for testing AI variations (if applicable)
            var traits = Enum.GetValues(typeof(PlayerPersonalityTrait)).Cast<PlayerPersonalityTrait>().ToList();
            player.Personality = traits[testRandom.Next(traits.Count)];

            // Calculate initial CA based on generated attributes
            player.CalculateCurrentAbility();

            // Potential Ability was set earlier, ensure it's valid relative to new CA
            player.PotentialAbility = Mathf.Clamp(pa, player.CurrentAbility, 100);

            return player;
        }

        /// <summary> Creates a tactic based on Inspector configuration settings. </summary>
        private Tactic CreateConfiguredTactic(string name, TacticPace pace, DefensiveSystem defSystem, OffensiveFocusPlay focus)
        {
            // ISSUE 6 FIX: Use configured tactic values
            return new Tactic {
                TacticName = name,
                Pace = pace,
                DefensiveSystem = defSystem,
                FocusPlay = focus,
                // Add other tactic settings if needed for testing
                // RiskTakingLevel = 0.5f, // Example default
                // TeamAggressionLevel = 0.5f, // Example default
            };
        }

        /// <summary>
        /// Calculates the standard deviation of a list of integers.
        /// Refactored for better readability.
        /// </summary>
        private double CalculateStandardDeviation(List<int> values)
        {
            // ISSUE 2 FIX: Improved Readability
            if (values == null || values.Count < 2)
            {
                return 0; // StdDev requires at least 2 values
            }

            double average = values.Average();
            double sumOfSquaresOfDifferences = values.Sum(val => Math.Pow(val - average, 2));
            double variance = sumOfSquaresOfDifferences / (values.Count - 1); // Use n-1 for sample standard deviation

            return Math.Sqrt(variance);
        }

        #endregion

    } // End of MatchEngineTester class

    --- END OF FILE MatchEngineTester.cs ---