using HandballManager.Simulation.Core.MatchData;

namespace HandballManager.Simulation.Events
{
    public interface IMatchFinalizer
    {
        void UpdateTimers(MatchState state, float deltaTime, IMatchEventHandler eventHandler);
    }
}