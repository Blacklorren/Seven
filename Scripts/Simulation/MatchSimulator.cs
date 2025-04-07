using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Engines; // SimulationUtils is likely here or just HandballManager.Simulation
using HandballManager.Core;
using System;

namespace HandballManager.Simulation
{
    /// <summary>
    /// Core class responsible for running the detailed, time-step simulation of a handball match.
    /// Manages game state, orchestrates updates (movement, AI, actions, events), and handles phase transitions.
    /// </summary>
    public class MatchSimulator
    {
        // --- Constants ---
        public const float MATCH_DURATION_MINUTES = 60f;
        public const float HALF_DURATION_SECONDS = (MATCH_DURATION_MINUTES / 2f) * SECONDS_PER_MINUTE;
        public const float FULL_DURATION_SECONDS = MATCH_DURATION_MINUTES * SECONDS_PER_MINUTE;
        public const float TIME_STEP_SECONDS = 0.1f;
        public const float SECONDS_PER_MINUTE = 60f;
        public const float TIMEOUT_DURATION_SECONDS = 60f;

        /// <summary>Defines key geometric properties of the handball pitch.</summary>
        public static class PitchGeometry {
            public const float Width = 20f;
            public const float Length = 40f;
            public static readonly Vector2 Center = new Vector2(Length / 2f, Width / 2f);
            public static readonly Vector2 HomeGoalCenter = new Vector2(0f, Width / 2f);
            public static readonly Vector2 AwayGoalCenter = new Vector2(Length, Width / 2f);
            public const float GoalAreaRadius = 6f;
            public const float FreeThrowLineRadius = 9f;
            public const float GoalWidth = 3f;
            public const float GoalHeight = 2f;
            public const float SevenMeterMarkX = 7f;
            public static Vector2 HomePenaltySpot => new Vector2(SevenMeterMarkX, Center.y);
            public static Vector2 AwayPenaltySpot => new Vector2(Length - SevenMeterMarkX, Center.y);

            /// <summary>Checks if a position is within the specified goal area (6m circle).</summary>
            public static bool IsInGoalArea(Vector2 position, bool checkHomeGoalArea) {
                Vector2 goalCenter = checkHomeGoalArea ? HomeGoalCenter : AwayGoalCenter;
                return Vector2.Distance(position, goalCenter) <= GoalAreaRadius;
            }
        }

        // --- Other Simulation Constants ---
        public const float MAX_PLAYER_SPEED = 8.0f;
        public const float BASE_STAMINA_DRAIN_PER_SECOND = 0.005f;
        public const float SPRINT_STAMINA_MULTIPLIER = 3.0f;
        public const float PASS_BASE_SPEED = 15.0f;
        public const float SHOT_BASE_SPEED = 20.0f;
        public const float INTERCEPTION_RADIUS = 1.5f;
        public const float TACKLE_RADIUS = 1.0f;
        public const float BLOCK_RADIUS = 1.0f;
        public const float LOOSE_BALL_PICKUP_RADIUS = 0.8f;
        public const float SET_PIECE_DEFENDER_DISTANCE = 3.0f;
        public const float DEF_GK_DEPTH = 0.5f;


        // --- Simulation Components ---
        private MatchState _state;
        private MovementSimulator _movementSim;
        private PlayerAIController _aiController;
        private ActionResolver _actionResolver;
        private MatchEventHandler _eventHandler;
        private TacticPositioner _tacticPositioner;

        // --- Simulation Control ---
        private bool _isInitialized = false;
        private int _randomSeed;
        /// <summary>Flag indicating if setup logic needs to run for the current phase.</summary>
        private bool _setupPending = false;

        /// <summary>
        /// Initializes a new MatchSimulator instance.
        /// </summary>
        /// <param name="seed">The random seed to use. If -1, a time-based seed is generated.</param>
        public MatchSimulator(int seed = -1)
        {
            // Use XOR with Process ID to further decorrelate seeds if multiple instances run near-simultaneously
            _randomSeed = (seed == -1) ? Environment.TickCount ^ System.Diagnostics.Process.GetCurrentProcess().Id : seed;
        }

        /// <summary>
        /// Runs the full match simulation from start to finish.
        /// </summary>
        /// <param name="homeTeam">Home team data.</param>
        /// <param name="awayTeam">Away team data.</param>
        /// <param name="homeTactic">Home team tactic.</param>
        /// <param name="awayTactic">Away team tactic.</param>
        /// <returns>The MatchResult containing score and statistics.</returns>
        public MatchResult SimulateMatch(TeamData homeTeam, TeamData awayTeam, Tactic homeTactic, Tactic awayTactic)
        {
            if (!Initialize(homeTeam, awayTeam, homeTactic, awayTactic)) {
                LogEvent("Match Initialization Failed. Returning Error Result.");
                // FinalizeResult handles null _state gracefully
                return FinalizeResult();
            }

            LogEvent("Match Simulation Started");
            int safetyCounter = 0;
            // Increased safety margin slightly, acknowledging potential interruptions
            int maxSteps = (int)((FULL_DURATION_SECONDS / TIME_STEP_SECONDS) * 2.0f);

            // --- Main Simulation Loop ---
            while (_state.CurrentPhase != GamePhase.Finished && safetyCounter < maxSteps)
            {
                try
                {
                    // --- Time Advancement & Half/Full Time Checks ---
                    float timeBeforeStep = _state.MatchTimeSeconds;
                    _state.MatchTimeSeconds += TIME_STEP_SECONDS;
                    float timeAfterStep = _state.MatchTimeSeconds;

                    // Check for phase transitions based on time first
                    if (CheckAndHandleHalfTime(timeBeforeStep, timeAfterStep)) continue; // If HT reached, skip rest of step
                    if (CheckAndHandleFullTime(timeAfterStep)) break; // If FT reached, exit loop

                    // --- Main Update Step for Active Play ---
                    UpdateSimulationStep(TIME_STEP_SECONDS);
                }
                catch (Exception ex) // Catch errors within the loop step
                {
                    // Attempt to handle error and abort simulation
                    HandleStepError("Main Simulation Loop", ex);
                    break; // Exit simulation loop
                }
                safetyCounter++;
            } // End main simulation loop

            // --- Post-Loop Checks ---
            if (safetyCounter >= maxSteps) {
                 // Log error if max steps exceeded, force finish
                 Debug.LogError("[MatchSimulator] Simulation exceeded max steps! Force finishing.");
                 if(_state != null) _state.CurrentPhase = GamePhase.Finished;
                 LogEvent("WARNING: Simulation exceeded max steps.");
            }

            LogEvent($"Match Simulation Finished: {homeTeam?.Name ?? "Home"} {_state?.HomeScore ?? 0} - {_state?.AwayScore ?? 0} {awayTeam?.Name ?? "Away"}");
            return FinalizeResult();
        }

        /// <summary>
        /// Checks if half time is reached based on elapsed time and handles the phase transition.
        /// </summary>
        /// <param name="timeBeforeStep">Match time before the current step.</param>
        /// <param name="timeAfterStep">Match time after the current step.</param>
        /// <returns>True if half time was reached and handled, signalling the main loop to continue to the next step.</returns>
        private bool CheckAndHandleHalfTime(float timeBeforeStep, float timeAfterStep)
        {
            if (_state != null && _state.CurrentPhase != GamePhase.HalfTime && // Avoid re-triggering
                !_state.HalfTimeReached &&
                timeBeforeStep < HALF_DURATION_SECONDS && timeAfterStep >= HALF_DURATION_SECONDS)
            {
                LogEvent("Half Time Reached.");
                _state.HalfTimeReached = true;
                // Transition immediately to HalfTime phase
                TransitionToPhase(GamePhase.HalfTime);
                return true; // Indicate phase changed, skip rest of current step update
            }
            return false;
        }


        /// <summary>
        /// Checks if full time is reached based on elapsed time and handles the phase transition.
        /// </summary>
        /// <param name="timeAfterStep">Match time after the current step.</param>
        /// <returns>True if full time was reached and handled, signalling the main loop to break.</returns>
        private bool CheckAndHandleFullTime(float timeAfterStep)
        {
             if (_state != null && timeAfterStep >= FULL_DURATION_SECONDS && _state.CurrentPhase != GamePhase.Finished) {
                 LogEvent("Full Time Reached.");
                 // Transition immediately to Finished phase
                 TransitionToPhase(GamePhase.Finished);
                 return true; // Indicate simulation should end
             }
             return false;
        }

        /// <summary>
        /// Performs a single time step update of the simulation logic, orchestrating component updates.
        /// Skips updates if the game is paused, finished, or in a break phase (except for timer updates during Timeout).
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last step (typically TIME_STEP_SECONDS).</param>
        private void UpdateSimulationStep(float deltaTime)
        {
            // Exit early if simulation is not in an active state
            if (!_isInitialized || _state == null || _state.CurrentPhase == GamePhase.Finished || _state.CurrentPhase == GamePhase.HalfTime)
            {
                return;
            }

            // Handle timeout timer separately
            if (_state.CurrentPhase == GamePhase.Timeout)
            {
                 try { UpdateTimers(deltaTime); } catch (Exception ex) { HandleStepError("Timers Update (Timeout)", ex); }
                 return; // Only update timers during timeout
            }

            // --- Phase Setup and Automatic Transitions ---
            // Handles entering new phases that require setup (kickoff, set piece) and immediate transitions (e.g., Kickoff -> Attack)
            try { HandlePhaseTransitions(); } catch (Exception ex) { HandleStepError("Phase Transitions", ex); return; }
            // Re-check phase after transitions, as it might have changed
             if (_state == null || _state.CurrentPhase == GamePhase.Finished || _state.CurrentPhase == GamePhase.HalfTime || _state.CurrentPhase == GamePhase.Timeout) return;

            // --- Active Gameplay Updates ---
            try { UpdateTimers(deltaTime); } catch (Exception ex) { HandleStepError("Timers Update", ex); return; }
            // Re-check phase after timer updates (e.g., suspension ends, timeout ends - handled above but double check)
             if (_state == null || _state.CurrentPhase == GamePhase.Finished || _state.CurrentPhase == GamePhase.HalfTime || _state.CurrentPhase == GamePhase.Timeout) return;

            // --- Core Simulation Logic (AI, Actions, Movement, Events) ---
            try { _aiController.UpdatePlayerDecisions(_state); } catch (Exception ex) { HandleStepError("AI Decisions", ex); return; }
            try { ResolvePendingActions(); } catch (Exception ex) { HandleStepError("Action Resolution", ex); return; }
            if (_state?.CurrentPhase == GamePhase.Finished) return; // Check again after actions resolved

            try { _movementSim.UpdateMovement(_state, deltaTime); } catch (Exception ex) { HandleStepError("Movement Update", ex); return; }
            try { CheckReactiveEvents(); } catch (Exception ex) { HandleStepError("Reactive Events Check", ex); return; }
            if (_state?.CurrentPhase == GamePhase.Finished) return; // Check again after reactive events

            try { CheckPassiveEvents(); } catch (Exception ex) { HandleStepError("Passive Events Check", ex); return; }
            // Final phase check not strictly needed here as loop condition handles it
        }

        /// <summary>
        /// Centralized method to transition the simulation to a new phase.
        /// Sets the new phase and flags if setup is required for it.
        /// </summary>
        /// <param name="newPhase">The target game phase.</param>
        private void TransitionToPhase(GamePhase newPhase)
        {
            if (_state == null || _state.CurrentPhase == newPhase) return; // Avoid redundant transitions

            // Log phase change? Optional, depends on desired log verbosity.
            // LogEvent($"Phase transition: {_state.CurrentPhase} -> {newPhase}");

            _state.CurrentPhase = newPhase;

            // Check if the new phase requires setup before the next logic step runs
            if (newPhase == GamePhase.PreKickOff || newPhase == GamePhase.HomeSetPiece ||
                newPhase == GamePhase.AwaySetPiece || newPhase == GamePhase.HomePenalty ||
                newPhase == GamePhase.AwayPenalty || newPhase == GamePhase.HalfTime)
            {
                _setupPending = true;
            }
        }


        /// <summary>
        /// Handles errors occurring within a simulation step, logs them, and forces the simulation to finish.
        /// Future Improvement: Could implement more granular error recovery if specific scenarios allow.
        /// </summary>
        /// <param name="stepName">Name of the step where error occurred.</param>
        /// <param name="ex">The exception caught.</param>
        private void HandleStepError(string stepName, Exception ex)
        {
             float currentTime = _state?.MatchTimeSeconds ?? -1f;
             // Log detailed error including the step name
             Debug.LogError($"[MatchSimulator] Error during '{stepName}' at Time {currentTime:F1}s: {ex?.Message ?? "Unknown Error"}\n{ex?.StackTrace ?? "No stack trace"}");
             if (_state != null) {
                 // Force the simulation to end immediately to prevent further issues
                 TransitionToPhase(GamePhase.Finished);
             }
             // Log the abortion event clearly
             LogEvent($"CRITICAL ERROR during {stepName} - Simulation aborted. Error: {ex?.Message ?? "Unknown Error"}");
        }


        /// <summary>
        /// Checks for all reactive events (Interceptions, Blocks, Saves, Pickups).
        /// Uses ToList() when iterating PlayersOnCourt as event handlers might modify the list (e.g., suspension).
        /// </summary>
        private void CheckReactiveEvents()
        {
            if (_state == null || _state.CurrentPhase == GamePhase.Finished) return;
            // Order matters sometimes (e.g., intercept before pickup)
            CheckForInterceptions(); if (_state?.CurrentPhase == GamePhase.Finished) return;
            CheckForBlocks(); if (_state?.CurrentPhase == GamePhase.Finished) return;
            CheckForSaves(); if (_state?.CurrentPhase == GamePhase.Finished) return;
            CheckForLooseBallPickup();
            // Final phase check implicitly handled by loop condition or next step's initial checks
        }

        // --- Initialization Methods ---

        /// <summary>
        /// Initializes the MatchSimulator state and all required components.
        /// Validates input data before proceeding.
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise.</returns>
        private bool Initialize(TeamData homeTeam, TeamData awayTeam, Tactic homeTactic, Tactic awayTactic) {
            // Initial validation of input data
            if (homeTeam?.Roster == null || awayTeam?.Roster == null || homeTactic == null || awayTactic == null ||
                homeTeam.Roster.Count < 7 || awayTeam.Roster.Count < 7) {
                 Debug.LogError($"[MatchSimulator] Initialization failed: Invalid input data. " +
                                $"HomeRoster: {homeTeam?.Roster?.Count ?? -1}, AwayRoster: {awayTeam?.Roster?.Count ?? -1}, " +
                                $"HomeTactic: {homeTactic != null}, AwayTactic: {awayTactic != null}");
                 return false;
            }

            // Initialize components and state object
            try {
                 _state = new MatchState(homeTeam, awayTeam, homeTactic, awayTactic, _randomSeed);
                 _movementSim = new MovementSimulator();
                 _tacticPositioner = new TacticPositioner();
                 _actionResolver = new ActionResolver();
                 _aiController = new PlayerAIController(_tacticPositioner, _actionResolver);
                 _eventHandler = new MatchEventHandler(this); // Pass self for logging etc.
            } catch (Exception ex) {
                // Log detailed error during component creation
                Debug.LogError($"[MatchSimulator] Exception during component initialization: {ex.Message}\n{ex.StackTrace}");
                _state = null; // Ensure state is null on failure
                return false;
            }

            // Populate player data and select starting lineups
            if (!PopulateAllPlayers() || !SelectStartingLineups()) {
                 _state = null; // Ensure state is null if setup fails
                 return false;
            }

            // Set initial phase and trigger initial setup
            TransitionToPhase(GamePhase.PreKickOff); // Will set _setupPending = true
            HandlePhaseTransitions(); // Run initial PreKickOff setup immediately

            _isInitialized = true;
            Debug.Log($"[MatchSimulator] Initialization complete. Seed: {_randomSeed}");
            return true;
         }

        /// <summary>
        /// Creates SimPlayer instances for all players in both teams and adds them to the central dictionary.
        /// </summary>
        /// <returns>True if successful, false if critical errors occurred during player creation.</returns>
        private bool PopulateAllPlayers()
        {
            if (_state == null) return false;
            bool success = true;
            try {
                if (!InitializeTeamPlayers(_state.HomeTeamData, 0)) success = false;
                if (!InitializeTeamPlayers(_state.AwayTeamData, 1)) success = false;
            } catch (Exception ex) {
                // Catch potential exceptions during team player initialization
                Debug.LogError($"[MatchSimulator] Unexpected error populating players: {ex.Message}");
                success = false;
            }
            if (!success) {
                 Debug.LogError("[MatchSimulator] Failed to successfully populate players for both teams.");
            }
            return success;
        }

        /// <summary>
        /// Populates SimPlayers for a single team roster. Validates PlayerData.
        /// </summary>
        /// <param name="team">The TeamData containing the roster.</param>
        /// <param name="teamSimId">The simulation team ID (0=home, 1=away).</param>
        /// <returns>True if successful, false if critical errors occurred.</returns>
        private bool InitializeTeamPlayers(TeamData team, int teamSimId) {
            // state and state.AllPlayers null checks done in calling methods
            bool success = true;
            // team and team.Roster null checks done in Initialize
            foreach (var playerData in team.Roster) {
                 // Validate individual player data before creating SimPlayer
                 if (playerData == null || playerData.PlayerID <= 0) {
                     Debug.LogWarning($"[MatchSimulator] Skipping invalid player data (null or ID <= 0) in team {team.Name}.");
                     continue; // Skip this player, but continue processing others
                 }
                 // Prevent adding duplicates
                 if (!_state.AllPlayers.ContainsKey(playerData.PlayerID)) {
                     try {
                        // Create and add the SimPlayer
                        var simPlayer = new SimPlayer(playerData, teamSimId);
                        _state.AllPlayers.Add(playerData.PlayerID, simPlayer);
                     } catch (Exception ex) {
                          // Log error during SimPlayer creation or dictionary add
                          Debug.LogError($"[MatchSimulator] Error creating/adding SimPlayer for {playerData.FullName} (ID:{playerData.PlayerID}): {ex.Message}");
                          success = false; // Mark failure, but allow loop to continue
                     }
                 } else {
                      // Log warning if player ID already exists (should ideally not happen with good data)
                      Debug.LogWarning($"[MatchSimulator] Player {playerData.Name} (ID: {playerData.PlayerID}) already exists in AllPlayers dictionary. Skipping duplicate add.");
                 }
            }
            return success;
         }

        /// <summary>
        /// Selects starting lineups for both teams. Ensures minimum players and required roles (GK).
        /// Future Improvement: Consider fatigue, form, tactical suitability.
        /// </summary>
        /// <returns>True if valid lineups were selected for both teams, false otherwise.</returns>
        private bool SelectStartingLineups()
        {
             if (_state == null) return false;
             try {
                 // Select for each team
                 if (!SelectStartingLineup(_state.HomeTeamData, 0)) return false;
                 if (!SelectStartingLineup(_state.AwayTeamData, 1)) return false;

                 // Final validation of lineup sizes
                 if (_state.HomePlayersOnCourt?.Count != 7 || _state.AwayPlayersOnCourt?.Count != 7) { // Check exact count
                     Debug.LogError($"[MatchSimulator] Initialization failed: Final lineup count incorrect. " +
                                    $"Home: {_state.HomePlayersOnCourt?.Count ?? -1}, Away: {_state.AwayPlayersOnCourt?.Count ?? -1}");
                     return false;
                 }
                 return true; // Success
             } catch (Exception ex) {
                 // Catch unexpected errors during lineup selection
                 Debug.LogError($"[MatchSimulator] Unexpected error selecting starting lineups: {ex.Message}");
                 return false;
             }
        }

        /// <summary>
        /// Selects the starting 7 players for a single team based on availability, role (GK), and ability.
        /// </summary>
        /// <returns>True if a valid 7-player lineup was selected, false otherwise.</returns>
        private bool SelectStartingLineup(TeamData team, int teamSimId) {
             // Prerequisite null checks for state, AllPlayers, team, roster done in calling methods
             var lineup = (teamSimId == 0) ? _state.HomePlayersOnCourt : _state.AwayPlayersOnCourt;
             // lineup should not be null if state was initialized correctly
             lineup.Clear();

             // Get available players (not injured) for this team
             var candidates = _state.AllPlayers.Values
                 .Where(p => p?.TeamSimId == teamSimId && p.BaseData != null && !p.BaseData.IsInjured())
                 .ToList(); // ToList needed for removing candidates

             // --- Goalkeeper Selection ---
             var gk = candidates
                 .Where(p => p.BaseData.PrimaryPosition == PlayerPosition.Goalkeeper)
                 .OrderByDescending(p => p.BaseData.CurrentAbility) // Pick best available GK
                 .FirstOrDefault();
             if (gk == null) { Debug.LogError($"[MatchSimulator] Team {team.Name} has no available Goalkeeper for starting lineup!"); return false; }
             lineup.Add(gk); candidates.Remove(gk);

             // --- Field Player Selection ---
             int fieldPlayersNeeded = 6;
             var selectedFieldPlayers = new List<SimPlayer>();
             var neededPositions = Enum.GetValues(typeof(PlayerPosition)).Cast<PlayerPosition>().Where(p => p != PlayerPosition.Goalkeeper).ToList();

             // 1. Try to fill each position with the best available candidate for that role
             foreach(var pos in neededPositions) {
                 var playerForPos = candidates
                     .Where(p => p.BaseData.PrimaryPosition == pos)
                     .OrderByDescending(p => p.BaseData.CurrentAbility) // Best CA for the position
                     .FirstOrDefault();
                 if (playerForPos != null) {
                      // Avoid adding duplicates if a player was somehow considered twice
                      if (!selectedFieldPlayers.Contains(playerForPos)) {
                         selectedFieldPlayers.Add(playerForPos);
                         candidates.Remove(playerForPos); // Remove from candidates pool
                         if (selectedFieldPlayers.Count == fieldPlayersNeeded) break; // Stop if we have enough
                      }
                 }
             }

             // 2. Fill remaining slots with the best remaining players, regardless of primary position
             int remainingNeeded = fieldPlayersNeeded - selectedFieldPlayers.Count;
             if (remainingNeeded > 0) {
                 if (candidates.Count >= remainingNeeded) {
                      // Add best remaining players based on Current Ability
                      selectedFieldPlayers.AddRange(candidates.OrderByDescending(p => p.BaseData.CurrentAbility).Take(remainingNeeded));
                 }
                 else {
                      // Critical error: Not enough players available even after considering all roles
                      Debug.LogError($"[MatchSimulator] Team {team.Name} does not have enough available field players ({candidates.Count}) " +
                                     $"to fill remaining {remainingNeeded} lineup slots!");
                      return false;
                 }
             }

             // Add selected field players to the final lineup
             lineup.AddRange(selectedFieldPlayers.Take(fieldPlayersNeeded)); // Ensure exactly 6 field players are added

             // Final check and state setting
             if(lineup.Count != 7) { Debug.LogError($"[MatchSimulator] Team {team.Name} lineup selection failed, final count is {lineup.Count} players."); return false; }

             // Set initial state for players on court
             foreach (var player in lineup) {
                 // player null check implicitly done by selection logic finding non-null candidates
                 player.IsOnCourt = true; player.Position = Vector2.zero; // Actual position set during phase setup
             }
             LogEvent($"Team {team.Name} lineup selected ({lineup.Count} players).", team?.TeamID ?? -1);
             return true;
        }

        // --- Phase Transition and Setup Methods ---

        /// <summary>
        /// Handles transitions between game phases and triggers necessary setup logic.
        /// Uses the _setupPending flag to ensure setup logic runs once upon entering certain phases.
        /// Also handles automatic phase transitions (e.g., Kickoff -> Attack).
        /// </summary>
        private void HandlePhaseTransitions()
        {
            if (_state == null) return;
            GamePhase phaseBeforeSetup = _state.CurrentPhase;

            // Execute setup logic if flagged for the current phase
            if (_setupPending)
            {
                _setupPending = false; // Consume flag immediately
                if (!ExecutePhaseSetup(phaseBeforeSetup))
                {
                    // If setup fails, log error and transition to a safe fallback state
                    Debug.LogError($"[MatchSimulator] Setup failed for phase {phaseBeforeSetup}. Reverting to ContestedBall.");
                    TransitionToPhase(GamePhase.ContestedBall);
                    // Ensure possession is cleared as state is uncertain
                    _state.PossessionTeamId = -1;
                    if(_state.Ball.Holder != null) { _state.Ball.Holder.HasBall = false; _state.Ball.Holder = null; }
                    return; // Stop further processing this step
                }
            }

            // Check for automatic transitions based on the phase *after* setup might have occurred
            ExecuteAutomaticPhaseTransitions(_state.CurrentPhase);
        }

        /// <summary>
        /// Executes the setup logic specific to the entered game phase.
        /// </summary>
        /// <param name="currentPhase">The phase requiring setup.</param>
        /// <returns>True if setup was successful or not needed, false if setup failed critically.</returns>
        private bool ExecutePhaseSetup(GamePhase currentPhase)
        {
            if (_state == null) { Debug.LogError("[MatchSimulator] Cannot execute phase setup: MatchState is null."); return false; }

            bool setupSuccess = true;
            // Use a switch statement for clarity on phase-specific setup logic
            switch (currentPhase)
            {
                case GamePhase.PreKickOff:
                    // Determine starting team for kickoff
                    int startingTeamId = DetermineKickoffTeam();
                    setupSuccess = SetupForKickOff(startingTeamId);
                    if (setupSuccess) LogEvent($"Setup for Kickoff. Team {startingTeamId} starts.", GetTeamIdFromSimId(startingTeamId));
                    break;
                case GamePhase.HomeSetPiece: // Fallthrough
                case GamePhase.AwaySetPiece:
                     setupSuccess = SetupForSetPiece();
                     if (setupSuccess) LogEvent($"Setup for Set Piece ({currentPhase}).");
                     break;
                case GamePhase.HomePenalty: // Fallthrough
                case GamePhase.AwayPenalty:
                     setupSuccess = SetupForPenalty();
                     if (setupSuccess) LogEvent($"Setup for Penalty ({currentPhase}).");
                     break;
                case GamePhase.HalfTime:
                     setupSuccess = SetupForHalfTime();
                     if (setupSuccess) LogEvent("Half Time setup actions completed.");
                     break;
                 // Phases that don't require specific setup logic upon entry
                 case GamePhase.KickOff:
                 case GamePhase.HomeAttack:
                 case GamePhase.AwayAttack:
                 case GamePhase.TransitionToHomeAttack:
                 case GamePhase.TransitionToAwayAttack:
                 case GamePhase.ContestedBall:
                 case GamePhase.Timeout: // Timeout setup handled by timer logic start/end
                 case GamePhase.Finished:
                      // No setup action needed
                      break;
                 default:
                      Debug.LogWarning($"[MatchSimulator] Unhandled phase in ExecutePhaseSetup: {currentPhase}");
                      break;
            }
            return setupSuccess;
        }

        /// <summary>
        /// Determines which team starts with the ball for a kickoff.
        /// </summary>
        /// <returns>The simulation ID (0 or 1) of the team kicking off.</returns>
        private int DetermineKickoffTeam()
        {
             if (_state.FirstHalfKickOffTeamId == -1)
             {
                 // First kickoff of the match: Random selection
                 int startingTeamId = _state.RandomGenerator.Next(0, 2);
                 _state.FirstHalfKickOffTeamId = startingTeamId; // Store who started first half
                 return startingTeamId;
             }
             else if (_state.IsSecondHalf)
             {
                 // Second half kickoff: Opposite team starts
                 return 1 - _state.FirstHalfKickOffTeamId;
             }
             else
             {
                 // Kickoff after a goal: Team that conceded restarts
                 // _state.PossessionTeamId should already be set correctly by HandleGoalScored to the conceding team
                 return _state.PossessionTeamId;
             }
        }

        /// <summary>Helper to get the actual TeamID for logging based on SimID.</summary>
        private int? GetTeamIdFromSimId(int simId)
        {
             if (_state == null) return null;
             if (simId == 0) return _state.HomeTeamData?.TeamID;
             if (simId == 1) return _state.AwayTeamData?.TeamID;
             return null;
        }


        /// <summary>
        /// Executes automatic phase transitions that happen immediately after entering certain phases.
        /// (e.g., KickOff -> Attack, Transition -> Attack).
        /// </summary>
        /// <param name="currentPhase">The current phase *after* setup logic ran this step.</param>
        private void ExecuteAutomaticPhaseTransitions(GamePhase currentPhase)
        {
             if (_state == null) return;

             // Avoid automatic transitions out of paused/break/end states
             if (currentPhase == GamePhase.Finished || currentPhase == GamePhase.HalfTime || currentPhase == GamePhase.Timeout) {
                 return;
             }

             GamePhase nextPhase = currentPhase; // Start assuming no change

             switch (currentPhase)
             {
                 case GamePhase.KickOff:
                      // After kickoff setup, immediately transition to the appropriate attack phase
                      nextPhase = (_state.PossessionTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack;
                      break;
                 case GamePhase.TransitionToHomeAttack:
                      nextPhase = GamePhase.HomeAttack;
                      break;
                 case GamePhase.TransitionToAwayAttack:
                      nextPhase = GamePhase.AwayAttack;
                      break;
                  // Add other immediate transitions if needed
             }

             // If the phase should change automatically, transition to it
             if (nextPhase != currentPhase) {
                  TransitionToPhase(nextPhase);
             }
        }


        /// <summary>Sets player positions and ball state for a kickoff. Returns true on success.</summary>
        /// <param name="startingTeamId">The ID (0 or 1) of the team starting with the ball.</param>
        /// <returns>True if setup was successful, false otherwise.</returns>
        private bool SetupForKickOff(int startingTeamId)
        {
            if (_state == null || _state.Ball == null) return false;
            _state.PossessionTeamId = startingTeamId; // Team starting with ball
            try {
                // Position players in formation
                PlacePlayersInFormation(_state.HomePlayersOnCourt.ToList(), _state.HomeTactic, true, true);
                PlacePlayersInFormation(_state.AwayPlayersOnCourt.ToList(), _state.AwayTactic, false, true);
            } catch (Exception ex) { Debug.LogError($"Error placing players in formation for kickoff: {ex.Message}"); return false; }

            // Reset ball state at center
            _state.Ball.Stop(); _state.Ball.Position = PitchGeometry.Center;
            _state.Ball.LastShooter = null; _state.Ball.ResetPassContext();

            // Find player to take kickoff (typically CentreBack)
            SimPlayer startingPlayer = FindPlayerByPosition(_state.GetTeamOnCourt(startingTeamId), PlayerPosition.CentreBack)
                                   ?? _state.GetTeamOnCourt(startingTeamId)?.FirstOrDefault(p => p != null && p.IsOnCourt && !p.IsGoalkeeper());
            if (startingPlayer == null) {
                Debug.LogError($"Could not find starting player for kickoff for Team {startingTeamId}");
                _state.Ball.MakeLoose(PitchGeometry.Center, Vector2.zero, -1); // Ball loose if no player found
                TransitionToPhase(GamePhase.ContestedBall); // Revert to contested if setup fails
                return false;
            }

            // Position kickoff player and give ball
            startingPlayer.Position = PitchGeometry.Center + Vector2.right * (startingTeamId == 0 ? -0.1f : 0.1f); // Offset slightly
            startingPlayer.TargetPosition = startingPlayer.Position; startingPlayer.CurrentAction = PlayerAction.Idle;
            _state.Ball.SetPossession(startingPlayer); // Ball held by starting player

            // Set phase AFTER setup is complete
            _state.CurrentPhase = GamePhase.KickOff;

            // Ensure other players are idle
            foreach(var p in _state.PlayersOnCourt) {
                 if(p != null && p != startingPlayer && !p.IsSuspended()) { p.CurrentAction = PlayerAction.Idle; }
            }
            return true;
        }

        /// <summary>Positions players for a free throw. Returns true on success.</summary>
        /// <returns>True if setup was successful, false otherwise.</returns>
        private bool SetupForSetPiece()
        {
            if (_state == null || _state.Ball == null) return false;
            int attackingTeamId = _state.PossessionTeamId;
            // Defending team ID calculation
            int defendingTeamId = 1 - attackingTeamId;
            if (attackingTeamId == -1) {
                Debug.LogError("Cannot setup Set Piece: PossessionTeamId is -1. Reverting to Contested.");
                TransitionToPhase(GamePhase.ContestedBall);
                return false;
            }
            Vector2 ballPos = _state.Ball.Position; // Use ball's current position

            // Find closest eligible thrower
            SimPlayer thrower = _state.GetTeamOnCourt(attackingTeamId)?
                                   .Where(p => p != null && p.IsOnCourt && !p.IsSuspended() && !p.IsGoalkeeper())
                                   .OrderBy(p => Vector2.Distance(p.Position, ballPos))
                                   .FirstOrDefault();
            if (thrower == null) {
                Debug.LogError($"Cannot find thrower for Set Piece Team {attackingTeamId}. Reverting to Contested.");
                 _state.Ball.MakeLoose(ballPos, Vector2.zero, -1); // Make ball loose
                 TransitionToPhase(GamePhase.ContestedBall);
                 return false;
            }

            // Setup thrower
            _state.Ball.SetPossession(thrower);
            // Use deterministic offset instead of random
            thrower.Position = ballPos + Vector2.one * 0.05f; // Small offset for clarity
            thrower.TargetPosition = thrower.Position; thrower.CurrentAction = PlayerAction.Idle;

            // Position other players using TacticPositioner and enforce defender distance
            foreach (var player in _state.PlayersOnCourt.ToList()) {
                 if (player == null || player == thrower || player.IsSuspended()) continue;
                 try {
                    Vector2 targetTacticalPos = _tacticPositioner.GetPlayerTargetPosition(player, _state);
                    player.Position = targetTacticalPos;

                    // Adjust defenders to be >= 3m away from the ball
                    if (player.TeamSimId == defendingTeamId) {
                        Vector2 vecFromBall = player.Position - ballPos;
                        float currentDistSq = vecFromBall.sqrMagnitude;
                        float requiredDistSq = SET_PIECE_DEFENDER_DISTANCE * SET_PIECE_DEFENDER_DISTANCE;
                        if (currentDistSq > 0.01f && currentDistSq < requiredDistSq) {
                             // Move player back along the line from the ball
                             player.Position = ballPos + vecFromBall.normalized * SET_PIECE_DEFENDER_DISTANCE * 1.05f; // Add buffer
                        }
                    }
                    // Set final state
                    player.CurrentAction = PlayerAction.Idle; player.TargetPosition = player.Position; player.Velocity = Vector2.zero;
                 } catch (Exception ex) { Debug.LogError($"Error positioning player {player.GetPlayerId()} for set piece: {ex.Message}"); /* Continue positioning others */ }
            }
            // Phase remains HomeSetPiece/AwaySetPiece
            return true;
        }


        /// <summary>Positions players for a 7m penalty throw. Returns true on success.</summary>
        /// <returns>True if setup was successful, false otherwise.</returns>
        private bool SetupForPenalty()
        {
            if (_state == null || _state.Ball == null) return false;
            int shootingTeamId = _state.PossessionTeamId;
            int defendingTeamId = 1 - shootingTeamId;
            if (shootingTeamId == -1) {
                 Debug.LogError("Cannot setup Penalty: PossessionTeamId is -1. Reverting to Contested.");
                 TransitionToPhase(GamePhase.ContestedBall);
                 return false;
            }
            bool shootingHome = (shootingTeamId == 0);

             Vector2 penaltySpot = shootingHome ? PitchGeometry.AwayPenaltySpot : PitchGeometry.HomePenaltySpot;
             _state.Ball.Stop(); _state.Ball.Position = penaltySpot; _state.Ball.Holder = null;

              // Find best shooter
              SimPlayer shooter = _state.GetTeamOnCourt(shootingTeamId)?
                                  .Where(p => p != null && p.IsOnCourt && !p.IsSuspended() && !p.IsGoalkeeper())
                                  .OrderByDescending(p => p.BaseData?.ShootingAccuracy ?? 0) // Simple criteria
                                  .FirstOrDefault();
             if (shooter == null) {
                 Debug.LogError($"Cannot find penalty shooter for Team {shootingTeamId}. Reverting to Contested.");
                 _state.Ball.MakeLoose(penaltySpot, Vector2.zero, -1);
                 TransitionToPhase(GamePhase.ContestedBall);
                 return false;
             }

             // Position shooter and GK
             shooter.Position = penaltySpot + Vector2.right * (shootingHome ? -0.2f : 0.2f); // Slightly behind
             shooter.TargetPosition = shooter.Position; shooter.CurrentAction = PlayerAction.PreparingShot; shooter.ActionTimer = 1.0f; // Penalty prep time

             SimPlayer gk = _state.GetGoalkeeper(defendingTeamId);
             if (gk != null) {
                  Vector2 goalCenter = defendingTeamId == 0 ? PitchGeometry.HomeGoalCenter : PitchGeometry.AwayGoalCenter;
                  gk.Position = goalCenter + Vector2.right * (defendingTeamId == 0 ? DEF_GK_DEPTH : -DEF_GK_DEPTH); // On line
                  gk.TargetPosition = gk.Position; gk.CurrentAction = PlayerAction.GoalkeeperPositioning;
             } else { Debug.LogWarning($"No Goalkeeper found for defending team {defendingTeamId} during penalty setup."); }

             // Position other players behind 9m line
             Vector2 opponentGoalCenter = shootingHome ? PitchGeometry.AwayGoalCenter : PitchGeometry.HomeGoalCenter;
             float freeThrowLineX = opponentGoalCenter.x + (shootingHome ? -PitchGeometry.FreeThrowLineRadius : PitchGeometry.FreeThrowLineRadius);

             foreach (var player in _state.PlayersOnCourt.ToList()) {
                  if (player == null || player == shooter || player == gk || player.IsSuspended()) continue;
                  try {
                     player.Position = _tacticPositioner.GetPlayerTargetPosition(player, _state); // Base position
                     // Ensure player is behind the free throw line X coordinate relative to the goal being attacked
                     if ((shootingHome && player.Position.x >= freeThrowLineX) || (!shootingHome && player.Position.x <= freeThrowLineX)) {
                          // Use deterministic offset instead of random
                          float offsetX = shootingHome ? -(0.5f + (player.GetPlayerId() % 5) * 0.5f) : (0.5f + (player.GetPlayerId() % 5) * 0.5f);
                          player.Position.x = freeThrowLineX + offsetX;
                     }
                     player.CurrentAction = PlayerAction.Idle; player.TargetPosition = player.Position; player.Velocity = Vector2.zero;
                  } catch (Exception ex) { Debug.LogError($"Error positioning player {player.GetPlayerId()} for penalty: {ex.Message}"); /* Continue */ }
             }
             // Phase remains HomePenalty/AwayPenalty
             return true;
        }


         /// <summary>Handles half-time logic: recovers stamina, sets state for second half kickoff. Returns true.</summary>
         /// <returns>True if setup was successful, false otherwise.</returns>
         private bool SetupForHalfTime()
         {
             if (_state == null) return false;
             // Iterate ALL players (including bench if implemented) using the main dictionary
             foreach (var player in _state.AllPlayers.Values) {
                 if (player == null || player.BaseData == null) continue;
                 try {
                    float recoveryAmount = (1.0f - player.Stamina) * 0.4f; // Base 40% recovery of missing stamina
                    recoveryAmount *= Mathf.Lerp(0.8f, 1.2f, (player.BaseData.NaturalFitness > 0 ? player.BaseData.NaturalFitness : 50f) / 100f);
                    player.Stamina = Mathf.Clamp01(player.Stamina + recoveryAmount);
                    player.UpdateEffectiveSpeed(); // Update speed based on new stamina
                 } catch (Exception ex) { Debug.LogError($"Error recovering stamina for player {player.GetPlayerId()}: {ex.Message}"); }
             }
             _state.IsSecondHalf = true;
             // Set state ready for the next half's kickoff sequence
             TransitionToPhase(GamePhase.PreKickOff); // This will set _setupPending = true
             return true;
         }

        // --- Update Methods ---

         /// <summary>Handles Timeout timer countdown and Suspension timer countdown/re-entry.</summary>
         /// <param name="deltaTime">Time elapsed since last update.</param>
        private void UpdateTimers(float deltaTime)
        {
            if (_state == null) return;
            // --- Timeout Timer ---
            if (_state.CurrentPhase == GamePhase.Timeout) {
                _state.TimeoutTimer -= deltaTime;
                if (_state.TimeoutTimer <= 0f) {
                    LogEvent("Timeout ended.");
                    _state.TimeoutTimer = 0f;
                    // Restore the phase that was active before the timeout
                    TransitionToPhase(_state.PhaseBeforeTimeout);
                    // _setupPending will be set by TransitionToPhase if needed
                }
                return; // Only timeout timer updates during a timeout
            }

            // --- Player Timers (Suspension, Action Prep) ---
             // Iterate over a copy of the values in case AllPlayers changes (less likely)
             // Removing unnecessary ToList() here if AllPlayers isn't modified during the loop.
             foreach(var player in _state.AllPlayers.Values) {
                 if (player == null) continue;
                 try {
                     // --- Suspension Timer & Re-entry Logic ---
                     if(player.IsSuspended() && player.SuspensionTimer > 0) {
                         player.SuspensionTimer -= deltaTime;
                         if(player.SuspensionTimer <= 0f) {
                               player.SuspensionTimer = 0f; // Clear timer

                               // Attempt re-entry if team is short-handed
                               List<SimPlayer> teamOnCourt = _state.GetTeamOnCourt(player.TeamSimId);
                               if (teamOnCourt != null && teamOnCourt.Count < 7) {
                                   // Player can re-enter
                                   if (!teamOnCourt.Contains(player)) { teamOnCourt.Add(player); } // Add if not already present
                                   player.IsOnCourt = true;
                                   // Position near bench (deterministic placement)
                                   player.Position = player.TeamSimId == 0 ? new Vector2(2f, 1f) : new Vector2(PitchGeometry.Length - 2f, 1f);
                                   player.TargetPosition = player.Position;
                                   player.CurrentAction = PlayerAction.Idle;
                                   LogEvent($"Player {player.BaseData?.Name ?? "Unknown"} re-enters after suspension.", player.GetTeamId(), player.GetPlayerId());
                               } else {
                                   // Team full or list error, player stays off court
                                   player.IsOnCourt = false;
                                   player.CurrentAction = PlayerAction.Idle;
                                   if (teamOnCourt != null && teamOnCourt.Contains(player)) { teamOnCourt.Remove(player); } // Ensure removed
                                   LogEvent($"Player {player.BaseData?.Name ?? "Unknown"} suspension ended, but team is full.", player.GetTeamId(), player.GetPlayerId());
                               }
                         }
                     }
                     // --- Action Preparation Timer ---
                     if(player.IsOnCourt && !player.IsSuspended() && player.ActionTimer > 0) { // Ensure player is active
                         player.ActionTimer -= deltaTime;
                         if(player.ActionTimer <= 0f) player.ActionTimer = 0f; // Clamp to zero
                     }
                 } catch (Exception ex) { Debug.LogError($"Error updating timers for player {player.GetPlayerId()}: {ex.Message}"); }
             }
        }


        /// <summary>
        /// Resolves actions for players whose action timer has completed.
        /// Uses ToList() on PlayersOnCourt for safety as HandleActionResult might modify the court list (suspension).
        /// </summary>
        private void ResolvePendingActions() {
            if (_state == null) return;
            foreach (var player in _state.PlayersOnCourt.ToList()) { // Iterate copy for safety
                if (player == null || player.IsSuspended()) continue;
                if (player.ActionTimer <= 0f) {
                    PlayerAction actionToResolve = player.CurrentAction;
                    // Resolve actions with timers
                    if (actionToResolve == PlayerAction.PreparingPass ||
                        actionToResolve == PlayerAction.PreparingShot ||
                        actionToResolve == PlayerAction.AttemptingTackle)
                    {
                         try {
                            // Resolve the action using ActionResolver
                            ActionResult result = _actionResolver.ResolvePreparedAction(player, _state);
                            // Update state based on the result using EventHandler
                            _eventHandler.HandleActionResult(result, _state);
                         } catch (Exception ex) {
                             HandleStepError($"Action Resolution ({actionToResolve})", ex);
                             // Attempt graceful recovery by resetting the player's state after error
                             try { _eventHandler?.ResetPlayerActionState(player, ActionResultOutcome.Failure); } catch {}
                         }

                         // If the game ended due to the action, stop processing further actions
                         if (_state?.CurrentPhase == GamePhase.Finished) return;
                    }
                }
            }
        }

        // --- Reactive Check Methods ---

        /// <summary>
        /// Checks for potential pass interceptions. Calls event handler if successful.
        /// Iterates over a copy of the opponent list for safety during potential state changes.
        /// </summary>
        private void CheckForInterceptions() {
            if (_state?.Ball?.Passer == null || !_state.Ball.IsInFlight || _state.Ball.IntendedTarget == null) return; // Combined null checks

            var potentialInterceptors = _state.GetOpposingTeamOnCourt(_state.Ball.Passer.TeamSimId)?.ToList();
            if (potentialInterceptors == null) return;

            foreach (var defender in potentialInterceptors) {
                if (defender == null || defender.IsSuspended()) continue;
                try {
                    float interceptChance = _actionResolver.CalculateInterceptionChance(defender, _state.Ball, _state);
                    // Use the deterministic random generator from the state
                    if (interceptChance > 0f && _state.RandomGenerator.NextDouble() < interceptChance)
                    {
                        ActionResult result = new ActionResult { Outcome = ActionResultOutcome.Intercepted, PrimaryPlayer = defender, SecondaryPlayer = _state.Ball.Passer, ImpactPosition = defender.Position };
                        _eventHandler.HandleActionResult(result, _state);
                        return; // Interception occurred, stop checking others for this pass
                    }
                    // If defender was attempting intercept but failed the random check, reset their state
                    if (defender.CurrentAction == PlayerAction.AttemptingIntercept) {
                         _eventHandler.ResetPlayerActionState(defender, ActionResultOutcome.Failure);
                    }
                } catch (Exception ex) { Debug.LogError($"Error checking interception for defender {defender.GetPlayerId()}: {ex.Message}"); }
            }
         }


        /// <summary>
        /// Checks for potential shot blocks by field players. Calls event handler if successful.
        /// Iterates over a copy of the opponent list for safety during potential state changes.
        /// </summary>
        private void CheckForBlocks() {
             if (_state?.Ball?.LastShooter == null || !_state.Ball.IsInFlight) return; // Combined null checks

             var potentialBlockers = _state.GetOpposingTeamOnCourt(_state.Ball.LastShooter.TeamSimId)?.ToList();
             if (potentialBlockers == null) return;
             Vector2 targetGoal = _state.Ball.LastShooter.TeamSimId == 0 ? PitchGeometry.AwayGoalCenter : PitchGeometry.HomeGoalCenter;

             foreach(var defender in potentialBlockers) {
                 if (defender == null || defender.IsSuspended() || defender.IsGoalkeeper()) continue;
                 try {
                     float distToBall = Vector2.Distance(defender.Position, _state.Ball.Position);
                     if (distToBall < BLOCK_RADIUS * 1.5f) {
                         Vector2 shooterPos = _state.Ball.LastShooter.Position; // Null checked earlier
                         float distToLine = SimulationUtils.CalculateDistanceToLine(defender.Position, shooterPos, targetGoal);

                         if(distToLine < BLOCK_RADIUS) {
                             float blockChance = 0.2f * Mathf.Lerp(0.5f, 1.5f, (defender.BaseData?.Blocking ?? 50f) / 75f)
                                                 * Mathf.Lerp(0.8f, 1.2f, (defender.BaseData?.Anticipation ?? 50f) / 100f)
                                                 * (1.0f - Mathf.Clamp01(distToLine / BLOCK_RADIUS));

                             // Use the deterministic random generator from the state
                             if (_state.RandomGenerator.NextDouble() < Mathf.Clamp01(blockChance)) {
                                 ActionResult result = new ActionResult { Outcome = ActionResultOutcome.Blocked, PrimaryPlayer = defender, SecondaryPlayer = _state.Ball.LastShooter, ImpactPosition = defender.Position };
                                 _eventHandler.HandleActionResult(result, _state);
                                 return; // Block occurred
                             }
                         }
                     }
                 } catch (Exception ex) { Debug.LogError($"Error checking block for defender {defender.GetPlayerId()}: {ex.Message}"); }
             }
         }


        /// <summary>
        /// Checks for potential saves by the goalkeeper. Calls event handler if successful.
        /// </summary>
        private void CheckForSaves() {
            if (_state?.Ball?.LastShooter == null || !_state.Ball.IsInFlight) return; // Combined null checks
            int defendingTeamSimId = 1 - _state.Ball.LastShooter.TeamSimId;
            SimPlayer gk = _state.GetGoalkeeper(defendingTeamSimId);
            if (gk == null || gk.IsSuspended()) return;

            try {
                float goalLineX = defendingTeamSimId == 0 ? 0f : PitchGeometry.Length;
                float nextPosX = _state.Ball.Position.x + _state.Ball.Velocity.x * TIME_STEP_SECONDS;
                bool headingTowardsGoalPlane = (defendingTeamSimId == 0 && _state.Ball.Velocity.x < -0.1f && nextPosX <= goalLineX + 1f) ||
                                               (defendingTeamSimId == 1 && _state.Ball.Velocity.x > 0.1f && nextPosX >= goalLineX - 1f);
                if (!headingTowardsGoalPlane) return;

                Vector2 predictedImpact = PredictImpactPoint(gk);
                float distanceToImpact = Vector2.Distance(gk.Position, predictedImpact);
                float ballSpeed = Mathf.Max(1f, _state.Ball.Velocity.magnitude);
                float timeToImpact = distanceToImpact / ballSpeed;
                float agilityFactor = Mathf.Lerp(0.8f, 1.2f, (gk.BaseData?.Agility ?? 50f) / 100f);
                float reachDistance = gk.EffectiveSpeed * timeToImpact * agilityFactor;

                if (distanceToImpact < reachDistance + 0.8f) {
                    float saveProb = 0.6f;
                    saveProb *= Mathf.Lerp(0.7f, 1.3f, (gk.BaseData?.Reflexes ?? 50f) / 80f);
                    saveProb *= Mathf.Lerp(0.8f, 1.2f, (gk.BaseData?.Handling ?? 50f) / 100f);
                    saveProb *= Mathf.Lerp(0.9f, 1.1f, (gk.BaseData?.PositioningGK ?? 50f) / 100f);
                    if(_state.Ball.LastShooter?.BaseData != null) {
                         saveProb *= Mathf.Lerp(1.1f, 0.9f, _state.Ball.LastShooter.BaseData.ShootingPower/100f);
                         saveProb *= Mathf.Lerp(1.1f, 0.8f, _state.Ball.LastShooter.BaseData.ShootingAccuracy/100f);
                    }

                    // Use the deterministic random generator from the state
                    if (_state.RandomGenerator.NextDouble() < Mathf.Clamp01(saveProb)) {
                        ActionResult result = new ActionResult { Outcome = ActionResultOutcome.Saved, PrimaryPlayer = gk, SecondaryPlayer = _state.Ball.LastShooter, ImpactPosition = predictedImpact };
                        _eventHandler.HandleActionResult(result, _state);
                        return; // Save occurred
                    }
                }
            } catch (Exception ex) { Debug.LogError($"Error checking save for GK {gk.GetPlayerId()}: {ex.Message}"); }
         }


        /// <summary>
        /// Checks for players picking up a loose ball. Calls event handler if successful.
        /// Iterates over a copy of PlayersOnCourt for safety.
        /// </summary>
        private void CheckForLooseBallPickup() {
            if (_state?.Ball == null || !_state.Ball.IsLoose) return; // Null checks
            SimPlayer potentialPicker = null; float minPickDistanceSq = LOOSE_BALL_PICKUP_RADIUS * LOOSE_BALL_PICKUP_RADIUS;
            var players = _state.PlayersOnCourt?.ToList(); // Use ToList for safety as picker state changes
            if (players == null) return;

            try {
                var chasers = players.Where(p => p != null && p.CurrentAction == PlayerAction.ChasingBall && !p.IsSuspended());
                foreach (var player in chasers) {
                     float distSq = (player.Position - _state.Ball.Position).sqrMagnitude;
                     if (distSq < minPickDistanceSq) { potentialPicker = player; minPickDistanceSq = distSq; }
                }

                if (potentialPicker == null) {
                    foreach (var player in players) {
                        if (player == null || player.IsSuspended() || player.CurrentAction == PlayerAction.Fallen || player.CurrentAction == PlayerAction.ChasingBall) continue;
                        float distSq = (player.Position - _state.Ball.Position).sqrMagnitude;
                        if (distSq < minPickDistanceSq) {
                            if ((player.BaseData?.Technique ?? 0) > 30 && (player.BaseData?.Anticipation ?? 0) > 30) {
                                potentialPicker = player; minPickDistanceSq = distSq;
                            }
                        }
                    }
                }

                if (potentialPicker != null) {
                    ActionResult pickupResult = new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = potentialPicker, ImpactPosition = _state.Ball.Position, Reason = "Picked up loose ball" };
                    _eventHandler.HandlePossessionChange(_state, potentialPicker.TeamSimId);
                    _state.Ball.SetPossession(potentialPicker);
                    _eventHandler.ResetPlayerActionState(potentialPicker, pickupResult.Outcome);
                }
            } catch (Exception ex) { Debug.LogError($"Error checking loose ball pickup: {ex.Message}"); }
        }


        // --- Passive Event Check Methods ---

        /// <summary>
        /// Checks for passive events like ball crossing goal lines or sidelines.
        /// </summary>
        private void CheckPassiveEvents() {
             if (_state == null || _state.Ball == null || _eventHandler == null) return;
             try {
                 if (!CheckGoalLineCrossing()) { // Pass current state implicitly
                      CheckSideLineCrossing(); // Pass current state implicitly
                 }
             } catch (Exception ex) { Debug.LogError($"Error checking passive events: {ex.Message}"); }
        }

        /// <summary>
        /// Checks if the ball crossed the goal line (end line). Refactored for clarity.
        /// </summary>
        /// <returns>True if a goal line event occurred and was handled, false otherwise.</returns>
        private bool CheckGoalLineCrossing()
        {
            Vector2 currentBallPos = _state.Ball.Position;
            Vector2 prevBallPos = currentBallPos - _state.Ball.Velocity * TIME_STEP_SECONDS;

            GoalLineCrossInfo crossInfo = DidCrossGoalLine(prevBallPos, currentBallPos);
            if (!crossInfo.DidCross) return false; // No crossing

            if (IsBetweenGoalPosts(currentBallPos, crossInfo.IsHomeGoalLine))
            {
                // Crossed between posts, check if it was a valid goal
                if (IsValidGoalAttempt(_state.Ball, crossInfo.IsHomeGoalLine))
                {
                    ActionResult goalResult = new ActionResult { Outcome = ActionResultOutcome.Goal, PrimaryPlayer = _state.Ball.LastShooter, ImpactPosition = currentBallPos };
                    _eventHandler.HandleActionResult(goalResult, _state);
                    return true; // Goal handled
                }
                // Else: Crossed between posts but not valid goal (e.g., pass, deflection) -> OOB
            }

            // Crossed end line but not a goal -> OutOfBounds
            ActionResult oobResult = new ActionResult { Outcome = ActionResultOutcome.OutOfBounds, ImpactPosition = currentBallPos, PrimaryPlayer = _state.Ball.LastTouchedByPlayer };
            _eventHandler.HandleOutOfBounds(oobResult, _state);
            return true; // OOB handled
        }

        /// <summary>Helper struct for CheckGoalLineCrossing.</summary>
        private struct GoalLineCrossInfo { public bool DidCross; public bool IsHomeGoalLine; }

        /// <summary>Checks if the ball segment crossed either goal line.</summary>
        private GoalLineCrossInfo DidCrossGoalLine(Vector2 prevPos, Vector2 currentPos)
        {
            bool crossedHome = prevPos.x > 0f && currentPos.x <= 0f;
            if (crossedHome) return new GoalLineCrossInfo { DidCross = true, IsHomeGoalLine = true };
            bool crossedAway = prevPos.x < PitchGeometry.Length && currentPos.x >= PitchGeometry.Length;
            if (crossedAway) return new GoalLineCrossInfo { DidCross = true, IsHomeGoalLine = false };
            return new GoalLineCrossInfo { DidCross = false };
        }

        /// <summary>Checks if the ball's Y position is within the goal posts.</summary>
        private bool IsBetweenGoalPosts(Vector2 position, bool isHomeGoalLine)
        {
            Vector2 goalCenter = isHomeGoalLine ? PitchGeometry.HomeGoalCenter : PitchGeometry.AwayGoalCenter;
            return Mathf.Abs(position.y - goalCenter.y) <= PitchGeometry.GoalWidth / 2f;
        }

        /// <summary>Checks if the ball state represents a valid goal attempt.</summary>
        private bool IsValidGoalAttempt(SimBall ball, bool crossingHomeLine)
        {
            // Must be in flight from a shot by the attacking team
            return ball.IsInFlight && ball.LastShooter != null &&
                   ((crossingHomeLine && ball.LastShooter.TeamSimId == 1) || // Away scored on Home goal
                    (!crossingHomeLine && ball.LastShooter.TeamSimId == 0));   // Home scored on Away goal
        }


        /// <summary>
        /// Checks if the ball crossed a sideline and handles OutOfBounds via EventHandler.
        /// </summary>
        /// <returns>True if a sideline event occurred and was handled, false otherwise.</returns>
        private bool CheckSideLineCrossing() // Removed parameter, uses _state.Ball.Position
        {
            Vector2 currentBallPos = _state.Ball.Position;
            Vector2 prevBallPos = currentBallPos - _state.Ball.Velocity * TIME_STEP_SECONDS;
            bool crossedBottomLine = prevBallPos.y > 0f && currentBallPos.y <= 0f;
            bool crossedTopLine = prevBallPos.y < PitchGeometry.Width && currentBallPos.y >= PitchGeometry.Width;

            if (crossedBottomLine || crossedTopLine) {
                 // Only trigger OOB if ball is not held (loose or in flight)
                 if (_state.Ball.IsLoose || _state.Ball.IsInFlight) {
                     ActionResult oobResult = new ActionResult { Outcome = ActionResultOutcome.OutOfBounds, ImpactPosition = currentBallPos, PrimaryPlayer = _state.Ball.LastTouchedByPlayer };
                     _eventHandler.HandleOutOfBounds(oobResult, _state); // Let handler manage state
                     return true; // Event handled
                 }
             }
            return false; // No crossing detected or ball was held
        }


        // --- Helper Methods ---

        /// <summary>
        /// Predicts the impact point of the ball on the goal line plane defended by the GK. Includes null checks.
        /// </summary>
        private Vector2 PredictImpactPoint(SimPlayer gk) {
            if (_state == null || gk == null || _state.Ball == null) return Vector2.zero;
            try {
                float goalPlaneX = (gk.TeamSimId == 0) ? 0.1f : PitchGeometry.Length - 0.1f;
                Vector2 ballPos = _state.Ball.Position; Vector2 ballVel = _state.Ball.Velocity;
                if (Mathf.Abs(ballVel.x) < 0.1f) return new Vector2(goalPlaneX, ballPos.y);

                float timeToPlane = (goalPlaneX - ballPos.x) / ballVel.x;
                if (timeToPlane < -0.05f || timeToPlane > 2.0f) return new Vector2(goalPlaneX, ballPos.y);

                float impactY = ballPos.y + ballVel.y * timeToPlane;
                float goalY = (gk.TeamSimId == 0) ? PitchGeometry.HomeGoalCenter.y : PitchGeometry.AwayGoalCenter.y;
                impactY = Mathf.Clamp(impactY, goalY - PitchGeometry.GoalWidth, goalY + PitchGeometry.GoalWidth);
                return new Vector2(goalPlaneX, impactY);
            } catch (Exception ex) {
                Debug.LogError($"Error predicting impact point: {ex.Message}");
                return new Vector2((gk.TeamSimId == 0) ? 0f : PitchGeometry.Length, PitchGeometry.Center.y);
            }
         }

        /// <summary>
        /// Finds the best available player for a given position in the lineup.
        /// </summary>
        private SimPlayer FindPlayerByPosition(List<SimPlayer> lineup, PlayerPosition position) {
             if (lineup == null) return null;
             SimPlayer player = lineup.FirstOrDefault(p=> p != null && p.BaseData?.PrimaryPosition == position && p.IsOnCourt && !p.IsSuspended());
             return player ?? lineup.FirstOrDefault(p => p != null && p.IsOnCourt && !p.IsSuspended() && !p.IsGoalkeeper());
        }


        // --- Logging ---

        /// <summary>
        /// Logs a simulation event message and adds it to the match event list.
        /// Ensures state object is valid before logging.
        /// </summary>
        public void LogEvent(string description, int? teamId = null, int? playerId = null) {
            // Check if state and MatchEvents list are valid before logging
            if (_state?.MatchEvents == null) return;
            try {
                float minutes = Mathf.Floor(_state.MatchTimeSeconds / SECONDS_PER_MINUTE);
                float seconds = Mathf.Floor(_state.MatchTimeSeconds % SECONDS_PER_MINUTE);
                string timeStr = $"{minutes:00}:{seconds:00}";
                string logMsg = $"[Sim {timeStr}] {description}";
                // Debug.Log(logMsg); // Optional: Keep for detailed console output during dev
                _state.MatchEvents.Add(new MatchEvent(_state.MatchTimeSeconds, description, teamId, playerId));
            } catch (Exception ex) { Debug.LogWarning($"[MatchSimulator] Failed to add MatchEvent: {ex.Message}"); }
        }

        // --- Finalization ---

        /// <summary>
        /// Finalizes the simulation state into a MatchResult object.
        /// Handles potential null state and attempts to get the correct match date.
        /// Includes final validation between score and goal stats.
        /// </summary>
        /// <returns>The completed MatchResult, or a basic error result if state was invalid.</returns>
        private MatchResult FinalizeResult() {
            if (_state == null) {
                Debug.LogError("[MatchSimulator] Cannot finalize result, MatchState is null!");
                // Return a clearly marked error result
                return new MatchResult(-99, -98, "ERROR", "NULL_STATE") { MatchDate = DateTime.Now.Date };
            }

            DateTime matchDate = DateTime.Now.Date; // Fallback date
            try {
                 // Safely attempt to get date from GameManager if available
                 matchDate = Core.GameManager.Instance?.TimeManager?.CurrentDate ?? DateTime.Now.Date;
            } catch (Exception ex) { Debug.LogWarning($"[MatchSimulator] Could not get game time for final result: {ex.Message}"); }

            // Create the result object using safe access to state data
            MatchResult result = new MatchResult(
                _state.HomeTeamData?.TeamID ?? -1,
                _state.AwayTeamData?.TeamID ?? -2,
                _state.HomeTeamData?.Name ?? "Home_Err",
                _state.AwayTeamData?.Name ?? "Away_Err"
            ) {
                HomeScore = _state.HomeScore,
                AwayScore = _state.AwayScore,
                MatchDate = matchDate,
                // Assign stats objects, creating new ones if somehow null in state
                HomeStats = _state.CurrentHomeStats ?? new TeamMatchStats(),
                AwayStats = _state.CurrentAwayStats ?? new TeamMatchStats()
            };

            // Final Validation: Check consistency between final score and accumulated goal stats
            if (result.HomeScore != result.HomeStats.GoalsScored) {
                Debug.LogWarning($"[MatchSimulator Validation] Final score mismatch! Home Score: {result.HomeScore} vs Stats Goals: {result.HomeStats.GoalsScored}");
            }
             if (result.AwayScore != result.AwayStats.GoalsScored) {
                Debug.LogWarning($"[MatchSimulator Validation] Final score mismatch! Away Score: {result.AwayScore} vs Stats Goals: {result.AwayStats.GoalsScored}");
            }
            return result;
        }

        // --- Placement & Positioning ---

        /// <summary>
        /// Places players in their tactical formation positions.
        /// Handles kickoff positioning adjustments.
        /// </summary>
        private void PlacePlayersInFormation(List<SimPlayer> players, Tactic tactic, bool isHomeTeam, bool isKickOff) {
             if (players == null || tactic == null || _state == null || _tacticPositioner == null) {
                  Debug.LogError("[MatchSimulator] Cannot place players in formation - null input detected.");
                  return;
             }
             foreach (var player in players) {
                 // Skip invalid players early
                 if (player == null || !player.IsOnCourt || player.IsSuspended()) continue;

                 // Reset action state and velocity
                 player.CurrentAction = PlayerAction.Idle; player.Velocity = Vector2.zero;
                 Vector2 basePos = player.Position; // Default to current pos if error occurs

                 try {
                      // Get the ideal tactical position from the positioner
                      basePos = _tacticPositioner.GetPlayerTargetPosition(player, _state);
                 } catch (Exception ex) {
                      Debug.LogError($"[MatchSimulator] Error getting tactical pos for player {player.GetPlayerId()}: {ex.Message}. Using current position as fallback.");
                 }

                 // Adjust position based on kickoff rules
                 if (isKickOff) {
                     float halfLineX = PitchGeometry.Center.x;
                     // Ensure player is on their own half
                     if (isHomeTeam && basePos.x >= halfLineX) { basePos.x = halfLineX - (1f + ((player.GetPlayerId() % 5) * 0.5f)); } // Deterministic spread back
                     else if (!isHomeTeam && basePos.x <= halfLineX) { basePos.x = halfLineX + (1f + ((player.GetPlayerId() % 5) * 0.5f)); } // Deterministic spread forward

                     // Place GK specifically
                     if (player.IsGoalkeeper()) {
                         basePos.x = isHomeTeam ? PitchGeometry.HomeGoalCenter.x + DEF_GK_DEPTH : PitchGeometry.AwayGoalCenter.x - DEF_GK_DEPTH;
                         basePos.y = PitchGeometry.Center.y;
                     }
                 }
                 // Set final position and target
                 player.Position = basePos; player.TargetPosition = player.Position;
             }
        }

    } // End MatchSimulator Class
}