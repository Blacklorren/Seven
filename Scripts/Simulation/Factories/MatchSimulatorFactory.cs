using HandballManager.Core.Logging;
using HandballManager.Simulation.AI;
using HandballManager.Simulation.Physics;
using HandballManager.Simulation.Events;
using HandballManager.Simulation.Core;
using HandballManager.Simulation.Core.MatchData;
using HandballManager.Simulation.Core.Interfaces;
using HandballManager.Simulation.Utils;
using System;
using System.Threading;
using UnityEngine;

namespace HandballManager.Simulation.Factories
{
    public class MatchSimulatorFactory : IMatchSimulatorFactory
    {
        private readonly HandballManager.Core.Logging.ILogger _logger;
        private readonly IPhaseManager _phaseManager;
        private readonly ISimulationTimer _simulationTimer;
        private readonly IBallPhysicsCalculator _ballPhysicsCalculator;
        private readonly IMovementSimulator _movementSimulator;
        private readonly IPlayerAIController _aiController;
        private readonly IActionResolver _actionResolver;
        private readonly IEventDetector _eventDetector;
        private readonly IMatchEventHandler _matchEventHandler;
        private readonly IPlayerSetupHandler _playerSetupHandler;
        private readonly IMatchFinalizer _matchFinalizer;
        private readonly IGeometryProvider _geometryProvider;
        
        /// <summary>
        /// Initializes a new instance of the MatchSimulatorFactory with all required dependencies.
        /// </summary>
        public MatchSimulatorFactory(
            HandballManager.Core.Logging.ILogger logger,
            IPhaseManager phaseManager,
            ISimulationTimer simulationTimer,
            IBallPhysicsCalculator ballPhysicsCalculator,
            IMovementSimulator movementSimulator,
            IPlayerAIController aiController,
            IActionResolver actionResolver,
            IEventDetector eventDetector,
            IMatchEventHandler matchEventHandler,
            IPlayerSetupHandler playerSetupHandler,
            IMatchFinalizer matchFinalizer,
            IGeometryProvider geometryProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _phaseManager = phaseManager ?? throw new ArgumentNullException(nameof(phaseManager));
            _simulationTimer = simulationTimer ?? throw new ArgumentNullException(nameof(simulationTimer));
            _ballPhysicsCalculator = ballPhysicsCalculator ?? throw new ArgumentNullException(nameof(ballPhysicsCalculator));
            _movementSimulator = movementSimulator ?? throw new ArgumentNullException(nameof(movementSimulator));
            _aiController = aiController ?? throw new ArgumentNullException(nameof(aiController));
            _actionResolver = actionResolver ?? throw new ArgumentNullException(nameof(actionResolver));
            _eventDetector = eventDetector ?? throw new ArgumentNullException(nameof(eventDetector));
            _matchEventHandler = matchEventHandler ?? throw new ArgumentNullException(nameof(matchEventHandler));
            _playerSetupHandler = playerSetupHandler ?? throw new ArgumentNullException(nameof(playerSetupHandler));
            _matchFinalizer = matchFinalizer ?? throw new ArgumentNullException(nameof(matchFinalizer));
            _geometryProvider = geometryProvider ?? throw new ArgumentNullException(nameof(geometryProvider));
                    }

        
        /// <summary>
        /// Creates a new, configured instance of MatchSimulator with specified teams and tactics.
        /// </summary>
        /// <param name="homeTeam">Home team data.</param>
        /// <param name="awayTeam">Away team data.</param>
        /// <param name="homeTactic">Home team tactic.</param>
        /// <param name="awayTactic">Away team tactic.</param>
        /// <param name="seed">The random seed for the simulation (-1 for default/time-based).</param>
        /// <param name="progress">Optional reporter for simulation progress updates (0.0 to 1.0).</param>
        /// <param name="cancellationToken">Optional token to allow cancellation of the simulation.</param>
        /// <returns>A new instance of MatchSimulator, ready to run a match.</returns>
        
        /// <summary>
        /// Creates a new, configured instance of MatchSimulator using a pre-configured MatchState.
        /// </summary>
        /// <param name="matchState">The match state containing team data, tactics, and simulation configuration.</param>
        /// <param name="progress">Optional reporter for simulation progress updates (0.0 to 1.0).</param>
        /// <param name="cancellationToken">Optional token to allow cancellation of the simulation.</param>
        /// <returns>A new instance of MatchSimulator, ready to run a match.</returns>
        public MatchSimulator Create(
            MatchState matchState,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (matchState == null)
            {
                throw new ArgumentNullException(nameof(matchState));
            }
            
            _logger.LogInformation($"Creating new MatchSimulator instance for {matchState.HomeTeamData?.Name ?? "Unknown"} vs {matchState.AwayTeamData?.Name ?? "Unknown"} with seed: {matchState.RandomSeed}");
            
            // Create and return a new MatchSimulator with all dependencies injected
            var simulator = new MatchSimulator(
                matchState,
                _phaseManager,
                _simulationTimer,
                _ballPhysicsCalculator,
                _movementSimulator,
                _aiController,
                _actionResolver,
                _eventDetector,
                _matchEventHandler,
                _playerSetupHandler,
                _matchFinalizer,
                _geometryProvider
            );
            
            return simulator;
        }
    }
}