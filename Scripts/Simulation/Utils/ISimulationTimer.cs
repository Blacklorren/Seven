using HandballManager.Simulation.Core.MatchData;
using HandballManager.Simulation.Events;

namespace HandballManager.Simulation.Utils // Changed from Interfaces to Utils
{
    public interface ISimulationTimer
    {
        void UpdateTimers(MatchState state, float deltaTime, IMatchEventHandler eventHandler);
    }
}