using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using HandballManager.Data;          // Access core data structures
using HandballManager.Simulation;     // Access MatchEngine & simulation data
using HandballManager.Gameplay;     // Access Tactic
using HandballManager.Core;         // Access Enums
using System;                       // For Math, Random

/// <summary>
/// A MonoBehaviour script designed specifically to test the MatchEngine.
/// It sets up two placeholder teams, runs multiple match simulations,
/// and logs statistical results (including team stats) to the console
/// to evaluate realism.
/// Attach this script to a GameObject in a dedicated test scene.
///
/// !! IMPORTANT !! This script ASSUMES that MatchResult has been extended
/// to include 'HomeStats' and 'AwayStats' properties of type 'TeamMatchStats',
/// and that the MatchEngine populates these stats correctly.
/// </summary>
public class MatchEngineTester : MonoBehaviour
{
    [Header("Test Configuration")]
    [Tooltip("Number of matches to simulate for statistical analysis.")]
    [SerializeField] private int numberOfSimulations = 100;

    [Tooltip("Average base ability score for Home Team players.")]
    [Range(30, 90)]
    [SerializeField] private int homeTeamAvgAbility = 70;

    [Tooltip("Average base ability score for Away Team players.")]
    [Range(30, 90)]
    [SerializeField] private int awayTeamAvgAbility = 70;

    [Tooltip("Seed for the random number generator used in tests. -1 uses a time-based seed.")]
    [SerializeField] private int randomSeed = -1;


    private MatchEngine matchEngine;
    private System.Random testRandom; // Separate random generator for test setup if needed

    // Simple unique ID generator for players created within this test
    private static int _nextPlayerId = 10000; // Start high to avoid potential clashes if running near main game

    void Start()
    {
        Debug.Log("===== MatchEngine Test Started =====");

        // Initialize Match Engine
        matchEngine = new MatchEngine();

        // Initialize Random Generator for Test Setup (if needed)
        testRandom = (randomSeed == -1) ? new System.Random() : new System.Random(randomSeed);

        // Run the simulations
        RunBatchSimulations(numberOfSimulations);

        Debug.Log("===== MatchEngine Test Finished =====");
    }

    /// <summary>
    /// Runs a specified number of match simulations and analyzes the results.
    /// </summary>
    private void RunBatchSimulations(int numSims)
    {
        if (numSims <= 0)
        {
            Debug.LogError("Number of simulations must be positive.");
            return;
        }

        Debug.Log($"Setting up teams for {numSims} simulations...");

        TeamData homeTeam = SetupTestTeam("Home Test Team", homeTeamAvgAbility, 1);
        TeamData awayTeam = SetupTestTeam("Away Test Team", awayTeamAvgAbility, 2);
        Tactic homeTactic = CreateDefaultTactic("Home Balanced");
        Tactic awayTactic = CreateDefaultTactic("Away Balanced");

        if (homeTeam == null || awayTeam == null)
        {
            Debug.LogError("Failed to create test teams.");
            return;
        }

        List<MatchResult> results = new List<MatchResult>();
        int simulationProgress = 0;
        const int LOG_INTERVAL = 10; // Log progress every 10 simulations

        Debug.Log($"Starting {numSims} simulations...");

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < numSims; i++)
        {
            // Run a single simulation
            MatchResult result = matchEngine.SimulateMatch(homeTeam, awayTeam, homeTactic, awayTactic);

            // Basic validation: Ensure stats object exists (due to assumption)
            if (result.HomeStats == null || result.AwayStats == null)
            {
                Debug.LogError($"Simulation {i+1} failed: MatchResult did not contain HomeStats/AwayStats. Ensure MatchEngine populates these!");
                // Decide whether to stop or continue testing
                // For now, add a dummy result to avoid breaking analysis, but flag it.
                result.HomeStats = new TeamMatchStats();
                result.AwayStats = new TeamMatchStats();
                // return; // Option: Stop the test run
            }
            // Basic validation: Check if goals scored match final score
            if (result.HomeStats.GoalsScored != result.HomeScore || result.AwayStats.GoalsScored != result.AwayScore) {
                 Debug.LogWarning($"Simulation {i+1} inconsistency: Score ({result.HomeScore}-{result.AwayScore}) doesn't match Stats Goals ({result.HomeStats.GoalsScored}-{result.AwayStats.GoalsScored}).");
            }

            results.Add(result);

            simulationProgress++;
            if (simulationProgress % LOG_INTERVAL == 0 || simulationProgress == numSims)
            {
                Debug.Log($"Simulation {simulationProgress}/{numSims} complete...");
            }
        }

        stopwatch.Stop();
        Debug.Log($"Finished {numSims} simulations in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");

        // Analyze and Log Results
        AnalyzeResults(results);
    }

    /// <summary>
    /// Analyzes the collected match results and logs statistics, including team stats.
    /// </summary>
    private void AnalyzeResults(List<MatchResult> results)
    {
        if (results == null || !results.Any())
        {
            Debug.LogWarning("No match results to analyze.");
            return;
        }

        Debug.Log("\n----- Match Result Analysis -----");
        int numMatches = results.Count;

        // --- Aggregate Score Statistics --- (Same as before)
        List<int> homeScores = results.Select(r => r.HomeScore).ToList();
        List<int> awayScores = results.Select(r => r.AwayScore).ToList();
        List<int> totalGoals = results.Select(r => r.HomeScore + r.AwayScore).ToList();

        double avgHomeScore = homeScores.Any() ? homeScores.Average() : 0;
        double avgAwayScore = awayScores.Any() ? awayScores.Average() : 0;
        double avgTotalGoals = totalGoals.Any() ? totalGoals.Average() : 0;

        double stdDevHome = CalculateStandardDeviation(homeScores);
        double stdDevAway = CalculateStandardDeviation(awayScores);
        double stdDevTotal = CalculateStandardDeviation(totalGoals);

        int minHome = homeScores.Any() ? homeScores.Min() : 0;
        int maxHome = homeScores.Any() ? homeScores.Max() : 0;
        int minAway = awayScores.Any() ? awayScores.Min() : 0;
        int maxAway = awayScores.Any() ? awayScores.Max() : 0;
        int minTotal = totalGoals.Any() ? totalGoals.Min() : 0;
        int maxTotal = totalGoals.Any() ? totalGoals.Max() : 0;

        // --- Aggregate Team Statistics ---
        double avgHomeShots = results.Average(r => r.HomeStats.ShotsTaken);
        double avgAwayShots = results.Average(r => r.AwayStats.ShotsTaken);
        double avgHomeSOT = results.Average(r => r.HomeStats.ShotsOnTarget);
        double avgAwaySOT = results.Average(r => r.AwayStats.ShotsOnTarget);
        double avgHomeSaves = results.Average(r => r.HomeStats.SavesMade); // Saves *by* Home GK
        double avgAwaySaves = results.Average(r => r.AwayStats.SavesMade); // Saves *by* Away GK
        double avgHomeTurnovers = results.Average(r => r.HomeStats.Turnovers);
        double avgAwayTurnovers = results.Average(r => r.AwayStats.Turnovers);
        double avgHomeFouls = results.Average(r => r.HomeStats.FoulsCommitted);
        double avgAwayFouls = results.Average(r => r.AwayStats.FoulsCommitted);
        double avgHomeSuspensions = results.Average(r => r.HomeStats.TwoMinuteSuspensions);
        double avgAwaySuspensions = results.Average(r => r.AwayStats.TwoMinuteSuspensions);

        // Calculate overall Shooting % and SOT % (Average of percentages can be misleading, better to use totals)
        float overallHomeShootPct = results.Sum(r => r.HomeStats.ShotsTaken) == 0 ? 0f :
                                   (float)results.Sum(r => r.HomeStats.GoalsScored) / results.Sum(r => r.HomeStats.ShotsTaken) * 100f;
        float overallAwayShootPct = results.Sum(r => r.AwayStats.ShotsTaken) == 0 ? 0f :
                                   (float)results.Sum(r => r.AwayStats.GoalsScored) / results.Sum(r => r.AwayStats.ShotsTaken) * 100f;
        float overallHomeSotPct = results.Sum(r => r.HomeStats.ShotsTaken) == 0 ? 0f :
                                  (float)results.Sum(r => r.HomeStats.ShotsOnTarget) / results.Sum(r => r.HomeStats.ShotsTaken) * 100f;
        float overallAwaySotPct = results.Sum(r => r.AwayStats.ShotsTaken) == 0 ? 0f :
                                  (float)results.Sum(r => r.AwayStats.ShotsOnTarget) / results.Sum(r => r.AwayStats.ShotsTaken) * 100f;
        // Save Percentage (Saves / Shots On Target Faced by opponent)
         float overallHomeSavePct = results.Sum(r => r.AwayStats.ShotsOnTarget) == 0 ? 0f :
                                   (float)results.Sum(r => r.HomeStats.SavesMade) / results.Sum(r => r.AwayStats.ShotsOnTarget) * 100f;
         float overallAwaySavePct = results.Sum(r => r.HomeStats.ShotsOnTarget) == 0 ? 0f :
                                   (float)results.Sum(r => r.AwayStats.SavesMade) / results.Sum(r => r.HomeStats.ShotsOnTarget) * 100f;


        // --- Calculate Win/Draw/Loss Percentage --- (Same as before)
        int homeWins = results.Count(r => r.HomeScore > r.AwayScore);
        int awayWins = results.Count(r => r.AwayScore > r.HomeScore);
        int draws = results.Count(r => r.HomeScore == r.AwayScore);

        double homeWinPct = (double)homeWins / numMatches * 100.0;
        double awayWinPct = (double)awayWins / numMatches * 100.0;
        double drawPct = (double)draws / numMatches * 100.0;

        // --- Log Summary ---
        Debug.Log($"Total Matches Simulated: {numMatches}");
        Debug.Log($"--- Scores ---");
        Debug.Log($"Avg Home Score: {avgHomeScore:F2} (StdDev: {stdDevHome:F2}, Min: {minHome}, Max: {maxHome})");
        Debug.Log($"Avg Away Score: {avgAwayScore:F2} (StdDev: {stdDevAway:F2}, Min: {minAway}, Max: {maxAway})");
        Debug.Log($"Avg Total Goals: {avgTotalGoals:F2} (StdDev: {stdDevTotal:F2}, Min: {minTotal}, Max: {maxTotal})");
        Debug.Log($"--- Outcomes ---");
        Debug.Log($"Home Wins: {homeWins} ({homeWinPct:F1}%) | Away Wins: {awayWins} ({awayWinPct:F1}%) | Draws: {draws} ({drawPct:F1}%)");
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
        Debug.Log($"Save %      | {overallHomeSavePct,8:F1}% | {overallAwaySavePct,8:F1}% |"); // Note: Home Save % uses Away SOT
        Debug.Log("---------------------------------");

        // --- Realism Check Notes ---
        Debug.Log("--- Realism Check Notes (Approximate Handball Values) ---");
        CheckStatRealism("Avg Total Goals", avgTotalGoals, 50, 75);
        CheckStatRealism("Draw %", drawPct, 5, 20);
        CheckStatRealism("Avg Shots (Team)", (avgHomeShots + avgAwayShots) / 2.0, 45, 70); // Shots per team
        CheckStatRealism("Overall Shooting %", (overallHomeShootPct + overallAwayShootPct) / 2.0f, 45, 65);
        CheckStatRealism("Overall Save %", (overallHomeSavePct + overallAwaySavePct) / 2.0f, 25, 40);
        CheckStatRealism("Avg Turnovers (Team)", (avgHomeTurnovers + avgAwayTurnovers) / 2.0, 8, 18);
        CheckStatRealism("Avg 2min Susp (Team)", (avgHomeSuspensions + avgAwaySuspensions) / 2.0, 1, 5);
    }

    /// <summary>
    /// Helper to log a simple realism check message.
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


    // --- Helper Methods for Setup --- (Mostly unchanged)

    /// <summary>
    /// Creates a placeholder team with randomized players around a target ability.
    /// </summary>
    private TeamData SetupTestTeam(string name, int averageAbility, int teamId)
    {
        TeamData team = new TeamData { TeamID = teamId, Name = name, Reputation = 5000, Budget = 1000000, LeagueID = 1 };
        team.CurrentTactic = CreateDefaultTactic($"{name} Tactic");
        team.Roster = new List<PlayerData>();

        int playersToCreate = 14; // Typical matchday squad size
        int gkCreated = 0;
        var positions = Enum.GetValues(typeof(PlayerPosition)).Cast<PlayerPosition>().ToList();

        for (int i = 0; i < playersToCreate; i++)
        {
            PlayerPosition pos;
            // Ensure at least 2 goalkeepers
            if (gkCreated < 2 && i >= playersToCreate - 2) {
                pos = PlayerPosition.Goalkeeper;
                gkCreated++;
            } else {
                 // Try to get a balanced roster - cycle through positions
                 pos = positions[i % positions.Count];
                 if (pos == PlayerPosition.Goalkeeper) {
                     if (gkCreated < 2) {
                         gkCreated++;
                     } else {
                         // Skip GK if we already have 2, pick next non-GK
                         pos = positions[(i + 1) % positions.Count];
                         if(pos == PlayerPosition.Goalkeeper) pos = positions[(i + 2) % positions.Count]; // Try one more
                         if(pos == PlayerPosition.Goalkeeper) pos = PlayerPosition.CentreBack; // Failsafe
                     }
                 }
            }

            int ca = averageAbility + testRandom.Next(-8, 9);
            int pa = ca + testRandom.Next(5, 16);
            ca = Mathf.Clamp(ca, 30, 95);
            pa = Mathf.Clamp(pa, ca, 100);

            PlayerData player = CreatePlaceholderPlayer(name + $" Player {i+1}", pos, ca, pa, teamId);
            team.AddPlayer(player);
        }

         if (team.Roster.Count(p => p.PrimaryPosition == PlayerPosition.Goalkeeper) < 1) {
             Debug.LogError($"Team {name} setup failed: No Goalkeeper created!"); return null;
         }
         if (team.Roster.Count < 7) {
             Debug.LogError($"Team {name} setup failed: Only {team.Roster.Count} players created (need at least 7)!"); return null;
         }

        team.UpdateWageBill();
        Debug.Log($"Created Test Team: {name} (ID: {teamId}), Players: {team.Roster.Count}, Avg Target Ability: {averageAbility}");
        return team;
    }

    /// <summary> Creates a placeholder player with randomized attributes based on CA. </summary>
    private PlayerData CreatePlaceholderPlayer(string name, PlayerPosition pos, int caEstimate, int pa, int? teamId)
    {
        PlayerData player = new PlayerData {
            FirstName = name.Split(' ')[0],
            LastName = name.Split(' ').Length > 1 ? string.Join(" ", name.Split(' ').Skip(1)) : "Player",
            Age = testRandom.Next(19, 31), PrimaryPosition = pos, PotentialAbility = pa, CurrentTeamID = teamId,
            Wage = 1000 + (caEstimate * testRandom.Next(40, 70)), Morale = (float)testRandom.NextDouble() * 0.4f + 0.5f,
            Condition = 1.0f, Resilience = testRandom.Next(40, 90)
        };
        Action<Action<int>, int> setAttr = (setter, baseVal) => { setter(Mathf.Clamp(baseVal + testRandom.Next(-15, 16), 10, 99)); };
        int baseSkill = caEstimate;
        setAttr(v => player.ShootingAccuracy = v, baseSkill); setAttr(v => player.Passing = v, baseSkill); setAttr(v => player.Technique = v, baseSkill);
        setAttr(v => player.Dribbling = v, baseSkill); setAttr(v => player.Tackling = v, baseSkill - 5); setAttr(v => player.Blocking = v, baseSkill - 5);
        setAttr(v => player.Speed = v, baseSkill); setAttr(v => player.Agility = v, baseSkill); setAttr(v => player.Strength = v, baseSkill);
        setAttr(v => player.Jumping = v, baseSkill - 5); setAttr(v => player.Stamina = v, baseSkill + 5); setAttr(v => player.NaturalFitness = v, baseSkill);
        setAttr(v => player.Composure = v, baseSkill); setAttr(v => player.Concentration = v, baseSkill); setAttr(v => player.Anticipation = v, baseSkill);
        setAttr(v => player.DecisionMaking = v, baseSkill); setAttr(v => player.Teamwork = v, baseSkill); setAttr(v => player.WorkRate = v, baseSkill);
        setAttr(v => player.Positioning = v, baseSkill - 5);
        if (pos == PlayerPosition.Goalkeeper) {
            setAttr(v => player.Reflexes = v, baseSkill + 10); setAttr(v => player.Handling = v, baseSkill + 5); setAttr(v => player.PositioningGK = v, baseSkill + 5);
            setAttr(v => player.OneOnOnes = v, baseSkill); setAttr(v => player.PenaltySaving = v, baseSkill - 10); setAttr(v => player.Throwing = v, baseSkill - 10);
            setAttr(v => player.Communication = v, baseSkill);
        } else {
            player.Reflexes = 10; player.Handling = 10; player.PositioningGK = 10; player.OneOnOnes = 10; player.PenaltySaving = 10; player.Throwing = 10; player.Communication = 10;
        }
        player.CalculateCurrentAbility(); return player;
    }

    /// <summary> Creates a basic default tactic. </summary>
    private Tactic CreateDefaultTactic(string name) { return new Tactic { TacticName = name, DefensiveSystem = DefensiveSystem.SixZero, Pace = TacticPace.Normal, FocusPlay = OffensiveFocusPlay.Balanced }; }

    /// <summary> Calculates the standard deviation of a list of integers. </summary>
    private double CalculateStandardDeviation(List<int> values) { if (values == null || values.Count < 2) return 0; double avg = values.Average(); double sumOfSquares = values.Sum(val => Math.Pow(val - avg, 2)); return Math.Sqrt(sumOfSquares / (values.Count - 1)); }
}