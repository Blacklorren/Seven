#region Core Dependencies
using HandballManager.Data;
using HandballManager.Gameplay;
using CoreLogger = HandballManager.Core.Logging.ILogger;
using HandballManager.Core.Time;

#endregion

#region Simulation Dependencies
using HandballManager.Simulation.AI.Decision;
using HandballManager.Simulation.AI.Evaluation;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Simulation.Physics;
using HandballManager.Simulation.Core.Interfaces;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Events;
using HandballManager.Simulation.AI;
using HandballManager.Simulation.Core.MatchData;
using HandballManager.Simulation.Core;
using IMatchEngine = HandballManager.Simulation.Core.Interfaces.IMatchEngine;

#endregion

#region System Dependencies
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


#endregion

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Implementation of the match engine that orchestrates the simulation of a handball match.
    /// </summary>
    public class MatchEngine : IMatchEngine
    {
        private const int DefaultMinPlayersRequired = 7;
        private readonly CoreLogger _logger;
        private readonly IGameTimeProvider _timeProvider;
        
        // Factory for creating MatchSimulator instances
        private readonly IMatchSimulatorFactory _simulatorFactory;
        
        // All dependencies injected through constructor
        private readonly IGeometryProvider _geometryProvider;
        private readonly IBallPhysicsCalculator _ballPhysicsCalculator;
        private readonly IPhaseManager _phaseManager;
        public MatchEngine() { }
        private readonly IPlayerSetupHandler _playerSetupHandler;
        private readonly IEventDetector _eventDetector;
        private readonly IMatchEventHandler _matchEventHandler;
        private readonly IMatchFinalizer _matchFinalizer;
        private readonly ISimulationTimer _simulationTimer;
        private readonly IMovementSimulator _movementSimulator;
        private readonly IPlayerAIController _aiController;
        private readonly IActionResolver _actionResolver;
        private readonly ITacticPositioner _tacticPositioner;
        
        // AI decision makers and evaluators
        private readonly IPersonalityEvaluator _personalityEvaluator;
        private readonly ITacticalEvaluator _tacticalEvaluator;
        private readonly IGameStateEvaluator _gameStateEvaluator;
        private readonly IPassingDecisionMaker _passingDecisionMaker;
        private readonly IShootingDecisionMaker _shootingDecisionMaker;
        private readonly IDribblingDecisionMaker _dribblingDecisionMaker;
        private readonly IDefensiveDecisionMaker _defensiveDecisionMaker;
        private readonly IGoalkeeperPositioner _goalkeeperPositioner;

        /// <summary>
        /// Initializes a new instance of the MatchEngine with all required dependencies.
        /// </summary>
        public MatchEngine(
            // Core dependencies
            CoreLogger logger,
            IGameTimeProvider timeProvider,
            
            // Factory for creating MatchSimulator instances
            IMatchSimulatorFactory simulatorFactory,
            
            // Simulation services
            IGeometryProvider geometryProvider,
            IBallPhysicsCalculator ballPhysicsCalculator,
            IPhaseManager phaseManager,
            IPlayerSetupHandler playerSetupHandler,
            IEventDetector eventDetector,
            IMatchEventHandler matchEventHandler,
            IMatchFinalizer matchFinalizer,
            ISimulationTimer simulationTimer,
            
            // Core engines
            IMovementSimulator movementSimulator,
            IActionResolver actionResolver,
            ITacticPositioner tacticPositioner,
            
            // AI controller and evaluators
            IPlayerAIController aiController,
            IPersonalityEvaluator personalityEvaluator,
            ITacticalEvaluator tacticalEvaluator,
            IGameStateEvaluator gameStateEvaluator,
            IPassingDecisionMaker passingDecisionMaker,
            IShootingDecisionMaker shootingDecisionMaker,
            IDribblingDecisionMaker dribblingDecisionMaker,
            IDefensiveDecisionMaker defensiveDecisionMaker,
            IGoalkeeperPositioner goalkeeperPositioner)
        {
            // Core dependencies
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            
            // Factory for creating MatchSimulator instances
            _simulatorFactory = simulatorFactory ?? throw new ArgumentNullException(nameof(simulatorFactory));
            
            // Simulation services
            _geometryProvider = geometryProvider ?? throw new ArgumentNullException(nameof(geometryProvider));
            _ballPhysicsCalculator = ballPhysicsCalculator ?? throw new ArgumentNullException(nameof(ballPhysicsCalculator));
            _phaseManager = phaseManager ?? throw new ArgumentNullException(nameof(phaseManager));
            _playerSetupHandler = playerSetupHandler ?? throw new ArgumentNullException(nameof(playerSetupHandler));
            _eventDetector = eventDetector ?? throw new ArgumentNullException(nameof(eventDetector));
            _matchEventHandler = matchEventHandler ?? throw new ArgumentNullException(nameof(matchEventHandler));
            _matchFinalizer = matchFinalizer ?? throw new ArgumentNullException(nameof(matchFinalizer));
            _simulationTimer = simulationTimer ?? throw new ArgumentNullException(nameof(simulationTimer));
            
            // Core engines
            _movementSimulator = movementSimulator ?? throw new ArgumentNullException(nameof(movementSimulator));
            _actionResolver = actionResolver ?? throw new ArgumentNullException(nameof(actionResolver));
            _tacticPositioner = tacticPositioner ?? throw new ArgumentNullException(nameof(tacticPositioner));
            
            // AI controller and evaluators
            _aiController = aiController ?? throw new ArgumentNullException(nameof(aiController));
            _personalityEvaluator = personalityEvaluator ?? throw new ArgumentNullException(nameof(personalityEvaluator));
            _tacticalEvaluator = tacticalEvaluator ?? throw new ArgumentNullException(nameof(tacticalEvaluator));
            _gameStateEvaluator = gameStateEvaluator ?? throw new ArgumentNullException(nameof(gameStateEvaluator));
            _passingDecisionMaker = passingDecisionMaker ?? throw new ArgumentNullException(nameof(passingDecisionMaker));
            _shootingDecisionMaker = shootingDecisionMaker ?? throw new ArgumentNullException(nameof(shootingDecisionMaker));
            _dribblingDecisionMaker = dribblingDecisionMaker ?? throw new ArgumentNullException(nameof(dribblingDecisionMaker));
            _defensiveDecisionMaker = defensiveDecisionMaker ?? throw new ArgumentNullException(nameof(defensiveDecisionMaker));
            _goalkeeperPositioner = goalkeeperPositioner ?? throw new ArgumentNullException(nameof(goalkeeperPositioner));
            
            _logger.LogInformation("MatchEngine initialized with all dependencies.");
        }

        // SetupSimulationServices method removed as all dependencies are now injected through the constructor


        /// <summary>
        /// Simulates a complete handball match between two teams using the injected dependencies.
        /// </summary>
        /// <param name="homeTeam">The home team data.</param>
        /// <param name="awayTeam">The away team data.</param>
        /// <param name="homeTactic">The tactic for the home team.</param>
        /// <param name="awayTactic">The tactic for the away team.</param>
        /// <param name="seed">Optional random seed for deterministic simulation (-1 for time-based).</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The match result containing score and statistics.</returns>
        public MatchResult SimulateMatch(TeamData homeTeam, TeamData awayTeam, Tactic homeTactic, Tactic awayTactic,
                                         int seed = -1, IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            string homeTeamName = homeTeam?.Name ?? "NULL_HOME_TEAM";
            string awayTeamName = awayTeam?.Name ?? "NULL_AWAY_TEAM";
            _logger.LogInformation($"Starting match simulation setup: {homeTeamName} vs {awayTeamName} (Seed: {seed})");

            // --- Input Validation ---
            if (homeTeam == null || awayTeam == null) return CreateErrorResult(homeTeam, awayTeam, "Home or Away TeamData is null.");
            string rosterValidationError = ValidateTeamRoster(homeTeam, "Home");
            if (rosterValidationError != null) return CreateErrorResult(homeTeam, awayTeam, rosterValidationError);
            rosterValidationError = ValidateTeamRoster(awayTeam, "Away");
            if (rosterValidationError != null) return CreateErrorResult(homeTeam, awayTeam, rosterValidationError);

            // --- Tactic Handling ---
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

                // --- Create MatchSimulator instance using the factory ---
                matchSimulator = _simulatorFactory.Create(
                    matchState,
                    progress,
                    cancellationToken
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
                result = CreateErrorResult(homeTeam, awayTeam, "Simulation was cancelled");
            }
            catch (InvalidOperationException opEx) { 
                _logger.LogError($"Simulation setup error ({homeTeamName} vs {awayTeamName}): {opEx.Message}", opEx);
                result = CreateErrorResult(homeTeam, awayTeam, $"Simulation Setup Exception: {opEx.Message}");
            }
            catch (Exception ex) { 
                _logger.LogError($"Simulation error ({homeTeamName} vs {awayTeamName}): {ex.Message}", ex);
                result = CreateErrorResult(homeTeam, awayTeam, $"Simulation Exception: {ex.Message}");
            }

            return result ?? CreateErrorResult(homeTeam, awayTeam, "Simulation returned null result unexpectedly.");
        }

        // Keep ValidateTeamRoster and CreateErrorResult methods as they are part of MatchEngine's responsibility.
        private string ValidateTeamRoster(TeamData team, string teamIdentifier) { 
            if (team == null) return $"{teamIdentifier} team is null";
            if (team.Roster == null) return $"{teamIdentifier} team has no players";
            if (team.Roster.Count < DefaultMinPlayersRequired) 
                return $"{teamIdentifier} team has insufficient players ({team.Roster.Count}/{DefaultMinPlayersRequired})";
            return null; 
        }
        
        private MatchResult CreateErrorResult(string reason) { 
            _logger.LogError($"Match simulation error: {reason}");
            return new MatchResult(-1, -2, "Error", "Error") { 
                ErrorMessage = reason 
            }; 
        }
        
        /// <summary>
        /// Creates an error result with team context preserved.
        /// </summary>
        /// <param name="home">Home team data.</param>
        /// <param name="away">Away team data.</param>
        /// <param name="reason">The error reason.</param>
        /// <returns>A MatchResult indicating an error but preserving team context.</returns>
        private MatchResult CreateErrorResult(TeamData home, TeamData away, string reason) { 
            _logger.LogError($"Match simulation error: {reason}");
            return new MatchResult(
                -1, 
                -2, 
                home?.Name ?? "Invalid Home", 
                away?.Name ?? "Invalid Away"
            ) { 
                ErrorMessage = reason,
                IsAborted = true
            }; 
        }

        /// <summary>
        /// Asynchronously simulates a complete handball match between two teams.
        /// </summary>
        /// <param name="homeTeam">The home team data.</param>
        /// <param name="awayTeam">The away team data.</param>
        /// <param name="homeTactic">The tactic for the home team.</param>
        /// <param name="awayTactic">The tactic for the away team.</param>
        /// <param name="cancellationToken">Token to cancel the simulation.</param>
        /// <returns>A task containing the match result with score and statistics.</returns>
        public async Task<MatchResult> SimulateMatchAsync(
            TeamData homeTeam,
            TeamData awayTeam,
            Tactic homeTactic,
            Tactic awayTactic,
            CancellationToken cancellationToken = default)
        {
            // Utilise un seed basé sur le temps pour la simulation asynchrone
            int seed = -1;
            IProgress<float> progress = null;

            // Délègue le travail à la méthode synchrone mais dans un contexte asynchrone
            return await Task.Run(() => {
                return SimulateMatch(homeTeam, awayTeam, homeTactic, awayTactic, seed, progress, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Gets whether the match is complete.
        /// </summary>
        public bool IsMatchComplete { get; private set; } = false;

        /// <summary>
        /// Resets the match engine state.
        /// </summary>
        public void ResetMatch()
        {
            IsMatchComplete = false;
            _logger.LogInformation("Match engine state reset.");
        }

        // Placeholder implementations removed as they're no longer needed with dependency injection
    }
}