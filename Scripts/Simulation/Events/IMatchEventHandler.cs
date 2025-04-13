using UnityEngine;
using System;
using HandballManager.Simulation.Core.MatchData;

namespace HandballManager.Simulation.Events
{
    /// <summary>
    /// Handles core match events and state transitions during simulation
    /// </summary>
    public interface IMatchEventHandler
    {
        /// <summary>
        /// Processes action outcomes and updates match state
        /// </summary>
        /// <param name="result">Outcome of the resolved action</param>
        /// <param name="state">Current match state</param>
        void HandleActionResult(ActionResult result, MatchState state);
                /// <summary>
        /// Handles out-of-bounds events and updates match state
        /// </summary>
        /// <param name="result">Outcome of the out-of-bounds action</param>
        /// <param name="state">Current match state</param>
        /// <param name="intersectionPoint3D">Optional 3D intersection point where ball went out</param>
        void HandleOutOfBounds(ActionResult result, MatchState state, Vector3? intersectionPoint3D = null);
                /// <summary>
        /// Resets a player's action state after outcome resolution
        /// </summary>
        /// <param name="player">Player whose state needs resetting</param>
        /// <param name="outcomeContext">The context that triggered the reset</param>
        void ResetPlayerActionState(SimPlayer player, ActionResultOutcome outcomeContext = ActionResultOutcome.Success);
                /// <summary>
        /// Handles possession changes and updates team states
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="newPossessionTeamId">Team ID gaining possession</param>
        /// <param name="ballIsLoose">True if possession changed due to loose ball</param>
        void HandlePossessionChange(MatchState state, int newPossessionTeamId, bool ballIsLoose = false);
                /// <summary>
        /// Logs game event with optional team/player context
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="description">Event description</param>
        /// <param name="teamId">Related team ID (if applicable)</param>
        /// <param name="playerId">Related player ID (if applicable)</param>
        void LogEvent(MatchState state, string description, int? teamId = null, int? playerId = null);
                /// <summary>
        /// Handles errors during match simulation steps
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="stepName">Name of the failed step</param>
        /// <param name="ex">Exception that occurred</param>
        void HandleStepError(MatchState state, string stepName, Exception ex);
        /// <summary>
        /// Transitions match to specified game phase
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="newPhase">Target phase to transition to</param>
        void TransitionToPhase(MatchState state, GamePhase newPhase);
    }
}