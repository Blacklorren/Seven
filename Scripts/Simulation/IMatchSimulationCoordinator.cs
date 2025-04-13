using HandballManager.Data;
using HandballManager.Gameplay; // For Tactic
using System.Threading; // For CancellationToken
using System.Threading.Tasks; // For Task

namespace HandballManager.Simulation
{
    public interface IMatchSimulationCoordinator
    {
        Task<MatchResult> RunSimulationAsync(TeamData home, TeamData away, 
                                          Tactic homeTactic, Tactic awayTactic,
                                          CancellationToken cancellationToken = default);
        void AbortCurrentSimulation();
        void CleanupResources();
        void InitializeMatch(TeamData home, TeamData away);
        void SimulateNextAction();
        MatchResult FinalizeMatch();
    }
}