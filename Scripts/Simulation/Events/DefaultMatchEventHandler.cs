using UnityEngine;
using HandballManager.Simulation.Interfaces;
using HandballManager.Simulation.Common;
using HandballManager.Simulation.MatchData;
using HandballManager.Data;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Core;
using System;
using System.Linq;
using System.Collections.Generic;
using HandballManager.Core;

namespace HandballManager.Simulation.Events
{
    public class DefaultMatchEventHandler : IMatchEventHandler
    {
        private readonly IGeometryProvider _geometry;
        private IPhaseManager _phaseManager; // Needs PhaseManager for transitions

        // Constants
        private const float DEFAULT_SUSPENSION_TIME = 120f;
        private const float RED_CARD_SUSPENSION_TIME = float.MaxValue;
        private const float BLOCK_REBOUND_MIN_SPEED_FACTOR = 0.2f;
        private const float BLOCK_REBOUND_MAX_SPEED_FACTOR = 0.5f;
        private const float OOB_RESTART_BUFFER = 0.1f;
        private const float GOAL_THROW_RESTART_DIST = 6.0f + 0.2f; // Use constant
        private const float MIN_DISTANCE_CHECK_SQ = 0.01f;

        public DefaultMatchEventHandler(IGeometryProvider geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            // PhaseManager will be injected via setter to avoid circular dependency in constructor
        }

        // Setter injection for PhaseManager
        public void SetPhaseManager(IPhaseManager phaseManager)
        {
            _phaseManager = phaseManager ?? throw new ArgumentNullException(nameof(phaseManager));
        }


        public void HandleActionResult(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
             if (state == null) { Debug.LogError("[DefaultMatchEventHandler] HandleActionResult called with null MatchState."); return; }
             if (_phaseManager == null) { Debug.LogError("[DefaultMatchEventHandler] PhaseManager is not set."); return; } // Check if injected

             switch (result.Outcome) {
                 case ActionResultOutcome.Success: HandleActionSuccess(result, state); break;
                 case ActionResultOutcome.Failure: HandleActionFailure(result, state); break;
                 case ActionResultOutcome.Intercepted: HandleInterception(result, state); break;
                 case ActionResultOutcome.Saved: HandleSave(result, state); break;
                 case ActionResultOutcome.Blocked: HandleBlock(result, state); break;
                 case ActionResultOutcome.Goal: HandleGoalScored(result, state); break;
                 case ActionResultOutcome.Miss: HandleMiss(result, state); break;
                 case ActionResultOutcome.FoulCommitted: HandleFoul(result, state); break;
                 case ActionResultOutcome.OutOfBounds: HandleOutOfBounds(result, state); break;
                 case ActionResultOutcome.Turnover: HandleTurnover(result, state); break;
                 default: Debug.LogWarning($"[DefaultMatchEventHandler] Unhandled ActionResult Outcome: {result.Outcome}"); break;
             }
        }

        public void ResetPlayerActionState(SimPlayer player, ActionResultOutcome outcomeContext = ActionResultOutcome.Success)
        {
             // --- Logic copied from MatchSimulator ---
              if (player == null) return;
              if (player.CurrentAction == PlayerAction.Suspended) return;

              // Persist receiving state unless pass was intercepted or outcome dictates reset
              // This logic might be better handled by AI re-evaluating
              // if (player.CurrentAction == PlayerAction.ReceivingPass && outcomeContext != ActionResultOutcome.Intercepted) { }

              player.CurrentAction = PlayerAction.Idle;
              player.ActionTimer = 0f;
              player.TargetPlayer = null;
              // Optionally reset TargetPosition: player.TargetPosition = player.Position;
        }

        public void HandlePossessionChange(MatchState state, int newPossessionTeamId, bool ballIsLoose = false)
        {
            // --- Logic copied from MatchSimulator ---
             if (state == null) return;
             if (_phaseManager == null) { Debug.LogError("[DefaultMatchEventHandler] PhaseManager is not set for HandlePossessionChange."); return; }

             int previousPossessionTeamId = state.PossessionTeamId;
             bool possessionTrulyChanged = previousPossessionTeamId != newPossessionTeamId && previousPossessionTeamId != -1 && newPossessionTeamId != -1;

             state.PossessionTeamId = newPossessionTeamId;

             GamePhase nextPhase = state.CurrentPhase;

             if (ballIsLoose || newPossessionTeamId == -1) {
                  nextPhase = GamePhase.ContestedBall;
                  // Log("Possession contested (Loose Ball).", state); // Internal log call
             } else if (possessionTrulyChanged) {
                  nextPhase = (newPossessionTeamId == 0) ? GamePhase.TransitionToHomeAttack : GamePhase.TransitionToAwayAttack;
                  // Log($"Possession changes to Team {newPossessionTeamId}.", state, GetTeamIdFromSimId(state, newPossessionTeamId));
             } else if (newPossessionTeamId != -1 && previousPossessionTeamId == -1) {
                  nextPhase = (newPossessionTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack;
                  // Log($"Team {newPossessionTeamId} gained possession.", state, GetTeamIdFromSimId(state, newPossessionTeamId));
             }

             if (ballIsLoose || newPossessionTeamId == -1) {
                  if(state.Ball?.Holder != null) { state.Ball.Holder.HasBall = false; state.Ball.Holder = null; }
                  if (nextPhase != GamePhase.ContestedBall) { nextPhase = GamePhase.ContestedBall; }
             } else if (newPossessionTeamId != -1 && state.Ball?.Holder != null) {
                  if (nextPhase == GamePhase.ContestedBall || nextPhase == GamePhase.TransitionToHomeAttack || nextPhase == GamePhase.TransitionToAwayAttack) {
                       nextPhase = (newPossessionTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack;
                  }
             }

             _phaseManager.TransitionToPhase(state, nextPhase); // Use injected PhaseManager
        }

        public void LogEvent(MatchState state, string description, int? teamId = null, int? playerId = null)
        {
            // --- Logic copied from MatchSimulator ---
            if (state?.MatchEvents == null) return;
            try {
                // Optional: Add timestamp formatting if needed, MatchEvent constructor handles time
                state.MatchEvents.Add(new MatchEvent(state.MatchTimeSeconds, description, teamId, playerId));
                // Debug.Log($"[Sim {(int)state.MatchTimeSeconds}s] {description}"); // Optional console log
            } catch (Exception ex) { Debug.LogWarning($"[DefaultMatchEventHandler] Failed to add MatchEvent: {ex.Message}"); }
        }

        public void HandleStepError(MatchState state, string stepName, Exception ex)
        {
            // --- Logic copied from MatchSimulator ---
             float currentTime = state?.MatchTimeSeconds ?? -1f;
             Debug.LogError($"[DefaultMatchEventHandler] Error during '{stepName}' at Time {currentTime:F1}s: {ex?.Message ?? "Unknown Error"}\n{ex?.StackTrace ?? "No stack trace"}");
             if (state != null) {
                 // Force the simulation to end immediately
                 _phaseManager?.TransitionToPhase(state, GamePhase.Finished); // Use PhaseManager if available
             }
             LogEvent(state, $"CRITICAL ERROR during {stepName} - Simulation aborted. Error: {ex?.Message ?? "Unknown Error"}");
        }

        // --- Specific Event Handler Implementations (Copied & Adapted) ---

        private void HandleActionSuccess(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
            SimPlayer primary = result.PrimaryPlayer;
            SimPlayer secondary = result.SecondaryPlayer;
            switch (result.Reason) {
                case "Pass Released": if (primary != null) { LogEvent(state, $"Pass released from {primary.BaseData?.Name ?? "Unknown"} towards {secondary?.BaseData?.Name ?? "target"}.", primary.GetTeamId(), primary.GetPlayerId()); ResetPlayerActionState(primary, result.Outcome); } break;
                case "Tackle Won Ball": case "Tackle Successful (No Ball)": if (primary != null && secondary != null) { LogEvent(state, $"Tackle by {primary.BaseData?.Name ?? "Unknown"} on {secondary.BaseData?.Name ?? "Unknown"} successful!", primary.GetTeamId(), primary.GetPlayerId()); HandlePossessionChange(state, -1, true); ResetPlayerActionState(primary, result.Outcome); ResetPlayerActionState(secondary, result.Outcome); } break;
                case "Shot Taken": if (primary != null) { LogEvent(state, $"Shot taken by {primary.BaseData?.Name ?? "Unknown"}.", primary.GetTeamId(), primary.GetPlayerId()); IncrementStat(state, primary, stats => stats.ShotsTaken++); ResetPlayerActionState(primary, result.Outcome); } break;
                case "Picked up loose ball": if (primary != null) { LogEvent(state, $"Player {primary.BaseData?.Name ?? "Unknown"} picked up loose ball.", primary.GetTeamId(), primary.GetPlayerId()); } break; // State handled elsewhere
                case "Penalty Shot Taken": if (primary != null) { LogEvent(state, $"Penalty shot taken by {primary.BaseData?.Name ?? "Unknown"}.", primary.GetTeamId(), primary.GetPlayerId()); IncrementStat(state, primary, stats => stats.ShotsTaken++); ResetPlayerActionState(primary, result.Outcome); } break;
                default: if (primary != null) LogEvent(state, $"Action successful for {primary.BaseData?.Name ?? "Unknown"}.", primary.GetTeamId(), primary.GetPlayerId()); else LogEvent(state, $"Action successful."); if (primary != null) ResetPlayerActionState(primary, result.Outcome); break;
            }
        }

        private void HandleActionFailure(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
            SimPlayer primary = result.PrimaryPlayer;
            SimPlayer secondary = result.SecondaryPlayer;
            string reason = result.Reason ?? "Unknown Reason";
            if (reason == "Tackle Evaded" && primary != null) { LogEvent(state, $"Tackle by {primary.BaseData?.Name ?? "Unknown"} evaded by {secondary?.BaseData?.Name ?? "opponent"}.", primary.GetTeamId(), primary.GetPlayerId()); ResetPlayerActionState(primary, result.Outcome); }
            else if (primary != null) { LogEvent(state, $"{primary.BaseData?.Name ?? "Unknown"}'s action failed: {reason}.", primary.GetTeamId(), primary.GetPlayerId()); ResetPlayerActionState(primary, result.Outcome); if (reason.Contains("Pass Target") || reason.Contains("Pass Inaccurate")) { IncrementStat(state, primary, stats => stats.Turnovers++); HandlePossessionChange(state, -1, true); } }
            else { LogEvent(state, $"Action failed: {reason}."); HandlePossessionChange(state, -1, true); }
        }

        private void HandleTurnover(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
            SimPlayer player = result.PrimaryPlayer;
            string reason = result.Reason ?? "Unknown";
            if (player != null) { LogEvent(state, $"Turnover by {player.BaseData?.Name ?? "Unknown"}. Reason: {reason}", player.GetTeamId(), player.GetPlayerId()); IncrementStat(state, player, stats => stats.Turnovers++); HandlePossessionChange(state, -1, true); ResetPlayerActionState(player, result.Outcome); }
            else { LogEvent(state, $"Turnover occurred. Reason: {reason}"); HandlePossessionChange(state, -1, true); }
        }

        private void HandleInterception(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
             SimPlayer interceptor = result.PrimaryPlayer; SimPlayer passer = result.SecondaryPlayer;
             if (interceptor?.BaseData == null) { Debug.LogError("[DefaultMatchEventHandler] Interception handled with null interceptor."); HandlePossessionChange(state, -1, true); return; }
             LogEvent(state, $"Pass from {passer?.BaseData?.Name ?? "Unknown"} intercepted by {interceptor.BaseData.Name}!", interceptor.GetTeamId(), interceptor.GetPlayerId());
             if (passer != null) IncrementStat(state, passer, stats => stats.Turnovers++);
             IncrementStat(state, interceptor, stats => stats.Interceptions++);
             state.Ball.SetPossession(interceptor);
             HandlePossessionChange(state, interceptor.TeamSimId);
             ResetPlayerActionState(interceptor, result.Outcome);
             if (passer != null) ResetPlayerActionState(passer, result.Outcome);
             if (state.Ball.IntendedTarget != null) ResetPlayerActionState(state.Ball.IntendedTarget, result.Outcome);
             state.Ball.ResetPassContext();
        }

        private void HandleSave(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
            SimPlayer gk = result.PrimaryPlayer; SimPlayer shooter = result.SecondaryPlayer;
            if (gk?.BaseData == null) { Debug.LogError("[DefaultMatchEventHandler] Save handled with null Goalkeeper."); HandlePossessionChange(state, -1, true); if (shooter != null) ResetPlayerActionState(shooter, result.Outcome); return; }
            LogEvent(state, $"Shot by {shooter?.BaseData?.Name ?? "Unknown"} saved by {gk.BaseData.Name}!", gk.GetTeamId(), gk.GetPlayerId());
            IncrementStat(state, gk, stats => stats.SavesMade++);
            if (shooter != null) IncrementStat(state, shooter, stats => stats.ShotsOnTarget++);
            state.Ball.SetPossession(gk);
            HandlePossessionChange(state, gk.TeamSimId);
            ResetPlayerActionState(gk, result.Outcome);
            if (shooter != null) ResetPlayerActionState(shooter, result.Outcome);
            state.Ball.LastShooter = null;
        }

        private void HandleBlock(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
            SimPlayer blocker = result.PrimaryPlayer; SimPlayer shooter = result.SecondaryPlayer;
            Vector2 impactPos = result.ImpactPosition ?? blocker?.Position ?? CoordinateUtils.To2DGround(state.Ball.Position);
            if (blocker?.BaseData == null) { Debug.LogError("[DefaultMatchEventHandler] Block handled with null Blocker."); state.Ball.MakeLoose(CoordinateUtils.To3DGround(impactPos), Vector3.zero, -1); HandlePossessionChange(state, -1, true); if (shooter != null) ResetPlayerActionState(shooter, result.Outcome); return; }
            LogEvent(state, $"Shot by {shooter?.BaseData?.Name ?? "Unknown"} blocked by {blocker.BaseData.Name}!", blocker.GetTeamId(), blocker.GetPlayerId());
            IncrementStat(state, blocker, stats => stats.BlocksMade++);
            Vector2 reboundDir = (impactPos - (shooter?.Position ?? impactPos)).normalized;
            if (reboundDir == Vector2.zero) reboundDir = (Vector2.right * (blocker.TeamSimId == 0 ? -1f : 1f));
            Vector2 randomOffset = new Vector2((float)state.RandomGenerator.NextDouble() - 0.5f, (float)state.RandomGenerator.NextDouble() - 0.5f) * 0.8f;
            Vector2 finalReboundDir = (reboundDir + randomOffset).normalized;
            float reboundSpeed = SimConstants.BALL_DEFAULT_HEIGHT * (float)state.RandomGenerator.NextDouble() * (BLOCK_REBOUND_MAX_SPEED_FACTOR - BLOCK_REBOUND_MIN_SPEED_FACTOR) + BLOCK_REBOUND_MIN_SPEED_FACTOR; // ERROR: Was using SHOT_BASE_SPEED, likely meant something smaller
            reboundSpeed = Mathf.Clamp(reboundSpeed, 1f, 5f); // Clamp rebound speed reasonably
            state.Ball.MakeLoose(CoordinateUtils.To3DGround(impactPos), CoordinateUtils.To3DGround(finalReboundDir * reboundSpeed, 0f) , blocker.TeamSimId, blocker); // Convert vel to 3D
            HandlePossessionChange(state, -1, true);
            ResetPlayerActionState(blocker, result.Outcome);
            if (shooter != null) ResetPlayerActionState(shooter, result.Outcome);
        }

        private void HandleGoalScored(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
            SimPlayer scorer = result.PrimaryPlayer;
            if (scorer?.BaseData == null) { LogEvent(state, "Goal registered with invalid scorer data!"); _phaseManager?.TransitionToPhase(state, GamePhase.Finished); return; } // Use PhaseManager
            int scoringTeamId = scorer.TeamSimId;
            bool wasPenalty = (state.CurrentPhase == GamePhase.HomePenalty || state.CurrentPhase == GamePhase.AwayPenalty);
            if (scoringTeamId == 0) state.HomeScore++; else state.AwayScore++;
            IncrementStat(state, scorer, stats => stats.GoalsScored++);
            IncrementStat(state, scorer, stats => stats.ShotsOnTarget++);
            if (wasPenalty) { IncrementStat(state, scorer, stats => stats.PenaltiesScored++); }
            LogEvent(state, $"GOAL! Scored by {scorer.BaseData.Name}. Score: {state.HomeTeamData?.Name ?? "Home"} {state.HomeScore} - {state.AwayScore} {state.AwayTeamData?.Name ?? "Away"}", scorer.GetTeamId(), scorer.GetPlayerId());
            state.Ball.Stop(); state.Ball.Position = _geometry.Center; // Use Geometry
            int kickoffTeamId = 1 - scoringTeamId;
            HandlePossessionChange(state, kickoffTeamId); // Ensure possession is set correctly BEFORE phase transition
            _phaseManager?.TransitionToPhase(state, GamePhase.PreKickOff); // Use PhaseManager
             foreach(var p in state.PlayersOnCourt.ToList()) { if (p != null && !p.IsSuspended()) ResetPlayerActionState(p, result.Outcome); }
        }

        private void HandleMiss(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
            SimPlayer shooter = result.PrimaryPlayer;
            if (shooter?.BaseData != null) { LogEvent(state, $"Shot by {shooter.BaseData.Name} missed target.", shooter.GetTeamId(), shooter.GetPlayerId()); ResetPlayerActionState(shooter, result.Outcome); }
            else { LogEvent(state, $"Shot missed target."); }
            state.Ball.LastShooter = null;
        }

        private void HandleFoul(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
             SimPlayer committer = result.PrimaryPlayer; SimPlayer victim = result.SecondaryPlayer; FoulSeverity severity = result.FoulSeverity;
             if (committer?.BaseData == null || victim?.BaseData == null) { Debug.LogError($"[DefaultMatchEventHandler] Foul event missing valid committer/victim!"); HandlePossessionChange(state, -1, true); return; }
             LogEvent(state, $"Foul by {committer.BaseData.Name} on {victim.BaseData.Name} ({severity}).", committer.GetTeamId(), committer.GetPlayerId());
             IncrementStat(state, committer, stats => stats.FoulsCommitted++);
             bool applySuspension = severity == FoulSeverity.TwoMinuteSuspension || severity == FoulSeverity.RedCard;
             if (applySuspension) {
                 if (severity == FoulSeverity.TwoMinuteSuspension) IncrementStat(state, committer, stats => stats.TwoMinuteSuspensions++);
                 if (severity == FoulSeverity.RedCard) IncrementStat(state, committer, stats => stats.RedCards++);
                 committer.SuspensionTimer = (severity == FoulSeverity.RedCard) ? RED_CARD_SUSPENSION_TIME : DEFAULT_SUSPENSION_TIME;
                 committer.CurrentAction = PlayerAction.Suspended; committer.IsOnCourt = false;
                 var teamList = state.GetTeamOnCourt(committer.TeamSimId);
                 if (teamList != null && teamList.Contains(committer)) { teamList.Remove(committer); }
                 else { Debug.LogWarning($"[DefaultMatchEventHandler] Suspended player {committer.GetPlayerId()} not found in team's on-court list."); }
                 committer.Position = new Vector2(-100, -100); committer.Velocity = Vector2.zero;
                 if(committer.HasBall) committer.HasBall = false;
                 LogEvent(state, $"{committer.BaseData.Name} receives {severity}.", committer.GetTeamId(), committer.GetPlayerId());
             }

             state.Ball.Stop();
             if (state.Ball.Holder != null) { if(state.Ball.Holder.HasBall) state.Ball.Holder.HasBall = false; state.Ball.Holder = null; }
             Vector2 foulLocation = result.ImpactPosition ?? victim.Position;
             state.Ball.Position = CoordinateUtils.To3DGround(foulLocation); // Set 3D ball pos

             int victimTeamId = victim.TeamSimId;
             HandlePossessionChange(state, victimTeamId);

             bool isPenaltyAreaFoul = _geometry.IsInGoalArea(foulLocation, victimTeamId == 1); // Use Geometry
             bool deniedClearChance = severity == FoulSeverity.RedCard || severity == FoulSeverity.TwoMinuteSuspension;

             if (isPenaltyAreaFoul && deniedClearChance && severity != FoulSeverity.OffensiveFoul) {
                 IncrementStat(state, victimTeamId, stats => stats.PenaltiesAwarded++);
                 GamePhase nextPhase = (victimTeamId == 0) ? GamePhase.HomePenalty : GamePhase.AwayPenalty;
                 LogEvent(state, $"7m Penalty awarded to Team {victimTeamId}.", GetTeamIdFromSimId(state, victimTeamId));
                 _phaseManager?.TransitionToPhase(state, nextPhase); // Use PhaseManager
             } else {
                 Vector2 opponentGoalCenter = _geometry.GetOpponentGoalCenter(victimTeamId); // Use Geometry
                 if (Vector2.Distance(foulLocation, opponentGoalCenter) < _geometry.FreeThrowLineRadius) {
                     Vector2 directionFromGoal = (foulLocation - opponentGoalCenter);
                     if (directionFromGoal.sqrMagnitude < MIN_DISTANCE_CHECK_SQ) directionFromGoal = Vector2.right * (victimTeamId == 0 ? -1f : 1f);
                     foulLocation = opponentGoalCenter + directionFromGoal.normalized * _geometry.FreeThrowLineRadius; // Recalculate 2D location
                     state.Ball.Position = CoordinateUtils.To3DGround(foulLocation); // Update 3D ball pos
                 }
                 GamePhase nextPhase = (victimTeamId == 0) ? GamePhase.HomeSetPiece : GamePhase.AwaySetPiece;
                 LogEvent(state, $"Free throw awarded to Team {victimTeamId}.", GetTeamIdFromSimId(state, victimTeamId));
                 _phaseManager?.TransitionToPhase(state, nextPhase); // Use PhaseManager
             }
             ResetPlayerActionState(committer, result.Outcome); ResetPlayerActionState(victim, result.Outcome);
        }


        public void HandleOutOfBounds(ActionResult result, MatchState state, Vector3? intersectionPoint3D = null)
        {
             // --- Logic copied from MatchSimulator ---
             if (state?.Ball == null) { return; }
             int lastTouchTeamId = state.Ball.LastTouchedByTeamId;
             int receivingTeamId;
             if (lastTouchTeamId == 0 || lastTouchTeamId == 1) { receivingTeamId = 1 - lastTouchTeamId; }
             else { receivingTeamId = (state.Ball.Position.x < _geometry.Center.x) ? 1 : 0; LogEvent(state, "Unknown last touch for OOB."); }

             Vector3 restartPosition3D = intersectionPoint3D ?? state.Ball.Position;
             RestartInfo restart = DetermineRestartTypeAndPosition(state, restartPosition3D, lastTouchTeamId, receivingTeamId);
             receivingTeamId = restart.ReceivingTeamId;

             LogEvent(state, $"Ball out of bounds ({restart.Type}). Possession to Team {receivingTeamId}.", GetTeamIdFromSimId(state, receivingTeamId));

             HandlePossessionChange(state, receivingTeamId);
             state.Ball.Stop();
             state.Ball.Position = restart.Position;

             SimPlayer thrower = FindThrower(state, receivingTeamId, restart.Position, restart.IsGoalThrow);

             if (thrower != null) {
                 state.Ball.SetPossession(thrower);
                 ResetPlayerActionState(thrower);
                 GamePhase nextPhase = restart.IsGoalThrow ? ((receivingTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack) : ((receivingTeamId == 0) ? GamePhase.HomeSetPiece : GamePhase.AwaySetPiece);
                 _phaseManager?.TransitionToPhase(state, nextPhase); // Use PhaseManager
             } else {
                 Debug.LogWarning($"No thrower found for {restart.Type} for Team {receivingTeamId} at {state.Ball.Position}");
                 state.Ball.MakeLoose(state.Ball.Position, Vector3.zero, receivingTeamId);
                 HandlePossessionChange(state, -1, true);
             }
        }

        // --- Helper Methods (Copied & Adapted) ---

        private void IncrementStat(MatchState state, SimPlayer player, Action<TeamMatchStats> updateAction)
        {
             if (player?.BaseData == null || state == null || updateAction == null) return;
             TeamMatchStats stats = (player.TeamSimId == 0) ? state.CurrentHomeStats : state.CurrentAwayStats;
             if (stats != null) { updateAction(stats); }
             else { Debug.LogWarning($"[DefaultMatchEventHandler] Could not find stats object for TeamSimId {player.TeamSimId} to increment stat."); }
        }
        private void IncrementStat(MatchState state, int teamSimId, Action<TeamMatchStats> updateAction)
        {
              if (state == null || updateAction == null || (teamSimId != 0 && teamSimId != 1)) return;
              TeamMatchStats stats = (teamSimId == 0) ? state.CurrentHomeStats : state.CurrentAwayStats;
              if (stats != null) { updateAction(stats); }
              else { Debug.LogWarning($"[DefaultMatchEventHandler] Could not find stats object for TeamSimId {teamSimId} to increment stat."); }
        }

         private struct RestartInfo { public string Type; public Vector3 Position; public bool IsGoalThrow; public int ReceivingTeamId; }

         private RestartInfo DetermineRestartTypeAndPosition(MatchState state, Vector3 oobPos3D, int lastTouchTeamId, int initialReceivingTeamId)
         {
             // --- Logic copied from MatchSimulator ---
             // Use _geometry provider
             string restartType = "Throw-in"; Vector3 restartPos3D = oobPos3D; bool isGoalThrow = false; int finalReceivingTeamId = initialReceivingTeamId;
             float oobPosX = oobPos3D.x; float oobPosZ = oobPos3D.z;
             bool wentOutHomeGoalLine = oobPosX <= OOB_RESTART_BUFFER; bool wentOutAwayGoalLine = oobPosX >= _geometry.PitchLength - OOB_RESTART_BUFFER;

             if (wentOutHomeGoalLine) { if (lastTouchTeamId == 1) { isGoalThrow = true; restartType = "Goal throw"; finalReceivingTeamId = 0; } }
             else if (wentOutAwayGoalLine) { if (lastTouchTeamId == 0) { isGoalThrow = true; restartType = "Goal throw"; finalReceivingTeamId = 1; } }

             if (isGoalThrow) {
                 SimPlayer gk = state.GetGoalkeeper(finalReceivingTeamId);
                 Vector3 goalCenter = (finalReceivingTeamId == 0) ? _geometry.HomeGoalCenter3D : _geometry.AwayGoalCenter3D;
                 Vector3 dir3D = Vector3.zero;
                 if(gk != null) { dir3D = new Vector3(gk.Position.x - goalCenter.x, 0f, gk.Position.y - goalCenter.z); } // Use Y from 2D for Z in 3D
                 if(dir3D.sqrMagnitude < SimConstants.VELOCITY_NEAR_ZERO_SQ) { dir3D = new Vector3((finalReceivingTeamId == 0 ? 1f : -1f), 0f, 0f); }
                 restartPos3D = goalCenter + dir3D.normalized * GOAL_THROW_RESTART_DIST;
                 restartPos3D.y = SimConstants.BALL_DEFAULT_HEIGHT;
             } else {
                  restartPos3D.z = Mathf.Clamp(restartPos3D.z, OOB_RESTART_BUFFER, _geometry.PitchWidth - OOB_RESTART_BUFFER);
                  if (oobPosZ <= OOB_RESTART_BUFFER) restartPos3D.z = OOB_RESTART_BUFFER;
                  else if (oobPosZ >= _geometry.PitchWidth - OOB_RESTART_BUFFER) restartPos3D.z = _geometry.PitchWidth - OOB_RESTART_BUFFER;
                  if (!wentOutHomeGoalLine && !wentOutAwayGoalLine) { restartPos3D.x = Mathf.Clamp(restartPos3D.x, OOB_RESTART_BUFFER, _geometry.PitchLength - OOB_RESTART_BUFFER); }
                  restartPos3D.y = SimConstants.BALL_DEFAULT_HEIGHT;
             }
             restartPos3D.x = Mathf.Clamp(restartPos3D.x, 0f, _geometry.PitchLength); restartPos3D.z = Mathf.Clamp(restartPos3D.z, 0f, _geometry.PitchWidth);
             restartPos3D.y = SimConstants.BALL_DEFAULT_HEIGHT;
             return new RestartInfo { Type = restartType, Position = restartPos3D, IsGoalThrow = isGoalThrow, ReceivingTeamId = finalReceivingTeamId };
         }

         private SimPlayer FindThrower(MatchState state, int receivingTeamId, Vector3 restartPos3D, bool isGoalThrow)
         {
             // --- Logic copied from MatchSimulator ---
              if (isGoalThrow) { return state.GetGoalkeeper(receivingTeamId); }
              else {
                  Vector2 restartPos2D = CoordinateUtils.To2DGround(restartPos3D);
                  return state.GetTeamOnCourt(receivingTeamId)?
                                .Where(p => p != null && p.IsOnCourt && !p.IsSuspended() && !p.IsGoalkeeper())
                                .OrderBy(p => Vector2.Distance(p.Position, restartPos2D))
                                .FirstOrDefault();
              }
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