using UnityEngine;
using HandballManager.Simulation.Core.MatchData; // Updated to reflect new location of MatchData
using HandballManager.Core; // For Enums like PlayerPosition, DefensiveSystem
using HandballManager.Gameplay; // For Tactic class
using System.Collections.Generic; // For List access in spacing
using System.Linq; // For Linq operations in spacing
using System; // For Math
using HandballManager.Simulation.Utils; // For IGeometryProvider

namespace HandballManager.Simulation.AI // Updated from Engines to AI
{
    /// <summary>
    /// Calculates the target tactical position for players based on team tactics,
    /// player role, game phase, ball position, and player attributes.
    /// Provides the "ideal spot" a player should aim for based on the tactical situation.
    /// Note: Current implementation uses hardcoded formations; a future improvement would be data-driven formations.
    /// </summary>
    public class TacticPositioner : ITacticPositioner // Implement the interface
    {
        private readonly IGeometryProvider _geometry;
        
        // Constructor to inject the geometry provider
        public TacticPositioner(IGeometryProvider geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }
        
        // --- Positional & Spacing Constants ---
        private const float SIDELINE_BUFFER = 1.0f;
        private const float FORMATION_WIDTH_FACTOR_DEF = 0.9f;
        private const float FORMATION_WIDTH_FACTOR_ATT = 0.85f;
        private float PITCH_HALF_X => _geometry.PitchLength / 2f;

        // Defensive Depths (distance from own goal line)
        private const float DEF_DEPTH_6_0_LINE = 7f;
        private const float DEF_DEPTH_5_1_BASE = DEF_DEPTH_6_0_LINE; // Base line for 5-1
        private const float DEF_DEPTH_5_1_POINT = 10f; // Forward player in 5-1
        private const float DEF_DEPTH_321_HIGH = 12f;  // Point player in 3-2-1
        private const float DEF_DEPTH_321_MID = 9f;   // Middle pair in 3-2-1
        private const float DEF_DEPTH_321_DEEP = 6.5f; // Deep line in 3-2-1
        private const float DEF_GK_DEPTH = 0.5f; // Hardcoded value that was in MatchSimulator

        // Attacking Depths (distance from *opponent* goal line)
        private const float ATT_DEPTH_PIVOT = 6.5f;
        private const float ATT_DEPTH_WING = 8.0f;
        private const float ATT_DEPTH_BACK = 10.5f;
        private const float ATT_DEPTH_PLAYMAKER = 10.0f; // Usually CentreBack
        private const float ATT_GK_DEPTH = 8f; // How far GK comes out

        // Defensive Slot Counts (Used for interpolation)
        private const int DEF_SLOTS_6_0 = 6;
        private const int DEF_SLOTS_5_1_LINE = 5;
        private const int DEF_SLOTS_321_DEEP = 3;

        // Attacking Width Factors (Portion of half-width)
        private const float ATT_WIDTH_FACTOR_WING = 1.0f; // Wings use full available width
        private const float ATT_WIDTH_FACTOR_BACK = 0.45f; // Backs positioned relatively narrower
        private const float ATT_WIDTH_FACTOR_MID_PAIR = 0.35f; // For 3-2-1 mid players
        private const float ATT_WIDTH_FACTOR_DEEP_321 = 0.8f; // For 3-2-1 deep players

        // Adjustment Factors
        private const float BALL_POS_SHIFT_FACTOR = 0.4f;
        private const float BALL_POS_DEPTH_FACTOR = 0.1f;
        private const float BALL_POS_DEPTH_MAX_SHIFT = 2.0f; // Max depth adjustment towards ball
        private const float TRANSITION_LERP_FACTOR = 0.6f;
        private const float CONTESTED_LERP_FACTOR = 0.1f; // Default pos during contested phase (closer to defense)

        // Spacing Constants
        private const float MIN_SPACING_DISTANCE = 2.5f;
        private const float MIN_SPACING_DISTANCE_SQ = MIN_SPACING_DISTANCE * MIN_SPACING_DISTANCE;
        private const float SPACING_PUSH_FACTOR = 0.5f;

        // Attribute Influence Factors
        private const float ATTRIB_ADJ_WORKRATE_FACTOR = 0.04f; // Meters per point difference from 50 WorkRate
        private const float ATTRIB_ADJ_POSITIONING_FACTOR = 0.03f; // Max meters deviation per point difference from 50 Positioning
        private const float ATTRIB_ADJ_POS_SKILL_BENCHMARK = 50f; // Benchmark skill level for positioning adjustments
        private const float ATTRIB_ADJ_POS_MAX_DEVIATION_BASE = 1.5f; // Base max deviation at low skill (modified by factor)
        private const float ATTRIB_ADJ_POS_MIN_DEVIATION_CHANCE = 0.1f; // Min chance to apply deviation even if skilled

        /// <summary>
        /// Calculates the target tactical position for a specific player in the current state.
        /// Considers team tactics, game phase, player role, ball position, attributes, and spacing.
        /// </summary>
        /// <param name="player">The simulation player object.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>The calculated target position on the pitch.</returns>
        public Vector2 GetPlayerTargetPosition(SimPlayer player, MatchState state)
        {
            // --- Input Validation ---
            if (player?.BaseData == null || state == null)
            {
                Debug.LogError("[TacticPositioner] GetPlayerTargetPosition called with null player, BaseData, or state.");
                // Return current position or a default safe position if possible
                return player?.Position ?? Vector2.zero;
            }
            // Get tactic safely
            Tactic tactic = (player.TeamSimId == 0) ? state.HomeTactic : state.AwayTactic;
            if (tactic == null) {
                Debug.LogError($"[TacticPositioner] Null tactic found for player {player.GetPlayerId()} (TeamSimId: {player.TeamSimId}). Using default position.");
                return player.Position; // Fallback to current position
            }

            // 1. Handle Goalkeeper Separately
            if (player.IsGoalkeeper())
            {
                return GetGoalkeeperPosition(player, state);
            }

            // 2. Determine Base Attacking and Defensive Positions based on role and tactic
            // Note: These methods now contain the core hardcoded formation logic.
            Vector2 defensivePos = GetDefensivePosition(player, tactic);
            Vector2 attackingPos = GetAttackingPosition(player, tactic);

            // 3. Determine Target Position based on Game Phase (Interpolate during Transitions)
            Vector2 basePosition = DetermineBasePositionForPhase(player.TeamSimId, state.CurrentPhase, state.PossessionTeamId, defensivePos, attackingPos);

            // 4. Adjust for Ball Position (Shift formation laterally and slightly depth-wise)
            basePosition = AdjustForBallPosition(basePosition, state.Ball.Position);

            // 5. Apply Player-Specific Attribute Adjustments (Work Rate, Positioning)
            basePosition = ApplyAttributeAdjustments(player, basePosition, IsPlayerTeamAttacking(player.TeamSimId, state.PossessionTeamId), state.RandomGenerator);

            // 6. Apply Spacing Logic (Prevent Clustering with Teammates)
            basePosition = ApplySpacing(player, basePosition, state);

            // 7. Final Clamping to Pitch Boundaries (with buffer)
            basePosition.x = Mathf.Clamp(basePosition.x, SIDELINE_BUFFER, _geometry.PitchLength - SIDELINE_BUFFER);
            basePosition.y = Mathf.Clamp(basePosition.y, SIDELINE_BUFFER, _geometry.PitchWidth - SIDELINE_BUFFER);

            return basePosition;
        }

        /// <summary>Calculates the target position for a goalkeeper based on game state.</summary>
        private Vector2 GetGoalkeeperPosition(SimPlayer gk, MatchState state)
        {
            // Added null check for safety, though already checked in main method
            if (gk?.BaseData == null || state == null) return Vector2.zero;

            bool isOwnTeamPossession = gk.TeamSimId == state.PossessionTeamId && state.PossessionTeamId != -1;
            Vector2 goalCenter = gk.TeamSimId == 0 ? _geometry.HomeGoalCenter : _geometry.AwayGoalCenter;
            float baseGoalLineX = goalCenter.x;

            Vector2 position;

            if (isOwnTeamPossession)
            {
                // Attack: Positioned further out
                float depth = ATT_GK_DEPTH;
                float goalX = baseGoalLineX + (gk.TeamSimId == 0 ? depth : -depth);
                position = new Vector2(goalX, _geometry.Center.y); // Central Y
            }
            else // Defending or Contested
            {
                // Defense: On/near goal line, reacting to ball Y
                float depth = DEF_GK_DEPTH;
                float goalX = baseGoalLineX + (gk.TeamSimId == 0 ? depth : -depth);
                position = new Vector2(goalX, _geometry.Center.y);

                // Adjust Y based on ball position and GK Positioning skill
                float positioningSkill = gk.BaseData?.PositioningGK ?? ATTRIB_ADJ_POS_SKILL_BENCHMARK; // Use benchmark if null
                float ballInfluence = Mathf.Lerp(0.3f, 0.8f, positioningSkill / 100f);
                position.y = Mathf.Lerp(position.y, state.Ball.Position.y, ballInfluence);
                // Clamp Y position to within goal posts (+ slight buffer)
                float goalBuffer = _geometry.GoalWidth * 0.1f; // 10% buffer outside post
                position.y = Mathf.Clamp(position.y, goalCenter.y - (_geometry.GoalWidth / 2f) - goalBuffer,
                                                   goalCenter.y + (_geometry.GoalWidth / 2f) + goalBuffer);
            }

            // Clamp X to prevent going too far behind line or excessively far out
            position.x = Mathf.Clamp(position.x, baseGoalLineX - 1f, baseGoalLineX + ATT_GK_DEPTH + 1f);

            return position;
        }

        /// <summary>Determines the base position based on the current game phase.</summary>
        private Vector2 DetermineBasePositionForPhase(int playerTeamId, GamePhase phase, int possessionTeamId, Vector2 defensivePos, Vector2 attackingPos)
        {
            bool isAttackingPhase = IsPlayerTeamAttacking(playerTeamId, possessionTeamId);
            bool isDefendingPhase = IsPlayerTeamDefending(playerTeamId, possessionTeamId);
            bool isInTransition = phase == GamePhase.TransitionToHomeAttack || phase == GamePhase.TransitionToAwayAttack;

            if (isAttackingPhase) {
                 return attackingPos;
            } else if (isDefendingPhase) {
                 return defensivePos;
            } else if (isInTransition) {
                 bool transitioningToAttack = (phase == GamePhase.TransitionToHomeAttack && playerTeamId == 0) ||
                                              (phase == GamePhase.TransitionToAwayAttack && playerTeamId == 1);
                 float lerpFactor = transitioningToAttack ? TRANSITION_LERP_FACTOR : (1.0f - TRANSITION_LERP_FACTOR);
                 return Vector2.Lerp(defensivePos, attackingPos, lerpFactor);
            } else { // Contested Ball, Kickoff phases - Default closer to defensive shape
                 return Vector2.Lerp(defensivePos, attackingPos, CONTESTED_LERP_FACTOR);
            }
        }

        /// <summary>Helper to check if the player's team is considered attacking.</summary>
        private bool IsPlayerTeamAttacking(int playerTeamId, int possessionTeamId) {
            return playerTeamId == possessionTeamId && possessionTeamId != -1;
        }
        /// <summary>Helper to check if the player's team is considered defending.</summary>
        private bool IsPlayerTeamDefending(int playerTeamId, int possessionTeamId) {
             return playerTeamId != possessionTeamId && possessionTeamId != -1;
        }


        /// <summary>Calculates the base defensive position based on role and tactical system.</summary>
        /// <remarks>Currently uses hardcoded formation logic. Future: Data-driven.</remarks>
        private Vector2 GetDefensivePosition(SimPlayer player, Tactic tactic)
        {
            if (player?.BaseData == null) return Vector2.zero; // Safety check

            float goalLineX = (player.TeamSimId == 0) ? 0f : _geometry.PitchLength;
            float centerGoalY = _geometry.Center.y;
            float formationWidth = _geometry.PitchWidth * FORMATION_WIDTH_FACTOR_DEF;
            float halfWidth = formationWidth / 2f;
            Vector2 position = new Vector2(goalLineX, centerGoalY);
            float depth = DEF_DEPTH_6_0_LINE; // Default depth

            int playerSlotIndex = GetDefensiveSlotIndex(player.BaseData.PrimaryPosition, tactic.DefensiveSystem);
            int slotCount = DEF_SLOTS_6_0; // Default interpolation slots

            // Determine depth and Y position based on defensive system and slot
            switch (tactic.DefensiveSystem)
            {
                case DefensiveSystem.SixZero:
                    depth = DEF_DEPTH_6_0_LINE;
                    slotCount = DEF_SLOTS_6_0;
                    position.y = centerGoalY + Mathf.Lerp(-halfWidth, halfWidth, (float)playerSlotIndex / (slotCount - 1));
                    break;

                case DefensiveSystem.FiveOne:
                    if (playerSlotIndex == 2) { // Central player pushes up (Slot 2 assumed point)
                        depth = DEF_DEPTH_5_1_POINT;
                        position.y = centerGoalY;
                    } else {
                        depth = DEF_DEPTH_5_1_BASE;
                        slotCount = DEF_SLOTS_5_1_LINE;
                        int lineIndex = (playerSlotIndex > 2) ? playerSlotIndex - 1 : playerSlotIndex; // Adjust index (0-4)
                        position.y = centerGoalY + Mathf.Lerp(-halfWidth, halfWidth, (float)lineIndex / (slotCount - 1));
                    }
                    break;

                case DefensiveSystem.ThreeTwoOne:
                    if (playerSlotIndex == 5) { // High player
                        depth = DEF_DEPTH_321_HIGH; position.y = centerGoalY;
                    } else if (playerSlotIndex == 3 || playerSlotIndex == 4) { // Mid players
                        depth = DEF_DEPTH_321_MID;
                        position.y = centerGoalY + (playerSlotIndex == 3 ? -halfWidth * ATT_WIDTH_FACTOR_MID_PAIR : halfWidth * ATT_WIDTH_FACTOR_MID_PAIR); // Use constant
                    } else { // Deep players (Slots 0, 1, 2)
                         depth = DEF_DEPTH_321_DEEP;
                         slotCount = DEF_SLOTS_321_DEEP;
                         position.y = centerGoalY + Mathf.Lerp(-halfWidth * ATT_WIDTH_FACTOR_DEEP_321, halfWidth * ATT_WIDTH_FACTOR_DEEP_321, (float)playerSlotIndex / (slotCount - 1)); // Use constants
                    }
                    break;

                default: // Fallback to 6-0
                     depth = DEF_DEPTH_6_0_LINE;
                     slotCount = DEF_SLOTS_6_0;
                     position.y = centerGoalY + Mathf.Lerp(-halfWidth, halfWidth, (float)playerSlotIndex / (slotCount - 1));
                     break;
            }

            // Apply depth relative to own goal line
            position.x += (player.TeamSimId == 0) ? depth : -depth;
            return position;
        }

        /// <summary>Gets a representative defensive slot index for a player position and system.</summary>
        /// <remarks>This mapping is simplified and hardcoded. Needs refinement or data-driven approach.</remarks>
        private int GetDefensiveSlotIndex(PlayerPosition position, DefensiveSystem system)
        {
            // Revised mapping for clarity and basic 3-2-1 logic
            switch (system)
            {
                case DefensiveSystem.SixZero:
                case DefensiveSystem.FiveOne: // Use 6-slot indices; logic in GetDefensivePosition handles the '1'
                    switch (position) {
                        case PlayerPosition.LeftWing: return 0;
                        case PlayerPosition.LeftBack: return 1;
                        case PlayerPosition.CentreBack: return 2; // Central
                        case PlayerPosition.Pivot: return 3;      // Central/Offset
                        case PlayerPosition.RightBack: return 4;
                        case PlayerPosition.RightWing: return 5;
                        default: return 2; // Fallback central
                    }

                case DefensiveSystem.ThreeTwoOne:
                    // Tentative mapping: Deep (0,1,2), Mid (3,4), High (5)
                    switch (position) {
                        case PlayerPosition.LeftWing: return 0;   // Deep Left
                        case PlayerPosition.Pivot: return 1;      // Deep Center
                        case PlayerPosition.RightWing: return 2;  // Deep Right
                        case PlayerPosition.LeftBack: return 3;   // Mid Left
                        case PlayerPosition.RightBack: return 4;  // Mid Right
                        case PlayerPosition.CentreBack: return 5; // High Point
                        default: return 5; // Fallback High? Or Deep Center? Let's use High.
                    }

                default: // Fallback for unknown systems
                    return GetDefensiveSlotIndex(position, DefensiveSystem.SixZero);
            }
        }


        /// <summary>Calculates the base attacking position based on role.</summary>
        /// <remarks>Currently uses hardcoded formation logic. Future: Data-driven.</remarks>
         private Vector2 GetAttackingPosition(SimPlayer player, Tactic tactic) // Tactic needed for Focus Play
         {
             if (player?.BaseData == null) return Vector2.zero; // Safety check

             float opponentGoalLineX = (player.TeamSimId == 0) ? _geometry.PitchLength : 0f;
             float centerGoalY = _geometry.Center.y;
             float formationWidth = _geometry.PitchWidth * FORMATION_WIDTH_FACTOR_ATT;
             float halfWidth = formationWidth / 2f;
             Vector2 position = new Vector2(opponentGoalLineX, centerGoalY); // Start relative to opponent goal
             float depth = ATT_DEPTH_BACK; // Default depth

             // --- Basic Role Positioning ---
             switch (player.BaseData.PrimaryPosition)
             {
                  case PlayerPosition.LeftWing:   depth = ATT_DEPTH_WING;   position.y = centerGoalY - halfWidth * ATT_WIDTH_FACTOR_WING; break;
                  case PlayerPosition.RightWing:  depth = ATT_DEPTH_WING;   position.y = centerGoalY + halfWidth * ATT_WIDTH_FACTOR_WING; break;
                  case PlayerPosition.LeftBack:   depth = ATT_DEPTH_BACK;   position.y = centerGoalY - halfWidth * ATT_WIDTH_FACTOR_BACK; break;
                  case PlayerPosition.RightBack:  depth = ATT_DEPTH_BACK;   position.y = centerGoalY + halfWidth * ATT_WIDTH_FACTOR_BACK; break;
                  case PlayerPosition.CentreBack: depth = ATT_DEPTH_PLAYMAKER; position.y = centerGoalY; break;
                  case PlayerPosition.Pivot:      depth = ATT_DEPTH_PIVOT;  position.y = centerGoalY; break; // Pivot finds central space near 6m
                  default: Debug.LogWarning($"Unhandled player position {player.BaseData.PrimaryPosition} in GetAttackingPosition"); break;
             }

             // Apply depth relative to opponent goal line (closer = more negative for home, more positive for away)
             position.x += (player.TeamSimId == 0) ? -depth : depth;

             // --- Adjust based on Tactical Focus Play ---
              float focusShiftYFactor = 0.2f; // How much players shift inwards/outwards relative to halfWidth
              float focusShiftX = 1.0f; // How much players shift depth-wise

              switch (tactic.FocusPlay) // Use tactic parameter
              {
                   case OffensiveFocusPlay.Wings: // Pull others slightly central/deeper
                       if (IsBackcourtOrPivot(player.BaseData.PrimaryPosition)) {
                            position.y = Mathf.Lerp(position.y, centerGoalY, focusShiftYFactor * 0.5f); // Shift 10% towards center
                            position.x += (player.TeamSimId == 0) ? focusShiftX : -focusShiftX; // Slightly deeper
                       }
                       break;
                   case OffensiveFocusPlay.Backs: // Wings tuck in slightly / hold depth
                        if (IsWing(player.BaseData.PrimaryPosition)) {
                             position.x += (player.TeamSimId == 0) ? focusShiftX * 0.5f : -focusShiftX * 0.5f; // Slightly less deep than default
                             position.y = Mathf.Lerp(position.y, centerGoalY, focusShiftYFactor * 0.5f); // Slightly narrower
                        }
                       break;
                   case OffensiveFocusPlay.Pivot: // Wings and Backs shift slightly central
                        if (IsWing(player.BaseData.PrimaryPosition) || IsBack(player.BaseData.PrimaryPosition)) {
                             position.y = Mathf.Lerp(position.y, centerGoalY, focusShiftYFactor); // Shift 20% towards center
                        }
                        break;
                   case OffensiveFocusPlay.Balanced: // No change from base role position
                   default:
                       break;
              }

             return position;
         }

         // Helper functions for role checks
         private bool IsWing(PlayerPosition pos) => pos == PlayerPosition.LeftWing || pos == PlayerPosition.RightWing;
         private bool IsBack(PlayerPosition pos) => pos == PlayerPosition.LeftBack || pos == PlayerPosition.RightBack || pos == PlayerPosition.CentreBack;
         private bool IsBackcourtOrPivot(PlayerPosition pos) => IsBack(pos) || pos == PlayerPosition.Pivot;


        // --- Adjustments ---

        /// <summary>
        /// Shifts the base position laterally and slightly depth-wise towards the ball's position.
        /// </summary>
        private Vector2 AdjustForBallPosition(Vector2 currentPosition, Vector2 ballPosition)
        {
             // Lateral Shift (Y-axis): Move formation towards ball Y
             float ballYRatio = Mathf.Clamp01(ballPosition.y / _geometry.PitchWidth);
             float targetY = Mathf.Lerp(currentPosition.y, ballYRatio * _geometry.PitchWidth, BALL_POS_SHIFT_FACTOR);

             // Depth Shift (X-axis): Subtle push towards ball X
             float xDiff = ballPosition.x - currentPosition.x;
             // Apply shift factor and clamp the maximum adjustment
             float depthAdjustment = Mathf.Clamp(xDiff * BALL_POS_DEPTH_FACTOR, -BALL_POS_DEPTH_MAX_SHIFT, BALL_POS_DEPTH_MAX_SHIFT);

             // Apply adjustments
             currentPosition.y = targetY;
             currentPosition.x += depthAdjustment;

             return currentPosition;
        }

        /// <summary>
        /// Applies subtle adjustments to the target position based on player attributes like Work Rate and Positioning.
        /// Includes a random element influenced by Positioning skill.
        /// </summary>
        private Vector2 ApplyAttributeAdjustments(SimPlayer player, Vector2 position, bool isAttackingPhase, System.Random random)
        {
             // Safety check for player data and random generator
             if (player?.BaseData == null || random == null) return position;

             // --- Work Rate Adjustment --- Affects willingness to push forward/track back
             float workRateDiff = player.BaseData.WorkRate - ATTRIB_ADJ_POS_SKILL_BENCHMARK; // Diff from benchmark 50
             // Positive direction is towards opponent goal (Negative X for Home, Positive X for Away)
             float attackDirectionX = (player.TeamSimId == 0) ? -1f : 1f;
             // If attacking, higher WR pushes further forward. If defending, higher WR tracks back more (further from opponent goal).
             float depthShiftDirection = isAttackingPhase ? attackDirectionX : -attackDirectionX;
             float depthShift = workRateDiff * ATTRIB_ADJ_WORKRATE_FACTOR * depthShiftDirection;
             position.x += depthShift;

             // --- Positioning Adjustment --- Affects deviation from calculated spot
             // Use the actual Positioning attribute. Lower skill = higher potential deviation.
             float positioningSkill = player.BaseData.Positioning; // Assuming Positioning attribute exists now
             // Calculate max potential deviation based on skill difference from benchmark
             float deviationPotential = Mathf.Clamp((100f - positioningSkill) * ATTRIB_ADJ_POSITIONING_FACTOR, 0f, ATTRIB_ADJ_POS_MAX_DEVIATION_BASE);

             // Apply deviation only if potential is significant OR there's a minimum random chance
             if (deviationPotential > 0.1f || random.NextDouble() < ATTRIB_ADJ_POS_MIN_DEVIATION_CHANCE)
             {
                  // Generate random direction and magnitude (scaled by potential)
                  float angle = (float)random.NextDouble() * 2f * Mathf.PI; // Random angle in radians
                  Vector2 randomDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                  float randomMagnitude = (float)random.NextDouble() * deviationPotential; // Random magnitude up to potential
                  // Apply the random offset
                  position += randomDir * randomMagnitude;
             }

             return position;
        }

        /// <summary>
        /// Adjusts the target position slightly to maintain minimum spacing from teammates, preventing clustering.
        /// Note: This is an O(N^2) operation within the team, acceptable for small team sizes.
        /// </summary>
        private Vector2 ApplySpacing(SimPlayer player, Vector2 targetPosition, MatchState state)
        {
            // Safety checks
            if (player == null || state == null) return targetPosition;

            Vector2 cumulativePush = Vector2.zero;
            int closeNeighbors = 0;
            var teammates = state.GetTeamOnCourt(player.TeamSimId);
            if (teammates == null) return targetPosition; // Check if teammate list is valid

            // Iterate through teammates currently on court
            foreach (var teammate in teammates)
            {
                // Skip self, nulls, or suspended players
                if (teammate == null || teammate == player || !teammate.IsOnCourt || teammate.IsSuspended()) continue;

                // Calculate vector and distance based on *current* positions for reaction
                Vector2 vectorToTeammate = player.Position - teammate.Position;
                float distSq = vectorToTeammate.sqrMagnitude;

                // If teammate is too close (within minimum spacing squared distance)
                if (distSq < MIN_SPACING_DISTANCE_SQ && distSq > 0.01f) // Use constant, add epsilon check
                {
                    closeNeighbors++;
                    float distance = Mathf.Sqrt(distSq);
                    // Calculate push magnitude based on how much overlap there is
                    float pushMagnitude = (MIN_SPACING_DISTANCE - distance) * SPACING_PUSH_FACTOR; // Use constant
                    // Add push vector (away from the teammate) to the cumulative push
                    // Normalize safely in case distSq was extremely small but passed check
                    Vector2 pushDir = (distSq > 0.0001f) ? vectorToTeammate.normalized : Vector2.right; // Default push direction if overlapping
                    cumulativePush += pushDir * pushMagnitude;
                }
            }

            // Apply the total calculated push vector to the target position
            if (closeNeighbors > 0)
            {
                // Optional: Clamp the maximum push distance from spacing?
                // float maxPush = 1.0f;
                // cumulativePush = Vector2.ClampMagnitude(cumulativePush, maxPush);
                targetPosition += cumulativePush;
            }

            return targetPosition;
        }

    }
}