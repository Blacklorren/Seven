using HandballManager.Simulation.AI;
using HandballManager.Simulation.Events;
using HandballManager.Simulation.Physics;
using System;

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Implementation of the IEngineServiceBundle interface that aggregates all engine-related services.
    /// This class follows the Facade pattern to simplify access to multiple engine components.
    /// </summary>
    public class EngineServiceBundle : IEngineServiceBundle
    {
        /// <summary>Gets the movement simulator.</summary>
        public IMovementSimulator MovementSimulator { get; }
        
        /// <summary>Gets the action resolver.</summary>
        public IActionResolver ActionResolver { get; }
        
        /// <summary>Gets the tactic positioner.</summary>
        public ITacticPositioner TacticPositioner { get; }

        /// <summary>
        /// Initializes a new instance of the EngineServiceBundle with all required engine components.
        /// </summary>
        public EngineServiceBundle(
            IMovementSimulator movementSimulator,
            IActionResolver actionResolver,
            ITacticPositioner tacticPositioner)
        {
            MovementSimulator = movementSimulator ?? throw new ArgumentNullException(nameof(movementSimulator));
            ActionResolver = actionResolver ?? throw new ArgumentNullException(nameof(actionResolver));
            TacticPositioner = tacticPositioner ?? throw new ArgumentNullException(nameof(tacticPositioner));
        }
    }
}