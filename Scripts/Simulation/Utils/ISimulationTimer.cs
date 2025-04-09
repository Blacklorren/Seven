using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Interfaces; // Added for IMatchEventHandler

namespace HandballManager.Simulation.Utils // Changed from Interfaces to Utils
{
    public interface ISimulationTimer
    {
        void UpdateTimers(MatchState state, float deltaTime, IMatchEventHandler eventHandler);
    }
}