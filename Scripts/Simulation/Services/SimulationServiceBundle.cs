using HandballManager.Simulation.Physics;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Events;
using System;

namespace HandballManager.Simulation.Services
{
    /// <summary>
    /// Implementation of the ISimulationServiceBundle interface that aggregates all simulation-related services.
    /// This class follows the Facade pattern to simplify access to multiple simulation components.
    /// </summary>
    public class SimulationServiceBundle : ISimulationServiceBundle
    {
        /// <summary>Gets the geometry provider.</summary>
        public IGeometryProvider GeometryProvider { get; }
        
        /// <summary>Gets the ball physics calculator.</summary>
        public IBallPhysicsCalculator BallPhysicsCalculator { get; }
        
        /// <summary>Gets the movement simulator.</summary>
        public IMovementSimulator MovementSimulator { get; }
        
        /// <summary>Gets the phase manager.</summary>
        public IPhaseManager PhaseManager { get; }
        
        /// <summary>Gets the player setup handler.</summary>
        public IPlayerSetupHandler PlayerSetupHandler { get; }
        
        /// <summary>Gets the event detector.</summary>
        public IEventDetector EventDetector { get; }
        
        /// <summary>Gets the match event handler.</summary>
        public IMatchEventHandler MatchEventHandler { get; }
        
        /// <summary>Gets the match finalizer.</summary>
        public IMatchFinalizer MatchFinalizer { get; }
        
        /// <summary>Gets the simulation timer.</summary>
        public ISimulationTimer SimulationTimer { get; }

        /// <summary>
        /// Initializes a new instance of the SimulationServiceBundle with all required simulation components.
        /// </summary>
        public SimulationServiceBundle(
            IGeometryProvider geometryProvider,
            IBallPhysicsCalculator ballPhysicsCalculator,
            IMovementSimulator movementSimulator,
            IPhaseManager phaseManager,
            IPlayerSetupHandler playerSetupHandler,
            IEventDetector eventDetector,
            IMatchEventHandler matchEventHandler,
            IMatchFinalizer matchFinalizer,
            ISimulationTimer simulationTimer)
        {
            GeometryProvider = geometryProvider ?? throw new ArgumentNullException(nameof(geometryProvider));
            BallPhysicsCalculator = ballPhysicsCalculator ?? throw new ArgumentNullException(nameof(ballPhysicsCalculator));
            MovementSimulator = movementSimulator ?? throw new ArgumentNullException(nameof(movementSimulator));
            PhaseManager = phaseManager ?? throw new ArgumentNullException(nameof(phaseManager));
            PlayerSetupHandler = playerSetupHandler ?? throw new ArgumentNullException(nameof(playerSetupHandler));
            EventDetector = eventDetector ?? throw new ArgumentNullException(nameof(eventDetector));
            MatchEventHandler = matchEventHandler ?? throw new ArgumentNullException(nameof(matchEventHandler));
            MatchFinalizer = matchFinalizer ?? throw new ArgumentNullException(nameof(matchFinalizer));
            SimulationTimer = simulationTimer ?? throw new ArgumentNullException(nameof(simulationTimer));
        }
    }
}