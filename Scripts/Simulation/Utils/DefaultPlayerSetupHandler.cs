using UnityEngine; // For Vector2
using HandballManager.Data;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Interfaces;
using HandballManager.Core; // For PlayerPosition
using HandballManager.Gameplay; // For Tactic
using HandballManager.Simulation.Common; // For IGeometryProvider (if needed directly, unlikely)
using System;
using System.Collections.Generic;
using System.Linq;


namespace HandballManager.Simulation.Utils // Changed from Services to Utils
{
    public class DefaultPlayerSetupHandler : IPlayerSetupHandler
    {
        private readonly IMatchEventHandler _eventHandler;
        private readonly TacticPositioner _tacticPositioner; // Inject TacticPositioner
        private readonly IGeometryProvider _geometry; // Inject Geometry

        // Constants from original MatchSimulator related to positioning
        private const float DEF_GK_DEPTH = 0.5f;

        public DefaultPlayerSetupHandler(IMatchEventHandler eventHandler, TacticPositioner tacticPositioner, IGeometryProvider geometry)
        {
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _tacticPositioner = tacticPositioner ?? throw new ArgumentNullException(nameof(tacticPositioner)); // Store TacticPositioner
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry)); // Store Geometry
        }

        public bool PopulateAllPlayers(MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
            if (state == null) return false;
            bool success = true;
            try {
                if (!InitializeTeamPlayers(state, state.HomeTeamData, 0)) success = false;
                if (!InitializeTeamPlayers(state, state.AwayTeamData, 1)) success = false;
            } catch (Exception ex) {
                Debug.LogError($"[DefaultPlayerSetupHandler] Unexpected error populating players: {ex.Message}");
                success = false;
            }
            if (!success) {
                 Debug.LogError("[DefaultPlayerSetupHandler] Failed to successfully populate players for both teams.");
            }
            return success;
        }

        private bool InitializeTeamPlayers(MatchState state, TeamData team, int teamSimId)
        {
            // --- Logic copied from MatchSimulator ---
             if (state == null || team?.Roster == null) return false; // Added null check for team/roster

             bool success = true;
             foreach (var playerData in team.Roster) {
                  if (playerData == null || playerData.PlayerID <= 0) {
                      Debug.LogWarning($"[DefaultPlayerSetupHandler] Skipping invalid player data in team {team.Name}.");
                      continue;
                  }
                  if (!state.AllPlayers.ContainsKey(playerData.PlayerID)) {
                      try {
                         var simPlayer = new SimPlayer(playerData, teamSimId);
                         state.AllPlayers.Add(playerData.PlayerID, simPlayer);
                      } catch (Exception ex) {
                           Debug.LogError($"[DefaultPlayerSetupHandler] Error creating/adding SimPlayer for {playerData.FullName} (ID:{playerData.PlayerID}): {ex.Message}");
                           success = false;
                      }
                  } else {
                       Debug.LogWarning($"[DefaultPlayerSetupHandler] Player {playerData.Name} (ID: {playerData.PlayerID}) already exists in AllPlayers. Skipping duplicate add.");
                  }
             }
             return success;
        }

        public bool SelectStartingLineups(MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
             if (state == null) return false;
             try {
                 if (!SelectStartingLineup(state, state.HomeTeamData, 0)) return false;
                 if (!SelectStartingLineup(state, state.AwayTeamData, 1)) return false;

                 if (state.HomePlayersOnCourt?.Count != 7 || state.AwayPlayersOnCourt?.Count != 7) {
                     Debug.LogError($"[DefaultPlayerSetupHandler] Initialization failed: Final lineup count incorrect. H:{state.HomePlayersOnCourt?.Count ?? -1}, A:{state.AwayPlayersOnCourt?.Count ?? -1}");
                     return false;
                 }
                 return true;
             } catch (Exception ex) {
                 Debug.LogError($"[DefaultPlayerSetupHandler] Unexpected error selecting starting lineups: {ex.Message}");
                 return false;
             }
        }

        public bool SelectStartingLineup(MatchState state, TeamData team, int teamSimId)
        {
            // --- Logic copied from MatchSimulator ---
             if (state == null || team?.Roster == null) return false;

             var lineup = (teamSimId == 0) ? state.HomePlayersOnCourt : state.AwayPlayersOnCourt;
             var bench = (teamSimId == 0) ? state.HomeBench : state.AwayBench; // Get bench list
             lineup.Clear();
             bench.Clear(); // Clear bench before populating

             // Get all players for this team from the central dictionary
             var teamPlayers = state.AllPlayers.Values
                 .Where(p => p?.TeamSimId == teamSimId && p.BaseData != null)
                 .ToList();

             // Separate available (not injured) players
             var candidates = teamPlayers
                 .Where(p => !p.BaseData.IsInjured())
                 .ToList();

             // Add injured players directly to bench
             bench.AddRange(teamPlayers.Where(p => p.BaseData.IsInjured()));

             // Ensure GK selection
             var gk = candidates
                 .Where(p => p.BaseData.PrimaryPosition == PlayerPosition.Goalkeeper)
                 .OrderByDescending(p => p.BaseData.CurrentAbility)
                 .FirstOrDefault();
             if (gk == null) { Debug.LogError($"[DefaultPlayerSetupHandler] Team {team.Name} has no available Goalkeeper!"); return false; }
             lineup.Add(gk); candidates.Remove(gk);

             // Select Field Players
             int fieldPlayersNeeded = 6;
             var selectedFieldPlayers = new List<SimPlayer>();
             var neededPositions = Enum.GetValues(typeof(PlayerPosition)).Cast<PlayerPosition>().Where(p => p != PlayerPosition.Goalkeeper).ToList();

             foreach(var pos in neededPositions) {
                  if (selectedFieldPlayers.Count == fieldPlayersNeeded) break;
                  var playerForPos = candidates
                      .Where(p => p.BaseData.PrimaryPosition == pos)
                      .OrderByDescending(p => p.BaseData.CurrentAbility)
                      .FirstOrDefault();
                  if (playerForPos != null && !selectedFieldPlayers.Contains(playerForPos)) {
                       selectedFieldPlayers.Add(playerForPos);
                       candidates.Remove(playerForPos);
                  }
             }

             int remainingNeeded = fieldPlayersNeeded - selectedFieldPlayers.Count;
             if (remainingNeeded > 0) {
                  if (candidates.Count >= remainingNeeded) {
                       selectedFieldPlayers.AddRange(candidates.OrderByDescending(p => p.BaseData.CurrentAbility).Take(remainingNeeded));
                       // Remove these selected players from candidates before adding rest to bench
                       var addedToLineup = selectedFieldPlayers.Skip(fieldPlayersNeeded - remainingNeeded).ToList();
                       candidates.RemoveAll(p => addedToLineup.Contains(p));
                  } else {
                       Debug.LogError($"[DefaultPlayerSetupHandler] Team {team.Name} lacks available field players ({candidates.Count}) for {remainingNeeded} slots!");
                       return false;
                  }
             }

             lineup.AddRange(selectedFieldPlayers.Take(fieldPlayersNeeded));

             // Add remaining available candidates to the bench
             bench.AddRange(candidates);

             if(lineup.Count != 7) { Debug.LogError($"[DefaultPlayerSetupHandler] Team {team.Name} lineup selection failed, count is {lineup.Count}."); return false; }

             // Set initial state for players on court / bench
             foreach (var player in lineup) { if(player != null) player.IsOnCourt = true; player.Position = Vector2.zero; }
             foreach (var player in bench) { if(player != null) player.IsOnCourt = false; player.Position = Vector2.zero; } // Mark bench players

             _eventHandler.LogEvent(state, $"Team {team.Name} lineup selected ({lineup.Count} players). Bench size: {bench.Count}", team?.TeamID ?? -1);
             return true;
        }

        public void PlacePlayersInFormation(MatchState state, List<SimPlayer> players, bool isHomeTeam, bool isKickOff)
        {
            // --- Logic copied from MatchSimulator ---
             if (players == null || state == null || _tacticPositioner == null) { // Check tactic pos too
                  Debug.LogError("[DefaultPlayerSetupHandler] Cannot place players in formation - null input detected.");
                  return;
              }
              Tactic tactic = isHomeTeam ? state.HomeTactic : state.AwayTactic; // Get correct tactic
              if (tactic == null) { Debug.LogError("[DefaultPlayerSetupHandler] Cannot place players: Tactic is null."); return; } // Null tactic check

              foreach (var player in players) {
                  if (player == null || !player.IsOnCourt || player.IsSuspended()) continue;

                  player.CurrentAction = PlayerAction.Idle; player.Velocity = Vector2.zero;
                  Vector2 basePos = player.Position;

                  try {
                       basePos = _tacticPositioner.GetPlayerTargetPosition(player, state); // Use injected TacticPositioner
                  } catch (Exception ex) {
                       Debug.LogError($"[DefaultPlayerSetupHandler] Error getting tactical pos for player {player.GetPlayerId()}: {ex.Message}. Using current position.");
                  }

                  if (isKickOff) {
                      float halfLineX = _geometry.Center.x; // Use geometry provider
                      if (isHomeTeam && basePos.x >= halfLineX) { basePos.x = halfLineX - (1f + ((player.GetPlayerId() % 5) * 0.5f)); }
                      else if (!isHomeTeam && basePos.x <= halfLineX) { basePos.x = halfLineX + (1f + ((player.GetPlayerId() % 5) * 0.5f)); }

                      if (player.IsGoalkeeper()) {
                           Vector3 goalCenter3D = isHomeTeam ? _geometry.HomeGoalCenter3D : _geometry.AwayGoalCenter3D; // Use geometry
                           basePos = new Vector2(goalCenter3D.x + (isHomeTeam ? DEF_GK_DEPTH : -DEF_GK_DEPTH), goalCenter3D.z);
                      }
                  }
                  player.Position = basePos; player.TargetPosition = player.Position;
              }
        }

         public SimPlayer FindPlayerByPosition(MatchState state, List<SimPlayer> lineup, PlayerPosition position)
         {
             // --- Logic copied from MatchSimulator ---
             if (lineup == null) return null;
             // Check BaseData null safety
             SimPlayer player = lineup.FirstOrDefault(p=> p != null && p.BaseData?.PrimaryPosition == position && p.IsOnCourt && !p.IsSuspended());
             // Check BaseData null safety
             return player ?? lineup.FirstOrDefault(p => p != null && p.BaseData?.PrimaryPosition != PlayerPosition.Goalkeeper && p.IsOnCourt && !p.IsSuspended());
         }
    }
}