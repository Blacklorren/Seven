#region Core Dependencies
using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Core;
using HandballManager.Core.Logging;
using HandballManager.Core.Time;
#endregion

#region Simulation Dependencies
using HandballManager.Simulation;
using HandballManager.Simulation.Factories;
using HandballManager.Simulation.Interfaces;
using HandballManager.Simulation.Services;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.AI.DecisionMakers;
using HandballManager.Simulation.AI.Evaluators;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Simulation.Physics;
#endregion

#region System Dependencies
using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
#endregion

namespace HandballManager.Simulation.Engines
{
    public class MatchEngine
    {
        private const int DefaultMinPlayersRequired = 7;
        private readonly ILogger _logger;
        private readonly IGameTimeProvider _timeProvider;
        private readonly IMatchSimulatorFactory _simulatorFactory; // Keep the factory interface

        // Store references to the instantiated services using interfaces
        private IGeometryProvider _geometryProvider;
        private IBallPhysicsCalculator _ballPhysicsCalculator;
        private IPhaseManager _phaseManager;
        private IPlayerSetupHandler _playerSetupHandler;
        private IEventDetector _eventDetector;
        private IMatchEventHandler _matchEventHandler;
        private IMatchFinalizer _matchFinalizer;
        private ISimulationTimer _simulationTimer;
        
        // Updated to use interfaces instead of concrete implementations
        private IMovementSimulator _movementSimulator;
        private IPlayerAIController _aiController;
        private IActionResolver _actionResolver;
        private ITacticPositioner _tacticPositioner;
        
        // AI decision makers and evaluators
        private IPersonalityEvaluator _personalityEvaluator;
        private ITacticalEvaluator _tacticalEvaluator;
        private IGameStateEvaluator _gameStateEvaluator;
        private IPassingDecisionMaker _passingDecisionMaker;
        private IShootingDecisionMaker _shootingDecisionMaker;
        private IDribblingDecisionMaker _dribblingDecisionMaker;
        private IDefensiveDecisionMaker _defensiveDecisionMaker;
        private IGoalkeeperPositioner _goalkeeperPositioner;

        public MatchEngine(ILogger logger, IGameTimeProvider timeProvider, IMatchSimulatorFactory simulatorFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _simulatorFactory = simulatorFactory ?? throw new ArgumentNullException(nameof(simulatorFactory));

            // Pre-instantiate services
            SetupSimulationServices();
        }

        /// <summary>
        /// Instantiates the simulation services needed for a match.
        /// Called by the constructor or potentially at the start of SimulateMatch.
        /// </summary>
        private void SetupSimulationServices()
        {
             _logger.LogInformation("Setting up simulation services for MatchEngine...");
             try
             {
                  // Instantiate services directly (similar to factory logic, but done in MatchEngine)
                  _geometryProvider = new PitchGeometryProvider();
                  
                  // AI/Evaluator instantiation
                  _personalityEvaluator = new PlaceholderPersonalityEvaluator();
                  _tacticalEvaluator = new PlaceholderTacticalEvaluator();
                  _gameStateEvaluator = new PlaceholderGameStateEvaluator();
                  _passingDecisionMaker = new PlaceholderPassingDecisionMaker();
                  _shootingDecisionMaker = new PlaceholderShootingDecisionMaker();
                  _dribblingDecisionMaker = new PlaceholderDribblingDecisionMaker();
                  _defensiveDecisionMaker = new PlaceholderDefensiveDecisionMaker();
                  _goalkeeperPositioner = new PlaceholderGoalkeeperPositioner();
                  
                  // Instantiate core services/engines, injecting dependencies
                  _ballPhysicsCalculator = new DefaultBallPhysicsCalculator(_geometryProvider);
                  _matchEventHandler = new DefaultMatchEventHandler(_geometryProvider);
                  _playerSetupHandler = new DefaultPlayerSetupHandler(_matchEventHandler);
                  _phaseManager = new DefaultPhaseManager(_playerSetupHandler, _matchEventHandler, _geometryProvider);
                  _simulationTimer = new DefaultSimulationTimer();
                  _eventDetector = new DefaultEventDetector(_geometryProvider);
                  _matchFinalizer = new DefaultMatchFinalizer();
                  
                  // Core engines with interfaces
                  // Using interfaces instead of concrete implementations for better testability
                  _movementSimulator = new MovementSimulator() as IMovementSimulator;
                  _actionResolver = new ActionResolver() as IActionResolver;
                  _tacticPositioner = new TacticPositioner() as ITacticPositioner;
                  
                  // AI controller with all dependencies injected
                  _aiController = new PlayerAIController(
                        _tacticPositioner, 
                        _goalkeeperPositioner, 
                        _passingDecisionMaker, 
                        _shootingDecisionMaker,
                        _dribblingDecisionMaker, 
                        _defensiveDecisionMaker, 
                        _tacticalEvaluator,
                        _personalityEvaluator, 
                        _gameStateEvaluator, 
                        _ballPhysicsCalculator
                  );
                  
                  _logger.LogInformation("Simulation services set up successfully.");
             }
             catch (Exception ex)
             {
                  _logger.LogError("CRITICAL ERROR setting up simulation services in MatchEngine.", ex);
                  throw new InvalidOperationException("Failed to setup simulation services.", ex);
             }
        }


        public MatchResult SimulateMatch(TeamData homeTeam, TeamData awayTeam, Tactic homeTactic, Tactic awayTactic,
                                         int seed = -1, IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            string homeTeamName = homeTeam?.Name ?? "NULL_HOME_TEAM";
            string awayTeamName = awayTeam?.Name ?? "NULL_AWAY_TEAM";
            _logger.LogInformation($"Starting match simulation setup: {homeTeamName} vs {awayTeamName} (Seed: {seed})");

            // --- Input Validation --- (Keep this here)
            if (homeTeam == null || awayTeam == null) return CreateErrorResult("Home or Away TeamData is null.");
            string rosterValidationError = ValidateTeamRoster(homeTeam, "Home");
            if (rosterValidationError != null) return CreateErrorResult(rosterValidationError);
            rosterValidationError = ValidateTeamRoster(awayTeam, "Away");
            if (rosterValidationError != null) return CreateErrorResult(rosterValidationError);

            // --- Tactic Handling --- (Keep this here)
             if (homeTactic == null) { _logger.LogWarning($"Home tactic null for {homeTeamName}. Using default."); homeTactic = Tactic.Default; }
             if (awayTactic == null) { _logger.LogWarning($"Away tactic null for {awayTeamName}. Using default."); awayTactic = Tactic.Default; }


            MatchResult result = null;
            MatchSimulator matchSimulator = null;
            MatchState matchState = null;

            try
            {
                // Check for cancellation before potentially lengthy setup
                cancellationToken.ThrowIfCancellationRequested();

                // --- Create MatchState ---
                matchState = new MatchState(homeTeam, awayTeam, homeTactic, awayTactic, seed);

                // --- Create MatchSimulator instance, passing state and pre-instantiated services ---
                 // Ensure all services were created successfully
                 if (_phaseManager == null || _simulationTimer == null || _ballPhysicsCalculator == null || 
                     _movementSimulator == null || _aiController == null || _actionResolver == null ||
                     _eventDetector == null || _matchEventHandler == null || _playerSetupHandler == null ||
                     _matchFinalizer == null || _geometryProvider == null)
                 {
                     throw new InvalidOperationException("Simulation services were not properly initialized before creating MatchSimulator.");
                 }

                 // Make sure ActionResolver is properly instantiated
                 if (_actionResolver == null)
                 {
                     _logger.LogWarning("ActionResolver was null, creating a new instance");
                     _actionResolver = new ActionResolver();
                 }

                 // Constructor now takes dependencies + initial state parameters
                 matchSimulator = new MatchSimulator(
                     matchState,
                     _phaseManager, 
                     _simulationTimer, 
                     _ballPhysicsCalculator,
                     _movementSimulator, 
                     _aiController, 
                     _actionResolver,  // Passing the ActionResolver interface
                     _eventDetector, 
                     _matchEventHandler, 
                     _playerSetupHandler,
                     _matchFinalizer, 
                     _geometryProvider
                 );

                _logger.LogInformation($"Starting simulation core: {homeTeamName} vs {awayTeamName}");

                // Run the simulation with current date
                DateTime matchDate = _timeProvider.CurrentDate;
                result = matchSimulator.SimulateMatch(matchDate);

                cancellationToken.ThrowIfCancellationRequested();

                if (result != null) { 
                    _logger.LogInformation($"Simulation finished: {result.HomeTeamName} {result.HomeScore} - {result.AwayScore} {result.AwayTeamName}"); 
                }
                else { 
                    _logger.LogWarning($"Simulation completed but returned a null result for {homeTeamName} vs {awayTeamName}."); 
                }
            }
            catch (OperationCanceledException) { 
                _logger.LogInformation($"Simulation cancelled for {homeTeamName} vs {awayTeamName}");
                result = CreateErrorResult("Simulation was cancelled");
            }
            catch (InvalidOperationException opEx) { 
                _logger.LogError($"Simulation setup error ({homeTeamName} vs {awayTeamName}): {opEx.Message}", opEx);
                result = CreateErrorResult($"Simulation Setup Exception: {opEx.Message}");
            }
            catch (Exception ex) { 
                _logger.LogError($"Simulation error ({homeTeamName} vs {awayTeamName}): {ex.Message}", ex);
                result = CreateErrorResult($"Simulation Exception: {ex.Message}");
            }

            return result ?? CreateErrorResult("Simulation returned null result unexpectedly.");
        }

        // Keep ValidateTeamRoster and CreateErrorResult methods as they are part of MatchEngine's responsibility.
        private string ValidateTeamRoster(TeamData team, string teamIdentifier) { 
            if (team == null) return $"{teamIdentifier} team is null";
            if (team.Players == null) return $"{teamIdentifier} team has no players";
            if (team.Players.Count < DefaultMinPlayersRequired) 
                return $"{teamIdentifier} team has insufficient players ({team.Players.Count}/{DefaultMinPlayersRequired})";
            return null; 
        }
        
        private MatchResult CreateErrorResult(string reason) { 
            _logger.LogError($"Match simulation error: {reason}");
            return new MatchResult(-1, -2, "Error", "Error") { 
                ErrorMessage = reason 
            }; 
        }

        // --- Placeholder Concrete Implementations Needed by SetupSimulationServices ---
        // These would ideally live in their respective service files
        private class PlaceholderPersonalityEvaluator : IPersonalityEvaluator { 
            public float GetDribblingTendencyModifier(PlayerData playerData) { return 1f; } 
            public float GetHesitationModifier(PlayerData playerData) { return 1f; } 
            public float GetPassingTendencyModifier(PlayerData playerData) { return 1f; } 
            public float GetRiskModifier(PlayerData playerData) { return 1f; } 
            public float GetShootingTendencyModifier(PlayerData playerData) { return 1f; } 
            public float GetTacklingTendencyModifier(PlayerData playerData) { return 1f; } 
        }
        
        private class PlaceholderTacticalEvaluator : ITacticalEvaluator { 
            public bool DoesActionMatchFocus(SimPlayer player, SimPlayer potentialTarget, PlayerAction actionType, OffensiveFocusPlay focus) { return true; } 
            public float GetPaceModifier(Tactic tactic) { return 1f; } 
            public float GetRiskModifier(Tactic tactic) { return 1f; } 
        }
        
        private class PlaceholderGameStateEvaluator : IGameStateEvaluator { 
            public float GetAttackRiskModifier(MatchState state, int playerTeamId) { return 1f; } 
            public float GetDefensiveAggressionModifier(MatchState state, int playerTeamId) { return 1f; } 
            public float GetGoalkeeperPassSafetyModifier(MatchState state, int playerTeamId) { return 1f; } 
            public bool IsCounterAttackOpportunity(SimPlayer player, MatchState state) { return false; } 
        }
        
        private class PlaceholderPassingDecisionMaker : IPassingDecisionMaker { 
            public List<PassOption> EvaluatePassOptions(SimPlayer passer, MatchState state, Tactic tactic, bool safeOnly) { return new List<PassOption>(); } 
            public PassOption GetBestPassOption(SimPlayer passer, MatchState state, Tactic tactic) { return null; } 
        }
        
        private class PlaceholderShootingDecisionMaker : IShootingDecisionMaker { 
            public float EvaluateShootScore(SimPlayer shooter, MatchState state, Tactic tactic) { return 0.1f; } 
        }
        
        private class PlaceholderDribblingDecisionMaker : IDribblingDecisionMaker { 
            public float EvaluateDribbleScore(SimPlayer player, MatchState state, Tactic tactic) { return 0.1f; } 
        }
        
        private class PlaceholderDefensiveDecisionMaker : IDefensiveDecisionMaker { 
            public DefensiveAction DecideDefensiveAction(SimPlayer player, MatchState state, Tactic tactic) { 
                return new DefensiveAction { Action = PlayerAction.MovingToPosition, TargetPosition = player.Position }; 
            } 
        }
        
        private class PlaceholderGoalkeeperPositioner : IGoalkeeperPositioner { 
            public Vector2 GetGoalkeeperAttackingSupportPosition(SimPlayer gk, MatchState state) { return gk.Position; } 
            public Vector2 GetGoalkeeperDefensivePosition(SimPlayer gk, MatchState state) { return gk.Position; } 
            public Vector2 GetGoalkeeperSavePosition(SimPlayer gk, MatchState state, Vector3 predictedImpactPoint3D) { return gk.Position; } 
        }
        
        private class DefaultMatchEventHandler : IMatchEventHandler { 
            private readonly IGeometryProvider _geometry; 
            
            public DefaultMatchEventHandler(IGeometryProvider geometry) { 
                _geometry = geometry; 
            } 
            
            public void HandleActionResult(ActionResult result, MatchState state) { } 
            public void HandleOutOfBounds(ActionResult result, MatchState state, Vector3? intersectionPoint3D = null) { } 
            public void ResetPlayerActionState(SimPlayer player, ActionResultOutcome outcomeContext = ActionResultOutcome.Success) { } 
            public void HandlePossessionChange(MatchState state, int newPossessionTeamId, bool ballIsLoose = false) { } 
            
            public void LogEvent(MatchState state, string description, int? teamId = null, int? playerId = null) { 
                if(state?.MatchEvents != null) 
                    state.MatchEvents.Add(new MatchEvent(state.MatchTimeSeconds, description, teamId, playerId)); 
            } 
            
            public void HandleStepError(MatchState state, string stepName, Exception ex) { 
                Debug.LogError($"STEP ERROR [{stepName}]: {ex.Message}"); 
                if(state != null) 
                    state.CurrentPhase = GamePhase.Finished; 
                LogEvent(state, $"ERROR during {stepName}: Simulation aborted."); 
            } 
        }
        
        private class DefaultPlayerSetupHandler : IPlayerSetupHandler { 
            private readonly IMatchEventHandler _eventHandler; 
            
            public DefaultPlayerSetupHandler(IMatchEventHandler eventHandler) { 
                _eventHandler = eventHandler; 
            } 
            
            public bool PopulateAllPlayers(MatchState state) { return true; } 
            public bool SelectStartingLineups(MatchState state) { return true; } 
            public bool SelectStartingLineup(MatchState state, TeamData team, int teamSimId) { return true; } 
            public void PlacePlayersInFormation(MatchState state, List<SimPlayer> players, bool isHomeTeam, bool isKickOff) { } 
        }
        
        private class DefaultSimulationTimer : ISimulationTimer { 
            public void UpdateTimers(MatchState state, float deltaTime, IMatchEventHandler eventHandler) { } 
        }
        
        private class DefaultEventDetector : IEventDetector { 
            private readonly IGeometryProvider _geometry; 
            
            public DefaultEventDetector(IGeometryProvider geometry) { 
                _geometry = geometry; 
            } 
            
            public void CheckPassiveEvents(MatchState state, IMatchEventHandler eventHandler) { } 
            public void CheckReactiveEvents(MatchState state, IActionResolver actionResolver, IMatchEventHandler eventHandler) { } 
        }
        
        private class DefaultMatchFinalizer : IMatchFinalizer { 
            public MatchResult FinalizeResult(MatchState state, DateTime matchDate) { 
                if(state == null) 
                    return new MatchResult(-1, -2, "ERR", "ERR"); 
                
                return new MatchResult(
                    state.HomeTeamData.TeamID, 
                    state.AwayTeamData.TeamID, 
                    state.HomeTeamData.Name, 
                    state.AwayTeamData.Name
                ) {
                    HomeScore = state.HomeScore, 
                    AwayScore = state.AwayScore, 
                    MatchDate = matchDate, 
                    HomeStats = state.CurrentHomeStats, 
                    AwayStats = state.CurrentAwayStats
                }; 
            } 
        }
    }
}