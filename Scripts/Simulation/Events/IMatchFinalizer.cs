using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Interfaces; // For IMatchEventHandler

namespace HandballManager.Simulation.Events
{
    public interface ISimulationTimer
    {
        void UpdateTimers(MatchState state, float deltaTime, IMatchEventHandler eventHandler);
    }
}