using HandballManager.Simulation.Physics;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Events;

namespace HandballManager.Simulation.Services
{
    /// <summary>
    /// Aggregates simulation-related services to reduce dependency injection complexity.
    /// This interface follows the Facade pattern to simplify access to multiple simulation components.
    /// </summary>
    public interface ISimulationServiceBundle
    {
        /// <summary>Gets the geometry provider.</summary>
        IGeometryProvider GeometryProvider { get; }
        
        /// <summary>Gets the ball physics calculator.</summary>
        IBallPhysicsCalculator BallPhysicsCalculator { get; }
        
        /// <summary>Gets the movement simulator.</summary>
        IMovementSimulator MovementSimulator { get; }
        
        /// <summary>Gets the phase manager.</summary>
        IPhaseManager PhaseManager { get; }
        
        /// <summary>Gets the player setup handler.</summary>
        IPlayerSetupHandler PlayerSetupHandler { get; }
        
        /// <summary>Gets the event detector.</summary>
        IEventDetector EventDetector { get; }
        
        /// <summary>Gets the match event handler.</summary>
        IMatchEventHandler MatchEventHandler { get; }
        
        /// <summary>Gets the match finalizer.</summary>
        IMatchFinalizer MatchFinalizer { get; }
        
        /// <summary>Gets the simulation timer.</summary>
        ISimulationTimer SimulationTimer { get; }
    }
}