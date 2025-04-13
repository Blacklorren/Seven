using HandballManager.Simulation.AI;
using HandballManager.Simulation.Events;
using HandballManager.Simulation.Physics;

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Aggregates engine-related services to reduce dependency injection complexity.
    /// This interface follows the Facade pattern to simplify access to multiple engine components.
    /// </summary>
    public interface IEngineServiceBundle
    {
        /// <summary>Gets the movement simulator.</summary>
        IMovementSimulator MovementSimulator { get; }
        
        /// <summary>Gets the action resolver.</summary>
        IActionResolver ActionResolver { get; }
        
        /// <summary>Gets the tactic positioner.</summary>
        ITacticPositioner TacticPositioner { get; }
    }
}