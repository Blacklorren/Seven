using UnityEngine;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Core;
using System;

namespace HandballManager.Simulation.Events
{
    public interface IMatchEventHandler
    {
        void HandleActionResult(ActionResult result, MatchState state);
        void HandleOutOfBounds(ActionResult result, MatchState state, Vector3? intersectionPoint3D = null);
        void ResetPlayerActionState(SimPlayer player, ActionResultOutcome outcomeContext = ActionResultOutcome.Success);
        void HandlePossessionChange(MatchState state, int newPossessionTeamId, bool ballIsLoose = false);
        void LogEvent(MatchState state, string description, int? teamId = null, int? playerId = null);
        void HandleStepError(MatchState state, string stepName, Exception ex);
        // Add TransitionToPhase if event handler needs to trigger phase changes directly
        // void TransitionToPhase(MatchState state, Core.GamePhase newPhase);
    }
}