using HandballManager.Simulation.MatchData;
using HandballManager.Core;

namespace HandballManager.Simulation.Utils // Changed from Interfaces to Utils
{
    public interface IPhaseManager
    {
        bool CheckAndHandleHalfTime(MatchState state, float timeBeforeStep, float timeAfterStep);
        bool CheckAndHandleFullTime(MatchState state, float timeAfterStep);
        void TransitionToPhase(MatchState state, GamePhase newPhase, bool forceSetup = false);
        void HandlePhaseTransitions(MatchState state);
        int DetermineKickoffTeam(MatchState state);
        bool SetupForKickOff(MatchState state, int startingTeamId); // Expose specific setups if needed externally
        bool SetupForSetPiece(MatchState state);
        bool SetupForPenalty(MatchState state);
        bool SetupForHalfTime(MatchState state);
    }
}