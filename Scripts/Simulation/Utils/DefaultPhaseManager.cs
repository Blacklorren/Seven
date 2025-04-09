using UnityEngine;
using HandballManager.Simulation.Interfaces;
using HandballManager.Simulation.MatchData;
using HandballManager.Core;
using HandballManager.Gameplay;
using HandballManager.Simulation.Common; // For IGeometryProvider
using HandballManager.Simulation.Utils; // For CoordinateUtils
using System;
using System.Linq;
using System.Collections.Generic; // Required for List
using HandballManager.Simulation.Core; // Added for SimConstants

namespace HandballManager.Simulation.Utils // Changed from Services to Utils
{
    public class DefaultPhaseManager : IPhaseManager
    {
        private readonly IPlayerSetupHandler _playerSetupHandler;
        private readonly IMatchEventHandler _eventHandler;
        private readonly IGeometryProvider _geometry;
        private readonly TacticPositioner _tacticPositioner; // Needs TacticPositioner

        private bool _setupPending = false;

        // Constants
        private const float SET_PIECE_DEFENDER_DISTANCE = 3.0f;
        private const float SET_PIECE_DEFENDER_DISTANCE_SQ = SET_PIECE_DEFENDER_DISTANCE * SET_PIECE_DEFENDER_DISTANCE;
        private const float DEF_GK_DEPTH = 0.5f;
        private const float MIN_DISTANCE_CHECK_SQ = 0.01f;
        private const float HALF_DURATION_SECONDS = 30f * 60f;
        private const float FULL_DURATION_SECONDS = 60f * 60f;


        // Inject TacticPositioner - ideally via interface if extracted later
        public DefaultPhaseManager(IPlayerSetupHandler playerSetupHandler, IMatchEventHandler eventHandler, IGeometryProvider geometry, TacticPositioner tacticPositioner)
        {
            _playerSetupHandler = playerSetupHandler ?? throw new ArgumentNullException(nameof(playerSetupHandler));
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _tacticPositioner = tacticPositioner ?? throw new ArgumentNullException(nameof(tacticPositioner)); // Store TacticPositioner
        }

        public bool CheckAndHandleHalfTime(MatchState state, float timeBeforeStep, float timeAfterStep)
        {
             if (state != null && state.CurrentPhase != GamePhase.HalfTime &&
                 !state.HalfTimeReached &&
                 timeBeforeStep < HALF_DURATION_SECONDS && timeAfterStep >= HALF_DURATION_SECONDS)
             {
                 _eventHandler.LogEvent(state, "Half Time Reached.");
                 state.HalfTimeReached = true;
                 TransitionToPhase(state, GamePhase.HalfTime);
                 return true;
             }
             return false;
        }

        public bool CheckAndHandleFullTime(MatchState state, float timeAfterStep)
        {
              if (state != null && timeAfterStep >= FULL_DURATION_SECONDS && state.CurrentPhase != GamePhase.Finished) {
                  _eventHandler.LogEvent(state, "Full Time Reached.");
                  if (state.Ball != null && state.Ball.Position.y > SimConstants.BALL_DEFAULT_HEIGHT * 3f) {
                      _eventHandler.LogEvent(state, "Ball in air at full time - waiting for it to land.");
                      return false;
                  }
                  TransitionToPhase(state, GamePhase.Finished);
                  return true;
              }
              return false;
        }

        public void TransitionToPhase(MatchState state, GamePhase newPhase, bool forceSetup = false)
        {
             if (state == null || state.CurrentPhase == newPhase) return;

             _eventHandler.LogEvent(state, $"Phase transition: {state.CurrentPhase} -> {newPhase}"); // Log transition

             state.CurrentPhase = newPhase;

             if (forceSetup || newPhase == GamePhase.PreKickOff || newPhase == GamePhase.HomeSetPiece ||
                 newPhase == GamePhase.AwaySetPiece || newPhase == GamePhase.HomePenalty ||
                 newPhase == GamePhase.AwayPenalty || newPhase == GamePhase.HalfTime)
             {
                 _setupPending = true;
             }
        }

        public void HandlePhaseTransitions(MatchState state)
        {
              if (state == null) return;
              GamePhase phaseBeforeSetup = state.CurrentPhase;

              if (_setupPending)
              {
                  _setupPending = false;
                  if (!ExecutePhaseSetup(state, phaseBeforeSetup))
                  {
                      Debug.LogError($"[DefaultPhaseManager] Setup failed for phase {phaseBeforeSetup}. Reverting to ContestedBall.");
                      TransitionToPhase(state, GamePhase.ContestedBall);
                      _eventHandler.HandlePossessionChange(state, -1, true); // Ensure correct state
                      return;
                  }
              }
              ExecuteAutomaticPhaseTransitions(state);
        }

        private bool ExecutePhaseSetup(MatchState state, GamePhase currentPhase)
        {
             if (state == null) { Debug.LogError("[DefaultPhaseManager] Cannot execute phase setup: MatchState is null."); return false; }
             bool setupSuccess = true;
             switch (currentPhase)
             {
                 case GamePhase.PreKickOff:
                     int startingTeamId = DetermineKickoffTeam(state);
                     setupSuccess = SetupForKickOff(state, startingTeamId);
                     if (setupSuccess) _eventHandler.LogEvent(state, $"Setup for Kickoff. Team {startingTeamId} starts.", GetTeamIdFromSimId(state, startingTeamId));
                     break;
                 case GamePhase.HomeSetPiece: case GamePhase.AwaySetPiece:
                      setupSuccess = SetupForSetPiece(state);
                      if (setupSuccess) _eventHandler.LogEvent(state, $"Setup for Set Piece ({currentPhase}).");
                      break;
                 case GamePhase.HomePenalty: case GamePhase.AwayPenalty:
                      setupSuccess = SetupForPenalty(state);
                      if (setupSuccess) _eventHandler.LogEvent(state, $"Setup for Penalty ({currentPhase}).");
                      break;
                 case GamePhase.HalfTime:
                      setupSuccess = SetupForHalfTime(state);
                      if (setupSuccess) _eventHandler.LogEvent(state, "Half Time setup actions completed.");
                      break;
                 // Other cases have no setup
                 case GamePhase.KickOff: case GamePhase.HomeAttack: case GamePhase.AwayAttack:
                 case GamePhase.TransitionToHomeAttack: case GamePhase.TransitionToAwayAttack:
                 case GamePhase.ContestedBall: case GamePhase.Timeout: case GamePhase.Finished:
                      break;
                 default: Debug.LogWarning($"[DefaultPhaseManager] Unhandled phase in ExecutePhaseSetup: {currentPhase}"); break;
             }
             return setupSuccess;
        }

        private void ExecuteAutomaticPhaseTransitions(MatchState state)
        {
             if (state == null) return;
             GamePhase currentPhase = state.CurrentPhase;
             if (currentPhase == GamePhase.Finished || currentPhase == GamePhase.HalfTime || currentPhase == GamePhase.Timeout) return;

             GamePhase nextPhase = currentPhase;
             switch (currentPhase)
             {
                 case GamePhase.KickOff:
                       if(state.PossessionTeamId == 0 || state.PossessionTeamId == 1) {
                           nextPhase = (state.PossessionTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack;
                       } else { Debug.LogWarning($"[DefaultPhaseManager] Invalid possession ({state.PossessionTeamId}) after KickOff setup."); }
                      break;
                 case GamePhase.TransitionToHomeAttack: nextPhase = GamePhase.HomeAttack; break;
                 case GamePhase.TransitionToAwayAttack: nextPhase = GamePhase.AwayAttack; break;
             }
             if (nextPhase != currentPhase) { TransitionToPhase(state, nextPhase); }
        }

        public int DetermineKickoffTeam(MatchState state)
        {
              if (state.FirstHalfKickOffTeamId == -1) {
                  int startingTeamId = state.RandomGenerator.Next(0, 2);
                  state.FirstHalfKickOffTeamId = startingTeamId;
                  return startingTeamId;
              } else if (state.IsSecondHalf) {
                  return 1 - state.FirstHalfKickOffTeamId;
              } else {
                  if (state.PossessionTeamId != 0 && state.PossessionTeamId != 1) {
                      Debug.LogWarning($"[DefaultPhaseManager] Invalid PossessionTeamId ({state.PossessionTeamId}) during post-goal kickoff determination. Defaulting.");
                      return state.RandomGenerator.Next(0, 2);
                  }
                  return state.PossessionTeamId; // Team that conceded restarts
              }
        }

        public bool SetupForKickOff(MatchState state, int startingTeamId)
        {
             if (state?.Ball == null) return false;
             state.PossessionTeamId = startingTeamId;
             try {
                 _playerSetupHandler.PlacePlayersInFormation(state, state.HomePlayersOnCourt.ToList(), true, true);
                 _playerSetupHandler.PlacePlayersInFormation(state, state.AwayPlayersOnCourt.ToList(), false, true);
             } catch (Exception ex) { Debug.LogError($"Error placing players in formation for kickoff: {ex.Message}"); return false; }

             state.Ball.Stop(); state.Ball.Position = _geometry.Center;
             state.Ball.LastShooter = null; state.Ball.ResetPassContext();

             SimPlayer startingPlayer = _playerSetupHandler.FindPlayerByPosition(state, state.GetTeamOnCourt(startingTeamId), PlayerPosition.CentreBack)
                                    ?? state.GetTeamOnCourt(startingTeamId)?.FirstOrDefault(p => p != null && p.IsOnCourt && !p.IsGoalkeeper());
             if (startingPlayer == null) {
                 Debug.LogError($"Could not find starting player for kickoff for Team {startingTeamId}");
                 state.Ball.MakeLoose(_geometry.Center, Vector3.zero, -1);
                 TransitionToPhase(state, GamePhase.ContestedBall);
                 return false;
             }

              Vector3 offset = Vector3.right * (startingTeamId == 0 ? -0.1f : 0.1f);
              Vector3 playerStartPos3D = _geometry.Center + offset;
              startingPlayer.Position = CoordinateUtils.To2DGround(playerStartPos3D);
              startingPlayer.TargetPosition = startingPlayer.Position; startingPlayer.CurrentAction = PlayerAction.Idle;
              state.Ball.SetPossession(startingPlayer);

             state.CurrentPhase = GamePhase.KickOff; // Set AFTER setup

             foreach(var p in state.PlayersOnCourt) {
                  if(p != null && p != startingPlayer && !p.IsSuspended()) { p.CurrentAction = PlayerAction.Idle; }
             }
             return true;
        }

        public bool SetupForSetPiece(MatchState state)
        {
             if (state?.Ball == null) return false;
             int attackingTeamId = state.PossessionTeamId;
             int defendingTeamId = 1 - attackingTeamId;
             if (attackingTeamId == -1) {
                 Debug.LogError("Cannot setup Set Piece: PossessionTeamId is -1. Reverting.");
                 TransitionToPhase(state, GamePhase.ContestedBall); return false;
             }
             Vector3 ballPos3D = state.Ball.Position;
             Vector2 ballPos2D = CoordinateUtils.To2DGround(ballPos3D);

             SimPlayer thrower = state.GetTeamOnCourt(attackingTeamId)?
                                    .Where(p => p != null && p.IsOnCourt && !p.IsSuspended() && !p.IsGoalkeeper())
                                    .OrderBy(p => Vector2.Distance(p.Position, ballPos2D))
                                    .FirstOrDefault();
             if (thrower == null) {
                 Debug.LogError($"Cannot find thrower for Set Piece Team {attackingTeamId}. Reverting.");
                 state.Ball.MakeLoose(ballPos3D, Vector3.zero, -1);
                 TransitionToPhase(state, GamePhase.ContestedBall); return false;
             }

             state.Ball.SetPossession(thrower);
             Vector3 throwerOffset = new Vector3(0.05f, 0f, 0.05f);
             Vector3 throwerPos3D = ballPos3D + throwerOffset;
             thrower.Position = CoordinateUtils.To2DGround(throwerPos3D);
             thrower.TargetPosition = thrower.Position; thrower.CurrentAction = PlayerAction.Idle;

             // Position other players using TacticPositioner
             foreach (var player in state.PlayersOnCourt.ToList()) {
                  if (player == null || player == thrower || player.IsSuspended()) continue;
                  try {
                     Vector2 targetTacticalPos = _tacticPositioner.GetPlayerTargetPosition(player, state); // Use injected TacticPositioner
                     player.Position = targetTacticalPos;

                     if (player.TeamSimId == defendingTeamId) {
                         Vector2 vecFromBall = player.Position - ballPos2D;
                         float currentDistSq = vecFromBall.sqrMagnitude;
                         if (currentDistSq > MIN_DISTANCE_CHECK_SQ && currentDistSq < SET_PIECE_DEFENDER_DISTANCE_SQ) {
                              player.Position = ballPos2D + vecFromBall.normalized * SET_PIECE_DEFENDER_DISTANCE * 1.05f;
                         }
                     }
                     player.CurrentAction = PlayerAction.Idle; player.TargetPosition = player.Position; player.Velocity = Vector2.zero;
                  } catch (Exception ex) { Debug.LogError($"Error positioning player {player.GetPlayerId()} for set piece: {ex.Message}"); }
             }
             return true;
        }

        public bool SetupForPenalty(MatchState state)
        {
             if (state?.Ball == null) return false;
             int shootingTeamId = state.PossessionTeamId;
             int defendingTeamId = 1 - shootingTeamId;
             if (shootingTeamId == -1) {
                  Debug.LogError("Cannot setup Penalty: PossessionTeamId is -1. Reverting.");
                  TransitionToPhase(state, GamePhase.ContestedBall); return false;
             }
             bool shootingHome = (shootingTeamId == 0);

              Vector3 penaltySpot3D = shootingHome ? _geometry.AwayPenaltySpot3D : _geometry.HomePenaltySpot3D;
              state.Ball.Stop(); state.Ball.Position = penaltySpot3D; state.Ball.Holder = null;

               SimPlayer shooter = state.GetTeamOnCourt(shootingTeamId)?
                                   .Where(p => p != null && p.IsOnCourt && !p.IsSuspended() && !p.IsGoalkeeper())
                                   .OrderByDescending(p => p.BaseData?.ShootingAccuracy ?? 0)
                                   .FirstOrDefault();
              if (shooter == null) {
                  Debug.LogError($"Cannot find penalty shooter for Team {shootingTeamId}. Reverting.");
                  state.Ball.MakeLoose(penaltySpot3D, Vector3.zero, -1);
                  TransitionToPhase(state, GamePhase.ContestedBall); return false;
              }

              Vector2 penaltySpot2D = CoordinateUtils.To2DGround(penaltySpot3D);
              shooter.Position = penaltySpot2D + Vector2.right * (shootingHome ? -0.2f : 0.2f);
              shooter.TargetPosition = shooter.Position; shooter.CurrentAction = PlayerAction.PreparingShot; shooter.ActionTimer = 1.0f;

              SimPlayer gk = state.GetGoalkeeper(defendingTeamId);
              if (gk != null) {
                   Vector3 goalCenter3D = defendingTeamId == 0 ? _geometry.HomeGoalCenter3D : _geometry.AwayGoalCenter3D;
                   Vector2 gkPos2D = new Vector2(goalCenter3D.x + (defendingTeamId == 0 ? DEF_GK_DEPTH : -DEF_GK_DEPTH), goalCenter3D.z);
                   gk.Position = gkPos2D;
                   gk.TargetPosition = gk.Position; gk.CurrentAction = PlayerAction.GoalkeeperPositioning;
              } else { Debug.LogWarning($"No Goalkeeper found for defending team {defendingTeamId} during penalty setup."); }

              Vector2 opponentGoalCenter2D = shootingHome ? CoordinateUtils.To2DGround(_geometry.AwayGoalCenter3D) : CoordinateUtils.To2DGround(_geometry.HomeGoalCenter3D);
              float freeThrowLineX = opponentGoalCenter2D.x + (shootingHome ? -_geometry.FreeThrowLineRadius : _geometry.FreeThrowLineRadius);

              foreach (var player in state.PlayersOnCourt.ToList()) {
                   if (player == null || player == shooter || player == gk || player.IsSuspended()) continue;
                   try {
                      player.Position = _tacticPositioner.GetPlayerTargetPosition(player, state); // Use injected TacticPositioner
                      if ((shootingHome && player.Position.x >= freeThrowLineX) || (!shootingHome && player.Position.x <= freeThrowLineX)) {
                           float offsetX = shootingHome ? -(0.5f + (player.GetPlayerId() % 5) * 0.5f) : (0.5f + (player.GetPlayerId() % 5) * 0.5f);
                           player.Position.x = freeThrowLineX + offsetX;
                      }
                      player.CurrentAction = PlayerAction.Idle; player.TargetPosition = player.Position; player.Velocity = Vector2.zero;
                   } catch (Exception ex) { Debug.LogError($"Error positioning player {player.GetPlayerId()} for penalty: {ex.Message}"); }
              }
              return true;
        }

        public bool SetupForHalfTime(MatchState state)
        {
             if (state == null) return false;
             foreach (var player in state.AllPlayers.Values) {
                 if (player?.BaseData == null) continue;
                 try {
                    float recoveryAmount = (1.0f - player.Stamina) * 0.4f;
                    recoveryAmount *= Mathf.Lerp(0.8f, 1.2f, (player.BaseData.NaturalFitness > 0 ? player.BaseData.NaturalFitness : 50f) / 100f);
                    player.Stamina = Mathf.Clamp01(player.Stamina + recoveryAmount);
                    player.UpdateEffectiveSpeed();
                 } catch (Exception ex) { Debug.LogError($"Error recovering stamina for player {player.GetPlayerId()}: {ex.Message}"); }
             }
             state.IsSecondHalf = true;
             TransitionToPhase(state, GamePhase.PreKickOff);
             return true;
        }

        private int? GetTeamIdFromSimId(MatchState state, int simId)
        {
            if (state == null) return null;
            if (simId == 0) return state.HomeTeamData?.TeamID;
            if (simId == 1) return state.AwayTeamData?.TeamID;
            return null;
        }
    }
}