using HandballManager.Data;
using HandballManager.Core; // For Enums, GameManager access
using HandballManager.Management; // For LeagueManager access (placeholder)
using UnityEngine; // For Mathf.Clamp, Debug.Log, Random
using System; // For Math, DateTime
using System.Collections.Generic; // For List, if tracking form
using System.Linq; // For Linq, if tracking form

namespace HandballManager.Simulation
{
    /// <summary>
    /// Simulates changes in player morale based on various factors like match results,
    /// team performance, contract status, and individual events.
    /// </summary>
    public class MoraleSimulator
    {
        // --- Constants for Morale Adjustments ---
        // Match Result Effects (Base Values)
        private const float WIN_MORALE_BOOST_BASE = 0.05f;
        private const float DRAW_MORALE_CHANGE = 0.0f;
        private const float LOSS_MORALE_DROP_BASE = -0.06f; // Slightly reduced base drop

        // Modifiers based on match importance/expectation
        private const float EXPECTED_WIN_MOD = 0.7f;    // Less boost for expected win
        private const float UNEXPECTED_WIN_MOD = 1.5f;  // Bigger boost for upset win
        private const float EXPECTED_LOSS_MOD = 0.6f;   // Less drop for expected loss
        private const float UNEXPECTED_LOSS_MOD = 1.6f; // Bigger drop for upset loss
        private const float DRAW_VS_EXPECTED_WIN_DROP = -0.02f;
        private const float DRAW_VS_EXPECTED_LOSS_BOOST = 0.01f;

        // Participation Modifier
        private const float NON_PARTICIPANT_MOD = 0.25f; // Player didn't play (e.g., injured), gets reduced effect

        // Weekly Effects
        private const float WEEKLY_MORALE_DECAY = -0.005f; // Slight regression towards neutral (0.5)
        private const float GOOD_TRAINING_WEEK_BOOST = 0.01f; // Placeholder value
        private const float POOR_TRAINING_WEEK_DROP = -0.01f; // Placeholder value
        private const float TOP_3_LEAGUE_BOOST = 0.015f;
        private const float BOTTOM_3_LEAGUE_DROP = -0.02f;
        private const float RECENT_FORM_MOD_MAX = 0.02f; // Max boost/drop from recent form

        // Contract/Transfer Effects (Weekly Check)
        private const float LOW_MORALE_EXPIRING_CONTRACT_DROP = -0.015f;
        private const float UNHAPPY_TRANSFER_LISTED_DROP = -0.01f;

        // Event Effects (One-off)
        private const float CONTRACT_RENEWED_BOOST = 0.15f;
        private const float CONTRACT_REJECTED_DROP = -0.05f; // Player rejected team's offer
        private const float TRANSFER_LISTED_DROP = -0.10f;
        private const float TRANSFER_REQUEST_APPROVED_BOOST = 0.05f;
        private const float TRANSFER_REQUEST_DENIED_DROP = -0.15f;
        private const float TRANSFER_COMPLETED_IN_BOOST = 0.08f; // New signing boost
        private const float TRANSFER_COMPLETED_OUT_DROP = -0.06f; // Teammate leaving drop (applied to remaining team)
        private const float MANAGER_PRAISE_BOOST = 0.03f;
        private const float MANAGER_CRITICISM_DROP = -0.04f;
        private const float INJURY_MINOR_DROP = -0.03f;
        private const float INJURY_MODERATE_DROP = -0.07f;
        private const float INJURY_MAJOR_DROP = -0.15f;
        private const float RECOVERED_FROM_INJURY_BOOST = 0.05f;


        /// <summary>
        /// Updates morale for players in a team after a match result.
        /// Considers expectations, result, and player participation.
        /// </summary>
        /// <param name="team">The team whose players' morale to update.</param>
        /// <param name="result">The result of the match the team participated in.</param>
        public void UpdateMoralePostMatch(TeamData team, MatchResult result)
        {
            if (team == null || team.Roster == null || result == null) return;

            // Determine actual outcome for this team
            MatchOutcome outcome = result.GetOutcomeForTeam(team.TeamID);

            // Determine Expected Outcome (Simple version based on Reputation)
            TeamData opponent = GetOpponentTeamData(result, team.TeamID); // Helper to get opponent data
            MatchOutcome expectedOutcome = PredictOutcome(team, opponent);

            float baseMoraleChange = 0f;
            float expectationModifier = 1.0f;

            switch (outcome)
            {
                case MatchOutcome.Win:
                    baseMoraleChange = WIN_MORALE_BOOST_BASE;
                    if (expectedOutcome == MatchOutcome.Win) expectationModifier = EXPECTED_WIN_MOD;
                    else if (expectedOutcome == MatchOutcome.Loss) expectationModifier = UNEXPECTED_WIN_MOD; // Upset win
                    // else (expected draw) modifier = 1.2f; // Slightly bigger boost than expected win
                    break;
                case MatchOutcome.Draw:
                    // Change morale based on whether a draw was better/worse than expected
                    if (expectedOutcome == MatchOutcome.Win) baseMoraleChange = DRAW_VS_EXPECTED_WIN_DROP;
                    else if (expectedOutcome == MatchOutcome.Loss) baseMoraleChange = DRAW_VS_EXPECTED_LOSS_BOOST;
                    else baseMoraleChange = DRAW_MORALE_CHANGE; // Expected draw = no change
                    break;
                case MatchOutcome.Loss:
                    baseMoraleChange = LOSS_MORALE_DROP_BASE;
                    if (expectedOutcome == MatchOutcome.Loss) expectationModifier = EXPECTED_LOSS_MOD;
                    else if (expectedOutcome == MatchOutcome.Win) expectationModifier = UNEXPECTED_LOSS_MOD; // Upset loss
                     // else (expected draw) modifier = 1.1f; // Slightly bigger drop than expected loss
                    break;
            }

            float finalBaseChange = baseMoraleChange * expectationModifier;

            // Optional: Log the overall calculation for the team
            // Debug.Log($"[MoraleSimulator] Post-Match Update for {team.Name}: Result={outcome}, Expected={expectedOutcome}, BaseChange={baseMoraleChange:F3}, Mod={expectationModifier:F2}, FinalBase={finalBaseChange:F3}");

            foreach (PlayerData player in team.Roster)
            {
                 float playerSpecificChange = finalBaseChange;

                 // --- Participation Modifier ---
                 // TODO: Needs actual match participation data. Using injury as a proxy.
                 bool participated = !player.IsInjured(); // Simple proxy: Assume non-injured played
                 if (!participated) {
                     playerSpecificChange *= NON_PARTICIPANT_MOD;
                 }

                 // --- Individual Performance Modifier ---
                 // TODO: Apply modifier based on player's match rating when available.
                 // float performanceMod = GetPerformanceModifier(player.MatchRating); // Example
                 // playerSpecificChange *= performanceMod;

                 // --- Personality Modifier ---
                 // TODO: Apply modifier based on personality (e.g., ambitious players react more strongly to losses)
                 // float personalityMod = GetPersonalityModifier(player.PersonalityTraits, outcome, expectedOutcome);
                 // playerSpecificChange *= personalityMod;

                 // --- Add Randomness ---
                 playerSpecificChange += UnityEngine.Random.Range(-0.015f, 0.015f); // Slightly increased randomness

                 // --- Apply Change ---
                 player.Morale = Mathf.Clamp(player.Morale + playerSpecificChange, 0.0f, 1.0f);
                 // Optional log per player: Debug.Log($" -- {player.Name} morale -> {player.Morale:F2} (Change: {playerSpecificChange:F3})");
            }
        }

        /// <summary>
        /// Performs weekly updates to player morale (decay, training, league position, contract/transfer status, form).
        /// </summary>
        /// <param name="team">The team whose players' morale to update.</param>
        public void UpdateMoraleWeekly(TeamData team)
        {
             if (team == null || team.Roster == null) return;
             // Only log occasionally to avoid spam
             // if (GameManager.Instance?.TimeManager?.CurrentDate.Day <= 7) Debug.Log($"[MoraleSimulator] Updating weekly morale for {team.Name}.");

             // --- Get Team-Level Context ---
             int teamPosition = 10; // Default mid-table
             int leagueSize = 20;   // Default size
             bool leagueInfoAvailable = false;
             // TODO: Replace placeholder LeagueManager call with actual implementation
             // In a real scenario, LeagueManager might be accessed via GameManager.Instance
             var leagueManager = GameManager.Instance?.LeagueManager; // Example access
             if(leagueManager != null && team.LeagueID.HasValue) {
                 // Placeholder method - replace with actual LeagueManager method signature
                 (teamPosition, leagueSize) = GetTeamPositionAndSizePlaceholder(leagueManager, team.LeagueID.Value, team.TeamID);
                 leagueInfoAvailable = true;
             }

             // TODO: Get general training atmosphere (needs TrainingSimulator feedback)
             float trainingEffect = 0f; // Placeholder: Neutral training week
             // Example: TrainingReport report = TrainingSimulator.GetLastWeekReport(team.TeamID);
             // if (report.OverallRating > 0.7f) trainingEffect = GOOD_TRAINING_WEEK_BOOST;
             // else if (report.OverallRating < 0.4f) trainingEffect = POOR_TRAINING_WEEK_DROP;

             // TODO: Get Recent Form Modifier (needs access to recent results)
             float formModifier = GetRecentFormModifierPlaceholder(team.TeamID);


             foreach (PlayerData player in team.Roster)
             {
                 float weeklyChange = 0f;

                 // 1. Base decay/regression towards neutral (0.5)
                 // More decay if further from neutral
                 weeklyChange += (0.5f - player.Morale) * 0.02f; // Regress 2% towards 0.5 each week
                 weeklyChange += WEEKLY_MORALE_DECAY; // Plus a small flat decay


                 // 2. Effect of league position (if available)
                 if (leagueInfoAvailable && leagueSize > 3) { // Avoid for tiny leagues
                     if (teamPosition <= 3) weeklyChange += TOP_3_LEAGUE_BOOST;
                     else if (teamPosition >= leagueSize - 2) weeklyChange += BOTTOM_3_LEAGUE_DROP;
                 }

                 // 3. Effect of training (Placeholder)
                 weeklyChange += trainingEffect;

                 // 4. Effect of Recent Form (Team Level)
                 weeklyChange += formModifier;

                 // 5. Contract situation checks
                 if (player.ContractExpiryDate != DateTime.MinValue && player.ContractExpiryDate < GameManager.Instance.TimeManager.CurrentDate.AddMonths(6)) {
                     // If contract expiring soon AND morale is low AND not explicitly unavailable, decrease morale
                     if (player.Morale < 0.4f && player.TransferStatus != TransferStatus.Unavailable) {
                          weeklyChange += LOW_MORALE_EXPIRING_CONTRACT_DROP;
                     }
                 }

                 // 6. Transfer status checks
                 // Unhappy about being listed?
                 if (player.TransferStatus == TransferStatus.ListedByClub && player.Morale < 0.5f) {
                      weeklyChange += UNHAPPY_TRANSFER_LISTED_DROP;
                 }
                 // TODO: Add check for denied transfer request if that state is tracked


                 // 7. Add Randomness
                 weeklyChange += UnityEngine.Random.Range(-0.008f, 0.008f);

                 // 8. Apply Change
                 player.Morale = Mathf.Clamp(player.Morale + weeklyChange, 0.0f, 1.0f);
             }
        }


        /// <summary>
        /// Applies a one-off morale change due to a specific event (e.g., contract, transfer, injury).
        /// </summary>
        /// <param name="player">The player affected.</param>
        /// <param name="eventType">The type of event.</param>
        /// <param name="relatedPlayer">Optional: Secondary player involved (e.g., player being transferred).</param>
        public void ApplyEventMoraleChange(PlayerData player, MoraleEventType eventType, PlayerData relatedPlayer = null)
        {
             if (player == null) return;

             float change = 0f;
             string eventDesc = eventType.ToString(); // Default description

             switch (eventType)
             {
                 case MoraleEventType.ContractRenewed: change = CONTRACT_RENEWED_BOOST; break;
                 case MoraleEventType.ContractRejectedByPlayer: change = CONTRACT_REJECTED_DROP; break;
                 case MoraleEventType.TransferListed: change = TRANSFER_LISTED_DROP; break;
                 case MoraleEventType.TransferRequestMade: break; // No immediate change, wait for response
                 case MoraleEventType.TransferRequestApproved: change = TRANSFER_REQUEST_APPROVED_BOOST; break;
                 case MoraleEventType.TransferRequestDenied: change = TRANSFER_REQUEST_DENIED_DROP; break;
                 case MoraleEventType.TransferCompleted_Incoming:
                     // Affects the whole team slightly? Or just players in same position?
                     // For simplicity, apply small boost to player initiating this call (who is already on team)
                     change = TRANSFER_COMPLETED_IN_BOOST * 0.2f; // Small boost for existing players
                     eventDesc = $"Team signed {relatedPlayer?.FullName ?? "a new player"}";
                     break;
                 case MoraleEventType.TransferCompleted_Outgoing:
                     // Affects the player initiating this call (who is remaining on team)
                     change = TRANSFER_COMPLETED_OUT_DROP;
                     // TODO: Modify change based on relationship with outgoing player?
                     eventDesc = $"{relatedPlayer?.FullName ?? "A player"} left the team";
                     break;
                 case MoraleEventType.ManagerPraise: change = MANAGER_PRAISE_BOOST; break;
                 case MoraleEventType.ManagerCriticism: change = MANAGER_CRITICISM_DROP; break;
                 case MoraleEventType.InjuryOccurred_Minor: change = INJURY_MINOR_DROP; break;
                 case MoraleEventType.InjuryOccurred_Moderate: change = INJURY_MODERATE_DROP; break;
                 case MoraleEventType.InjuryOccurred_Major: change = INJURY_MAJOR_DROP; break;
                 case MoraleEventType.RecoveredFromInjury: change = RECOVERED_FROM_INJURY_BOOST; break;
                 // Add more events: WonAward, PlayerFeud, PromotionAchieved, RelegationSuffered etc.
             }

              if (Mathf.Abs(change) > 0.001f) { // Only apply if change is significant
                 Debug.Log($"[MoraleSimulator] Event '{eventDesc}' affecting {player.FullName}. Change: {change:F3}");
                 player.Morale = Mathf.Clamp(player.Morale + change, 0.0f, 1.0f);
              }
        }

        /// <summary>
        /// Applies a morale change to all players on a team, e.g., when another player is sold.
        /// </summary>
        public void ApplyTeamWideEventMoraleChange(TeamData team, MoraleEventType eventType, PlayerData relatedPlayer = null)
        {
            if (team?.Roster == null) return;
            // Debug.Log($"[MoraleSimulator] Applying team-wide event {eventType} to {team.Name}");
            foreach(var player in team.Roster)
            {
                ApplyEventMoraleChange(player, eventType, relatedPlayer);
            }
        }


        // --- Helper Methods ---

        /// <summary>Placeholder: Predicts the likely outcome of a match based on team reputations.</summary>
        private MatchOutcome PredictOutcome(TeamData team1, TeamData team2)
        {
             if (team1 == null || team2 == null) return MatchOutcome.Draw; // Can't predict

             // Simple reputation comparison (adjust thresholds as needed)
             float repDiff = team1.Reputation - team2.Reputation;
             float repThreshold = 500; // Example threshold for significant difference

             if (repDiff > repThreshold) return MatchOutcome.Win; // Team1 expected to win
             if (repDiff < -repThreshold) return MatchOutcome.Loss; // Team1 expected to lose
             return MatchOutcome.Draw; // Teams are relatively evenly matched
        }

         /// <summary>Placeholder: Gets the opponent's TeamData from a MatchResult.</summary>
        private TeamData GetOpponentTeamData(MatchResult result, int perspectiveTeamID)
        {
             int opponentID = (result.HomeTeamID == perspectiveTeamID) ? result.AwayTeamID : result.HomeTeamID;
             // TODO: Replace with actual lookup from a DatabaseManager or GameManager list
             TeamData opponent = GameManager.Instance?.AllTeams?.FirstOrDefault(t => t.TeamID == opponentID);
             if (opponent != null) return opponent;

             // Placeholder fallback (creates dummy data)
             return new TeamData { TeamID = opponentID, Name = (result.HomeTeamID == opponentID ? result.HomeTeamName : result.AwayTeamName), Reputation = 5000 }; // Dummy opponent
        }

        /// <summary>Placeholder: Gets team position from LeagueManager.</summary>
        private (int position, int leagueSize) GetTeamPositionAndSizePlaceholder(LeagueManager leagueManager, int leagueId, int teamId) {
            // TODO: Implement actual method in LeagueManager: leagueManager.GetTeamPositionAndSize(leagueId, teamId)
            // Returning placeholder mid-table position
             return (position: 10, leagueSize: 20);
        }

        /// <summary>Placeholder: Gets a morale modifier based on recent form.</summary>
        private float GetRecentFormModifierPlaceholder(int teamId) {
             // TODO: Implement logic to retrieve last N match results for the team
             //       and calculate a modifier based on W/D/L record.
             // Example: +0.02 for excellent form, -0.02 for poor form.
             float randomForm = UnityEngine.Random.Range(-RECENT_FORM_MOD_MAX, RECENT_FORM_MOD_MAX);
             return randomForm;
        }

    }

     /// <summary>
     /// Types of discrete events that can affect player morale.
     /// </summary>
     public enum MoraleEventType
     {
         ContractRenewed,
         ContractRejectedByPlayer, // Player rejected team's offer
         TransferListed,           // By club
         TransferRequestMade,      // By player
         TransferRequestApproved,
         TransferRequestDenied,
         TransferCompleted_Incoming, // New player joins team (affects existing players)
         TransferCompleted_Outgoing, // Player leaves team (affects remaining players)
         ManagerPraise,
         ManagerCriticism,
         InjuryOccurred_Minor,
         InjuryOccurred_Moderate,
         InjuryOccurred_Major,
         RecoveredFromInjury,
         // Add more... WonAward, PromotionAchieved, RelegationSuffered, PlayerFeud etc.
     }
}