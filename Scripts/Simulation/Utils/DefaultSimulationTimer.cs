using UnityEngine;
using System;
using System.Collections.Generic; // For List
using System.Linq;
using HandballManager.Simulation.Core.MatchData;
using HandballManager.Simulation.Events; // For ToList() extension method

namespace HandballManager.Simulation.Utils // Changed from Services to Utils
{
    public class DefaultSimulationTimer : ISimulationTimer
    {
        public void UpdateTimers(MatchState state, float deltaTime, IMatchEventHandler eventHandler)
        {
            // --- Logic copied from MatchSimulator ---
            if (state == null) return;

            // --- Timeout Timer --- (Handled differently - only runs if state IS Timeout)
            // This logic belongs *outside* the timer service, in the main loop's check
            // if (state.CurrentPhase == GamePhase.Timeout) { ... return; } -> This check is in MatchSimulator loop

            // --- Player Timers (Suspension, Action Prep) ---
            // We need to iterate over a copy of the players collection because the re-entry logic
            // modifies team court lists (adding/removing players) which could affect iteration safety.
            // ToList() creates a safe copy to iterate over while the original collection may change.
            var playersToUpdate = state.AllPlayers.Values.ToList(); // Creating a copy for safe iteration

            foreach (var player in playersToUpdate) {
                if (player == null) continue;
                try {
                    // --- Suspension Timer & Re-entry Logic ---
                    if (player.IsSuspended() && player.SuspensionTimer > 0) {
                        player.SuspensionTimer -= deltaTime;
                        if (player.SuspensionTimer <= 0f) {
                            player.SuspensionTimer = 0f;
                            List<SimPlayer> teamOnCourt = state.GetTeamOnCourt(player.TeamSimId);
                            bool canReEnter = teamOnCourt != null && teamOnCourt.Count < 7;

                            // Update IsOnCourt status based on re-entry possibility
                            player.IsOnCourt = canReEnter;

                            if (canReEnter) {
                                // Player re-enters - Add to court list IF NOT ALREADY THERE (safety check)
                                if (!teamOnCourt.Contains(player)) {
                                    teamOnCourt.Add(player);
                                }
                                player.Position = player.TeamSimId == 0 ? new Vector2(2f, 1f) : new Vector2(38f, 1f); // Near bench
                                player.TargetPosition = player.Position;
                                player.CurrentAction = PlayerAction.Idle; // Reset action
                                eventHandler?.LogEvent(state, $"Player {player.BaseData?.FullName ?? "Unknown"} re-enters after suspension.", player.GetTeamId(), player.GetPlayerId());
                            } else {
                                // Player suspension ended, but stays off court
                                if (teamOnCourt != null && teamOnCourt.Contains(player)) {
                                    teamOnCourt.Remove(player); // Ensure removed if somehow still in list
                                }
                                player.Position = new Vector2(-100, -100); // Keep off pitch
                                player.CurrentAction = PlayerAction.Idle; // Reset action
                                eventHandler?.LogEvent(state, $"Player {player.BaseData?.FullName ?? "Unknown"} suspension ended, but team is full.", player.GetTeamId(), player.GetPlayerId());
                            }
                        }
                    }
                    // --- Action Preparation Timer ---
                    // Only tick down if player is actually on court and preparing something
                    if (player.IsOnCourt && !player.IsSuspended() && player.ActionTimer > 0) {
                        player.ActionTimer -= deltaTime;
                        if (player.ActionTimer < 0f) player.ActionTimer = 0f; // Clamp to zero
                    }
                } catch (Exception ex) { Debug.LogError($"Error updating timers for player {player.GetPlayerId()}: {ex.Message}"); }
            }
        }
    }
}