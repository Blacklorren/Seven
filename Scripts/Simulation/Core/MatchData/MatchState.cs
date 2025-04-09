using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using HandballManager.Data;
using HandballManager.Gameplay;
using System; // Required for ArgumentNullException
using HandballManager.Core; // For PlayerPosition and GamePhase enums

// Change from:
namespace HandballManager.Simulation.MatchData
// To:
namespace HandballManager.Simulation.Core.MatchData
{
    /// <summary>
    /// Contains the complete dynamic state of the simulated match at any given time.
    /// Includes team data, scores, phase, ball/player states, timers, and event logs.
    /// </summary>
    public class MatchState
    {
        // --- Static Match Info ---
        public TeamData HomeTeamData { get; }
        public TeamData AwayTeamData { get; }
        public Tactic HomeTactic { get; }
        public Tactic AwayTactic { get; }

        // --- Dynamic State ---
        /// <summary>Current elapsed time in the match simulation (seconds).</summary>
        public float MatchTimeSeconds { get; set; } = 0f;
        /// <summary>Current score for the home team.</summary>
        public int HomeScore { get; set; } = 0;
        /// <summary>Current score for the away team.</summary>
        public int AwayScore { get; set; } = 0;
        /// <summary>The current phase of the match simulation.</summary>
        public GamePhase CurrentPhase { get; set; } = GamePhase.PreKickOff;
        /// <summary>Team currently considered in possession. 0=Home, 1=Away, -1=None/Contested.</summary>
        public int PossessionTeamId { get; set; } = -1;

        // --- Simulation Objects ---
        /// <summary>The state of the ball.</summary>
        public SimBall Ball { get; }
        /// <summary>Dictionary mapping PlayerData ID to the dynamic SimPlayer state.</summary>
        public Dictionary<int, SimPlayer> AllPlayers { get; } = new Dictionary<int, SimPlayer>();
        /// <summary>List of home team players currently on the court.</summary>
        public List<SimPlayer> HomePlayersOnCourt { get; } = new List<SimPlayer>(7); // Initialize capacity
        /// <summary>List of away team players currently on the court.</summary>
        public List<SimPlayer> AwayPlayersOnCourt { get; } = new List<SimPlayer>(7); // Initialize capacity

        // --- Bench Players (Implemented state variables) ---
        /// <summary>List of home team players currently on the bench.</summary>
        public List<SimPlayer> HomeBench { get; } = new List<SimPlayer>();
        /// <summary>List of away team players currently on the bench.</summary>
        public List<SimPlayer> AwayBench { get; } = new List<SimPlayer>();
        // Note: Logic for substitutions needs to be implemented elsewhere (e.g., MatchSimulator or AI).

        // --- Event Log ---
        /// <summary>Log of significant events that occurred during the simulation.</summary>
        public List<MatchEvent> MatchEvents { get; } = new List<MatchEvent>();

        // --- Game Progression & Temporary State ---
        /// <summary>Flag indicating if the simulation has passed the half-time mark.</summary>
        public bool HalfTimeReached { get; set; } = false;
        /// <summary>Flag indicating if the simulation is currently in the second half.</summary>
        public bool IsSecondHalf { get; set; } = false;
        /// <summary>Stores which team (0 or 1) kicked off the first half.</summary>
        public int FirstHalfKickOffTeamId { get; set; } = -1;
        /// <summary>Stores the game phase that was active before a timeout began.</summary>
        public GamePhase PhaseBeforeTimeout { get; set; } = GamePhase.Finished; // Default safe value
        /// <summary>Countdown timer for the current timeout duration (seconds).</summary>
        public float TimeoutTimer { get; set; } = 0f;

        // --- Timeout Counts (Implemented state variables) ---
        /// <summary>Number of timeouts remaining for the home team.</summary>
        public int HomeTimeoutsRemaining { get; set; } = 3; // Default based on common rules
        /// <summary>Number of timeouts remaining for the away team.</summary>
        public int AwayTimeoutsRemaining { get; set; } = 3; // Default based on common rules
        // Note: Logic for calling timeouts and enforcing limits needs implementation elsewhere.

        // --- Randomness ---
        /// <summary>The pseudo-random number generator used for this specific match simulation instance.</summary>
        public System.Random RandomGenerator { get; }

        // --- Temporary Stats (Accumulated during simulation) ---
        /// <summary>Accumulated match statistics for the home team.</summary>
        public TeamMatchStats CurrentHomeStats { get; private set; }
        /// <summary>Accumulated match statistics for the away team.</summary>
        public TeamMatchStats CurrentAwayStats { get; private set; }

        /// <summary>
        /// Initializes a new MatchState instance.
        /// </summary>
        /// <param name="homeTeam">Home team data (required).</param>
        /// <param name="awayTeam">Away team data (required).</param>
        /// <param name="homeTactic">Home team tactic (required).</param>
        /// <param name="awayTactic">Away team tactic (required).</param>
        /// <param name="randomSeed">Seed for the simulation's random number generator.</param>
        /// <exception cref="ArgumentNullException">Thrown if required parameters (teams, tactics) are null.</exception>
        public MatchState(TeamData homeTeam, TeamData awayTeam, Tactic homeTactic, Tactic awayTactic, int randomSeed)
        {
            // --- Constructor Validation ---
            HomeTeamData = homeTeam ?? throw new ArgumentNullException(nameof(homeTeam));
            AwayTeamData = awayTeam ?? throw new ArgumentNullException(nameof(awayTeam));
            HomeTactic = homeTactic ?? throw new ArgumentNullException(nameof(homeTactic));
            AwayTactic = awayTactic ?? throw new ArgumentNullException(nameof(awayTactic));

            // Initialize critical components
            RandomGenerator = new System.Random(randomSeed);
            Ball = new SimBall(); // Ensure ball is created
            CurrentHomeStats = new TeamMatchStats(); // Ensure stats objects are created
            CurrentAwayStats = new TeamMatchStats();
        }

        // --- Utility Accessors ---

        /// <summary>
        /// Gets an enumerable collection of all players currently on the court (both teams).
        /// Uses yield return for potentially better enumeration performance compared to Concat().
        /// </summary>
        public IEnumerable<SimPlayer> PlayersOnCourt
        {
            get
            {
                // Iterate directly over the lists if they are guaranteed not to be modified
                // during the enumeration by external code accessing this property.
                if (HomePlayersOnCourt != null) {
                     foreach (var player in HomePlayersOnCourt) {
                         if (player != null) yield return player; // Add null check within loop
                     }
                }
                if (AwayPlayersOnCourt != null) {
                     foreach (var player in AwayPlayersOnCourt) {
                         if (player != null) yield return player; // Add null check within loop
                     }
                }
            }
        }

        /// <summary>
        /// Gets a simulation player object by their unique PlayerData ID.
        /// </summary>
        /// <param name="playerId">The ID of the player to retrieve.</param>
        /// <returns>The SimPlayer object, or null if not found.</returns>
        public SimPlayer GetPlayerById(int playerId)
        {
            AllPlayers.TryGetValue(playerId, out SimPlayer player);
            return player;
        }

         /// <summary>
         /// Gets the list of players currently on court for the specified team simulation ID.
         /// </summary>
         /// <param name="teamSimId">The simulation team ID (0 for home, 1 for away).</param>
         /// <returns>The list of players on court, or null if the ID is invalid or lists are null.</returns>
         public List<SimPlayer> GetTeamOnCourt(int teamSimId)
         {
             // Validate teamSimId parameter
             if (teamSimId == 0) return HomePlayersOnCourt;
             if (teamSimId == 1) return AwayPlayersOnCourt;

             Debug.LogWarning($"[MatchState] GetTeamOnCourt called with invalid teamSimId: {teamSimId}");
             return null; // Return null for invalid ID
         }

          /// <summary>
          /// Gets the list of players currently on court for the opposing team.
          /// </summary>
          /// <param name="teamSimId">The simulation team ID (0 for home, 1 for away) of the *reference* team.</param>
          /// <returns>The list of opposing players on court, or null if the ID is invalid or lists are null.</returns>
          public List<SimPlayer> GetOpposingTeamOnCourt(int teamSimId)
          {
             // Validate teamSimId parameter before determining opponent
             if (teamSimId == 0) return AwayPlayersOnCourt;
             if (teamSimId == 1) return HomePlayersOnCourt;

             Debug.LogWarning($"[MatchState] GetOpposingTeamOnCourt called with invalid teamSimId: {teamSimId}");
             return null; // Return null for invalid ID
          }

        /// <summary>
        /// Gets the goalkeeper currently on court for the specified team.
        /// </summary>
        /// <param name="teamSimId">The simulation team ID (0 for home, 1 for away).</param>
        /// <returns>The SimPlayer for the goalkeeper, or null if not found or not on court.</returns>
        public SimPlayer GetGoalkeeper(int teamSimId)
        {
            var teamList = GetTeamOnCourt(teamSimId); // Use validated getter
            // Use FirstOrDefault with null check on BaseData and IsOnCourt check
            return teamList?.FirstOrDefault(p => p?.BaseData?.PrimaryPosition == Core.PlayerPosition.Goalkeeper && p.IsOnCourt && !p.IsSuspended());
        }

         /// <summary>Helper to check if a player belongs to the Home team (TeamSimId 0).</summary>
         /// <param name="player">The player to check.</param>
         /// <returns>True if the player is on the home team, false otherwise or if player is null.</returns>
         public bool IsHomeTeam(SimPlayer player) => player?.TeamSimId == 0; // Safe access with ?.
    }
}