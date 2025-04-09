// --- START OF FILE HandballManager/Simulation/MatchSimulator.cs ---
using UnityEngine;
using HandballManager.Simulation.Core.MatchData; // Updated to reflect new location of MatchData
using HandballManager.Simulation.AI; // Updated from Engines to AI for PlayerAIController
using HandballManager.Simulation.Physics; // For MovementSimulator and other physics components
using HandballManager.Core; // For Enums (GamePhase)
using System; // For Exception, ArgumentNullException
using System.Linq; // For Linq (used in ResolvePendingActions)

// Change from:
namespace HandballManager.Simulation
// To:
namespace HandballManager.Simulation.Core
{
    /// <summary>
    /// Core class responsible for orchestrating the detailed simulation of a handball match.
    /// Manages the main simulation loop and delegates tasks to specialized services via injected dependencies.
    /// </summary>
    public class MatchSimulator
    {
        // --- Simulation Constants ---
        // Time step remains fundamental to the loop orchestration
        private const float TIME_STEP_SECONDS = 0.1f;
        // Match duration might be determined by external config or TimeManager later
        private const float DEFAULT_MATCH_DURATION_SECONDS = 60f * 60f;

        // --- Dependencies (Injected) ---
        private readonly IPhaseManager _phaseManager;
        private readonly ISimulationTimer _simulationTimer;
        private readonly IBallPhysicsCalculator _ballPhysicsCalculator;
        private readonly IMovementSimulator _movementSimulator; // Changed to interface
        private readonly IPlayerAIController _aiController; // Changed to interface
        private readonly IActionResolver _actionResolver; // Changed to interface
        private readonly IEventDetector _eventDetector;
        private readonly IMatchEventHandler _eventHandler;
        private readonly IPlayerSetupHandler _playerSetupHandler; // Needed for initialization phase
        private readonly IMatchFinalizer _matchFinalizer;
        private readonly IGeometryProvider _geometryProvider; // May not be needed directly if services handle all geometry

        // --- Simulation State (Managed Externally, Passed In) ---
        private readonly MatchState _state;

        // --- Simulation Control ---
        private bool _isInitialized = false;
        // Seed is now primarily managed by MatchState initialization

        /// <summary>
        /// Initializes a new MatchSimulator instance with required dependencies and the initial MatchState.
        /// The MatchState should already be populated with teams, tactics, and players.
        /// </summary>
        /// <param name="initialState">The fully initialized MatchState object.</param>
        /// <param name="phaseManager">Service for managing game phases.</param>
        /// <param name="timer">Service for updating simulation timers.</param>
        /// <param name="ballPhysicsCalculator">Service for ball physics calculations.</param>
        /// <param name="movementSimulator">Engine for player/ball movement updates.</param>
        /// <param name="aiController">Engine for AI player decisions.</param>
        /// <param name="actionResolver">Engine for resolving discrete actions.</param>
        /// <param name="eventDetector">Service for detecting simulation events.</param>
        /// <param name="eventHandler">Service for handling simulation events and state changes.</param>
        /// <param name="playerSetupHandler">Service used for initial player setup validation/logging.</param>
        /// <param name="matchFinalizer">Service for finalizing the match result.</param>
        /// <param name="geometryProvider">Service providing pitch geometry info.</param>
        /// <exception cref="ArgumentNullException">Thrown if any required dependency or the initial state is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the initial state is invalid (e.g., missing players).</exception>
        public MatchSimulator(
            MatchState initialState, // State is now passed in
            IPhaseManager phaseManager,
            ISimulationTimer timer,
            IBallPhysicsCalculator ballPhysicsCalculator,
            IMovementSimulator movementSimulator, // Changed to interface
            IPlayerAIController aiController, // Changed to interface
            IActionResolver actionResolver, // Changed to interface
            IEventDetector eventDetector,
            IMatchEventHandler eventHandler,
            IPlayerSetupHandler playerSetupHandler,
            IMatchFinalizer matchFinalizer,
            IGeometryProvider geometryProvider)
        {
            // --- Dependency and State Validation ---
            _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
            _phaseManager = phaseManager ?? throw new ArgumentNullException(nameof(phaseManager));
            _simulationTimer = timer ?? throw new ArgumentNullException(nameof(timer));
            _ballPhysicsCalculator = ballPhysicsCalculator ?? throw new ArgumentNullException(nameof(ballPhysicsCalculator));
            _movementSimulator = movementSimulator ?? throw new ArgumentNullException(nameof(movementSimulator));
            _aiController = aiController ?? throw new ArgumentNullException(nameof(aiController));
            _actionResolver = actionResolver ?? throw new ArgumentNullException(nameof(actionResolver));
            _eventDetector = eventDetector ?? throw new ArgumentNullException(nameof(eventDetector));
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _playerSetupHandler = playerSetupHandler ?? throw new ArgumentNullException(nameof(playerSetupHandler)); // Keep reference if needed
            _matchFinalizer = matchFinalizer ?? throw new ArgumentNullException(nameof(matchFinalizer));
            _geometryProvider = geometryProvider ?? throw new ArgumentNullException(nameof(geometryProvider));

            // Basic validation of the passed-in state
            if (_state.HomeTeamData == null || _state.AwayTeamData == null ||
                _state.HomeTactic == null || _state.AwayTactic == null ||
                _state.Ball == null || _state.AllPlayers == null || _state.RandomGenerator == null ||
                _state.HomePlayersOnCourt == null || _state.AwayPlayersOnCourt == null)
            {
                throw new InvalidOperationException("Initial MatchState is missing critical data.");
            }
             // Ensure lineups seem valid (correct count after setup)
             // Setup (PopulatePlayers/SelectLineups) is assumed to have happened BEFORE this constructor
             if (_state.HomePlayersOnCourt.Count != 7 || _state.AwayPlayersOnCourt.Count != 7)
             {
                 _eventHandler.LogEvent(_state, $"ERROR: Invalid lineup count detected during MatchSimulator init. H:{_state.HomePlayersOnCourt.Count} A:{_state.AwayPlayersOnCourt.Count}");
                 throw new InvalidOperationException("Invalid player lineup count in initial MatchState.");
             }

             // Initialize Phase Manager (runs initial setup like PreKickOff)
             try
             {
                 _phaseManager.TransitionToPhase(_state, GamePhase.PreKickOff, forceSetup: true);
                 _phaseManager.HandlePhaseTransitions(_state); // Run initial PreKickOff setup immediately
                 _isInitialized = true;
                 _eventHandler.LogEvent(_state, $"MatchSimulator Initialized. Seed: {_state.RandomGenerator.GetHashCode()}"); // Log seed if needed
             }
             catch (Exception ex)
             {
                  _eventHandler.HandleStepError(_state, "Initialization Phase Setup", ex);
                  _isInitialized = false; // Mark as failed
                  // Let the exception propagate up to MatchEngine
                  throw;
             }
        }

        /// <summary>
        /// Runs the main simulation loop from the current state until the match finishes.
        /// Assumes the MatchState and dependencies were properly initialized in the constructor.
        /// </summary>
        /// <param name="matchDate">The date the match occurred (passed to finalizer).</param>
        /// <returns>The MatchResult containing score and statistics.</returns>
        public MatchResult SimulateMatch(DateTime matchDate)
        {
            if (!_isInitialized || _state == null) // Double check state
            {
                 // Attempt to log if possible, but state might be bad
                 _eventHandler?.LogEvent(_state, "Match Simulation cannot start: Not Initialized.");
                 // Return an error result using the finalizer
                 return _matchFinalizer.FinalizeResult(null, matchDate); // Pass null state to indicate error
            }

            _eventHandler.LogEvent(_state, "Match Simulation Started");
            int safetyCounter = 0;
            float matchDurationSeconds = DEFAULT_MATCH_DURATION_SECONDS; // Use constant or get from config
            int maxSteps = (int)((matchDurationSeconds / TIME_STEP_SECONDS) * 1.5f); // Slightly smaller safety margin? 2.0 is safer.
            maxSteps = Math.Max(1000, maxSteps); // Ensure a minimum number of steps

            // --- Main Simulation Loop ---
            while (_state.CurrentPhase != GamePhase.Finished && safetyCounter < maxSteps)
            {
                // Check for external cancellation if token was implemented
                // cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // 1. Advance Time & Check for Half/Full Time
                    float timeBeforeStep = _state.MatchTimeSeconds;
                    _state.MatchTimeSeconds += TIME_STEP_SECONDS; // Direct time update remains here
                    float timeAfterStep = _state.MatchTimeSeconds;

                    if (_phaseManager.CheckAndHandleHalfTime(_state, timeBeforeStep, timeAfterStep)) continue;
                    if (_phaseManager.CheckAndHandleFullTime(_state, timeAfterStep)) break;

                    // 2. Perform Core Simulation Step Updates
                    UpdateSimulationStep(TIME_STEP_SECONDS);

                    // 3. Check if simulation ended during the step update
                    if (_state.CurrentPhase == GamePhase.Finished) break;
                }
                catch (Exception ex) // Catch errors within the loop step
                {
                     // Use EventHandler for logging and setting Finished state
                     _eventHandler.HandleStepError(_state, "Main Simulation Loop", ex);
                     // HandleStepError should transition state to Finished, breaking the loop
                     break;
                }
                safetyCounter++;
            } // End main simulation loop

            // --- Post-Loop Checks ---
            if (safetyCounter >= maxSteps && _state.CurrentPhase != GamePhase.Finished)
            {
                 Debug.LogError("[MatchSimulator] Simulation exceeded max steps! Force finishing.");
                 _eventHandler.LogEvent(_state, "WARNING: Simulation exceeded max steps. Forcing finish.");
                 if(_state != null) _phaseManager.TransitionToPhase(_state, GamePhase.Finished);
            }

             _eventHandler.LogEvent(_state, $"Match Simulation Finished: {_state?.HomeTeamData?.Name ?? "Home"} {_state?.HomeScore ?? 0} - {_state?.AwayScore ?? 0} {_state?.AwayTeamData?.Name ?? "Away"}");

             // Finalize result using the dedicated service
             return _matchFinalizer.FinalizeResult(_state, matchDate);
        }


        /// <summary>
        /// Performs a single time step update of the simulation logic, orchestrating component updates.
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last step (typically TIME_STEP_SECONDS).</param>
        private void UpdateSimulationStep(float deltaTime)
        {
            // Initial phase checks already done in the main loop

            // Handle timeout state separately (only timer updates)
            if (_state.CurrentPhase == GamePhase.Timeout)
            {
                 try { _simulationTimer.UpdateTimers(_state, deltaTime, _eventHandler); }
                 catch (Exception ex) { _eventHandler.HandleStepError(_state, "Timers Update (Timeout)", ex); }
                 return;
            }

            // --- Phase Setup & Automatic Transitions (Handles entering new phases) ---
            try { _phaseManager.HandlePhaseTransitions(_state); }
            catch (Exception ex) { _eventHandler.HandleStepError(_state, "Phase Transitions", ex); return; }
            // Re-check phase after potential transitions
             if (_state.CurrentPhase == GamePhase.Finished || _state.CurrentPhase == GamePhase.HalfTime || _state.CurrentPhase == GamePhase.Timeout) return;

            // --- Active Gameplay Updates --- Order Matters!
            // 1. Update Timers (Match, Suspension, Player Action)
            try { _simulationTimer.UpdateTimers(_state, deltaTime, _eventHandler); }
            catch (Exception ex) { _eventHandler.HandleStepError(_state, "Timers Update", ex); return; }
             if (_state.CurrentPhase == GamePhase.Finished) return; // Check if timer update finished game

            // 2. Update AI Decisions (Sets player intentions: CurrentAction, TargetPosition, TargetPlayer)
            try { _aiController.UpdatePlayerDecisions(_state); }
            catch (Exception ex) { _eventHandler.HandleStepError(_state, "AI Decisions", ex); return; }
             if (_state.CurrentPhase == GamePhase.Finished) return;

            // 3. Resolve Pending Player Actions (Pass/Shot Release, Tackle Commit) - Uses ActionResolver & EventHandler
            try { ResolvePendingActions(); }
            catch (Exception ex) { _eventHandler.HandleStepError(_state, "Action Resolution", ex); return; }
             if (_state.CurrentPhase == GamePhase.Finished) return;

            // 4. Update Ball Physics (Movement, Collisions with ground etc.)
            try { _ballPhysicsCalculator.UpdateBallMovement(_state.Ball, deltaTime); }
            catch (Exception ex) { _eventHandler.HandleStepError(_state, "Ball Physics Update", ex); return; }
             if (_state.CurrentPhase == GamePhase.Finished) return; // Ball physics shouldn't end game, but check state

            // 5. Update Player Movement & Collisions (Uses MovementSimulator)
            try { _movementSimulator.UpdateMovement(_state, deltaTime); }
            catch (Exception ex) { _eventHandler.HandleStepError(_state, "Movement Update", ex); return; }
             if (_state.CurrentPhase == GamePhase.Finished) return;

            // 6. Detect Reactive Events (Interceptions, Blocks, Saves, Pickups) - Uses EventDetector -> EventHandler
            try { _eventDetector.CheckReactiveEvents(_state, _actionResolver, _eventHandler); }
            catch (Exception ex) { _eventHandler.HandleStepError(_state, "Reactive Events Check", ex); return; }
             if (_state.CurrentPhase == GamePhase.Finished) return;

            // 7. Detect Passive Events (Goal, OutOfBounds) - Uses EventDetector -> EventHandler
            try { _eventDetector.CheckPassiveEvents(_state, _eventHandler); }
            catch (Exception ex) { _eventHandler.HandleStepError(_state, "Passive Events Check", ex); return; }
            // Final state check happens in the main loop
        }

        /// <summary>
        /// Internal orchestration: Resolves actions for players whose action timer has completed.
        /// Uses the ActionResolver and updates state via the EventHandler.
        /// </summary>
        private void ResolvePendingActions() {
            // Use ToList() for safety if HandleActionResult might modify the court list (e.g., suspension)
            foreach (var player in _state.PlayersOnCourt.ToList())
            {
                // Added null check for player safety
                if (player == null || player.IsSuspended()) continue;

                // Use epsilon comparison for float timer
                if (player.ActionTimer <= SimConstants.FLOAT_EPSILON)
                {
                    PlayerAction actionToResolve = player.CurrentAction;

                    // Resolve actions that have a preparation timer
                    if (actionToResolve == PlayerAction.PreparingPass ||
                        actionToResolve == PlayerAction.PreparingShot ||
                        actionToResolve == PlayerAction.AttemptingTackle)
                    {
                         // ActionResolver calculates outcome, EventHandler applies it
                         ActionResult result = _actionResolver.ResolvePreparedAction(player, _state); // Assumes ActionResolver handles internal errors safely
                         _eventHandler.HandleActionResult(result, _state);

                         // If the game ended due to the action, stop processing further actions this step
                         if (_state.CurrentPhase == GamePhase.Finished) return;
                    }
                    // No 'else': Other actions either don't have timers or are handled by AI changing state
                }
            }
        }

    } // End MatchSimulator Class
}
// --- END OF FILE HandballManager/Simulation/MatchSimulator.cs ---