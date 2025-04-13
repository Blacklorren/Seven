using HandballManager.Simulation.Core.MatchData;

namespace HandballManager.Simulation.Events
{
    public interface IEventDetector
    {
        /// <summary>Checks for reactive events like interceptions, blocks, saves, pickups.</summary>
        /// <param name="state">Current match state.</param>
        /// <param name="actionResolver">Resolver for calculating action outcomes and probabilities</param>
        /// <param name="eventHandler">Handler to process detected events.</param>
        void CheckReactiveEvents(MatchState state, IActionResolver actionResolver, IMatchEventHandler eventHandler);

        /// <summary>Checks for passive events like ball crossing goal/side lines.</summary>
        /// <param name="state">Current match state.</param>
        /// <param name="eventHandler">Handler to process detected events.</param>
        void CheckPassiveEvents(MatchState state, IMatchEventHandler eventHandler);
    }
}