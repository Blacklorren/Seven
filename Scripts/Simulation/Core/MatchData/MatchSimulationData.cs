using UnityEngine;
using HandballManager.Data;
// using System.Collections.Generic; // No longer needed directly in this file
using HandballManager.Core; // For PlayerPosition enum
using System; // For ArgumentNullException, Math

namespace HandballManager.Simulation.Core.MatchData // Updated to match new folder structure
{
    /// <summary>
    /// Contains constants related to simulation data structures and logic.
    /// Marked internal as these are primarily for simulation engine use.
    /// </summary>
 
    /// <summary>
    /// Represents the state and physics of the ball within the 3D simulation.
    /// </summary>
    public class SimBall
    {
        /// <summary>Current 3D position of the ball in world space (Y is height).</summary>
        public Vector3 Position { get; internal set; } // Encapsulated with internal setter
        /// <summary>Current 3D velocity of the ball (m/s).</summary>
        public Vector3 Velocity { get; internal set; } // Encapsulated with internal setter
        /// <summary>Current angular velocity (spin) of the ball (radians/s), axis represents rotation axis.</summary>
        public Vector3 AngularVelocity { get; internal set; } // Encapsulated with internal setter

        /// <summary>The player currently holding the ball (null if loose or in flight).</summary>
        public SimPlayer Holder { get; private set; } = null;
        /// <summary>True if the ball is not held and not actively in flight (e.g., rolling, stationary).</summary>
        public bool IsLoose => Holder == null && !IsInFlight && !IsRolling;
        /// <summary>True if the ball was passed or shot and is currently moving through the air.</summary>
        public bool IsInFlight { get; private set; } = false;
        /// <summary>True if the ball is on the ground and rolling.</summary>
        public bool IsRolling { get; private set; } = false;
        /// <summary>Simulation Team ID (0=Home, 1=Away) of the team that last touched the ball.</summary>
        public int LastTouchedByTeamId { get; private set; } = -1;
        /// <summary>Reference to the player who last touched the ball.</summary>
        public SimPlayer LastTouchedByPlayer { get; private set; } = null;
        // Height is now directly Position.y

        // --- Pass Context ---
        /// <summary>The player who initiated the current pass (if any).</summary>
        public SimPlayer Passer { get; private set; } = null;
        /// <summary>The intended recipient of the current pass (if any).</summary>
        public SimPlayer IntendedTarget { get; private set; } = null;
        /// <summary>The position where the current pass was initiated.</summary>
        public Vector3 PassOrigin { get; private set; } = Vector3.zero; // Now 3D

        // --- Shot Context ---
        /// <summary>The player who last attempted a shot.</summary>
        public SimPlayer LastShooter { get; private set; } = null;

        /// <summary>
        /// Initializes a new SimBall instance.
        /// </summary>
        /// <param name="startPos">Initial position of the ball (Y is height).</param>
        public SimBall(Vector3 startPos = default)
        {
            Position = startPos;
            // Ensure ball starts on the ground if default position used
            if (startPos == default) Position.y = SimConstants.BALL_RADIUS;
            Velocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Resets the context related to an active pass. Does not affect IsInFlight status.
        /// </summary>
        public void ResetPassContext()
        {
            Passer = null;
            IntendedTarget = null;
            PassOrigin = Vector3.zero;
        }

        /// <summary>
        /// Assigns possession of the ball to a player. Updates 3D position.
        /// If the player is null, makes the ball loose at its current position.
        /// Ensures previous holder's state is updated.
        /// </summary>
        /// <param name="player">The player gaining possession, or null to make the ball loose.</param>
        public void SetPossession(SimPlayer player)
        {
            // Clear previous holder's status if necessary
            if (Holder != null && Holder != player && Holder.HasBall) {
                 Holder.HasBall = false; // Ensure previous holder knows they lost the ball
            }

            if (player != null)
            {
                Holder = player;
                IsInFlight = false;
                IsRolling = false;
                ResetPassContext();
                LastShooter = null; // Reset shooter on possession change
                Velocity = Vector3.zero; // Stop ball movement
                AngularVelocity = Vector3.zero; // Stop spin

                LastTouchedByTeamId = player.TeamSimId; // Assumes player.TeamSimId is valid
                LastTouchedByPlayer = player;
                player.HasBall = true; // Update player's state

                // Position ball slightly offset from the player
                Vector2 offsetDir2D = Vector2.right * (player.TeamSimId == 0 ? 1f : -1f); // Default direction
                // Use near zero check constant
                if (player.Velocity.sqrMagnitude > SimConstants.VELOCITY_NEAR_ZERO_SQ) {
                    offsetDir2D = player.Velocity.normalized;
                }
                
                // Convert 2D player position to 3D ball position
                Vector3 playerPos3D = new Vector3(player.Position.x, SimConstants.BALL_DEFAULT_HEIGHT, player.Position.y);
                Vector3 offsetDir3D = new Vector3(offsetDir2D.x, 0f, offsetDir2D.y);
                Position = playerPos3D + offsetDir3D * SimConstants.BALL_OFFSET_FROM_HOLDER;

            } else {
                // Player is null - handle this explicitly by making the ball loose
                Debug.LogWarning("[SimBall] SetPossession called with null player. Making ball loose.");
                Vector3 currentPos = this.Position;
                MakeLoose(currentPos, Vector3.zero, this.LastTouchedByTeamId, this.LastTouchedByPlayer);
            }
        }

        /// <summary>
        /// Releases the ball as a pass from a specific player towards a target.
        /// Validates parameters before proceeding.
        /// </summary>
        /// <param name="passer">The player initiating the pass. Can be null.</param>
        /// <param name="target">The intended recipient of the pass (required).</param>
        /// <param name="initialVelocity">The initial 3D velocity vector of the pass.</param>
        /// <param name="initialSpin">Optional initial angular velocity (spin) to apply to the ball.</param>
        public void ReleaseAsPass(SimPlayer passer, SimPlayer target, Vector3 initialVelocity, Vector3 initialSpin = default)
        {
            // Validate target
            if (target == null) {
                Debug.LogError("[SimBall] ReleaseAsPass called with null target. Ball made loose instead.");
                // Use safe origin position and last touch info
                Vector3 origin = this.Position; // Default to current 3D position
                if (passer != null) {
                    // Convert 2D player position to 3D
                    origin = new Vector3(passer.Position.x, SimConstants.BALL_DEFAULT_HEIGHT, passer.Position.y);
                }
                MakeLoose(origin, Vector3.zero, passer?.TeamSimId ?? this.LastTouchedByTeamId, passer ?? this.LastTouchedByPlayer);
                return;
            }

            Vector3 originPos = this.Position; // Default origin (already 3D)
            if (passer != null)
            {
                if (passer.HasBall) passer.HasBall = false; // Ensure passer releases the ball state
                // Convert 2D player position to 3D for pass origin
                Vector3 passerPos3D = new Vector3(passer.Position.x, SimConstants.BALL_DEFAULT_HEIGHT, passer.Position.y);
                PassOrigin = passerPos3D;
                originPos = passerPos3D;
                LastTouchedByTeamId = passer.TeamSimId;
                LastTouchedByPlayer = passer;
            } else {
                PassOrigin = this.Position; // Use current 3D ball pos if no passer
            }

            Holder = null;
            IsInFlight = true;
            IsRolling = false;
            IntendedTarget = target;
            Passer = passer;
            LastShooter = null;
            Velocity = initialVelocity;
            AngularVelocity = initialSpin;
            
            // Ensure non-zero velocity for normalization
            Vector3 releaseDir;
            if (initialVelocity.sqrMagnitude > SimConstants.VELOCITY_NEAR_ZERO_SQ) {
                releaseDir = initialVelocity.normalized;
            } else {
                // Default direction if velocity is near zero
                releaseDir = new Vector3(1f, 0f, 0f);
            }
            
            Position = originPos + releaseDir * SimConstants.BALL_RELEASE_OFFSET;
        }

        /// <summary>
        /// Releases the ball as a shot from a specific player.
        /// Validates parameters before proceeding.
        /// </summary>
        /// <param name="shooter">The player initiating the shot (required).</param>
        /// <param name="initialVelocity">The initial 3D velocity vector of the shot.</param>
        /// <param name="initialSpin">Optional initial angular velocity (spin) to apply to the ball.</param>
        public void ReleaseAsShot(SimPlayer shooter, Vector3 initialVelocity, Vector3 initialSpin = default)
        {
            // Validate shooter and BaseData
            if (shooter?.BaseData == null) {
                 Debug.LogError($"[SimBall] ReleaseAsShot called with null shooter or BaseData. Ball made loose instead. Shooter: {shooter?.GetPlayerId() ?? -1}");
                 // Make loose at shooter's position if available, otherwise current ball pos
                 Vector3 currentPos = this.Position; // Use current 3D position
                 if (shooter != null) {
                     // Convert 2D player position to 3D
                     currentPos = new Vector3(shooter.Position.x, SimConstants.BALL_DEFAULT_HEIGHT, shooter.Position.y);
                 }
                 MakeLoose(currentPos, Vector3.zero, shooter?.TeamSimId ?? this.LastTouchedByTeamId, shooter ?? this.LastTouchedByPlayer);
                 return;
            }

            // Convert 2D player position to 3D for shot origin
            Vector3 originPos = new Vector3(shooter.Position.x, SimConstants.BALL_DEFAULT_HEIGHT, shooter.Position.y);
            if (shooter.HasBall) shooter.HasBall = false;
            LastTouchedByTeamId = shooter.TeamSimId;
            LastTouchedByPlayer = shooter;

            Holder = null;
            IsInFlight = true;
            IsRolling = false; // Ensure rolling state is cleared
            ResetPassContext();
            LastShooter = shooter;
            Velocity = initialVelocity;
            AngularVelocity = initialSpin; // Apply spin to the ball
            
            // Ensure non-zero velocity for normalization
            Vector3 releaseDir;
            if (initialVelocity.sqrMagnitude > SimConstants.VELOCITY_NEAR_ZERO_SQ) {
                releaseDir = initialVelocity.normalized;
            } else {
                // Default direction if velocity is near zero
                releaseDir = new Vector3(1f, 0f, 0f);
            }
            
            Position = originPos + releaseDir * SimConstants.BALL_RELEASE_OFFSET;
        }

        /// <summary>
        /// Sets the ball state to loose (not held, not in flight) at a specified position and velocity.
        /// Clears the current holder if any.
        /// </summary>
        /// <param name="position">The 3D position where the ball becomes loose.</param>
        /// <param name="velocity">The initial 3D velocity of the loose ball (e.g., rebound).</param>
        /// <param name="lastTeamId">The simulation ID of the team that last influenced the ball.</param>
        /// <param name="lastPlayer">The player who last influenced the ball (optional).</param>
        public void MakeLoose(Vector3 position, Vector3 velocity, int lastTeamId, SimPlayer lastPlayer = null)
        {
            if (Holder != null) {
                 if (Holder.HasBall) Holder.HasBall = false;
                 Holder = null;
            }

            IsInFlight = false;
            IsRolling = false; // Ensure rolling state is cleared
            ResetPassContext();
            LastShooter = null;
            LastTouchedByTeamId = lastTeamId;
            LastTouchedByPlayer = lastPlayer;
            Position = position;
            Velocity = velocity;
            AngularVelocity = Vector3.zero; // Reset any spin
            
            // Ensure the ball has appropriate height if not specified
            if (position.y < SimConstants.BALL_RADIUS) {
                Position = new Vector3(position.x, SimConstants.BALL_DEFAULT_HEIGHT * SimConstants.BALL_LOOSE_HEIGHT_FACTOR, position.z);
            }
        }

        /// <summary>
        /// Stops the ball's movement (sets velocity to zero) and sets its state to not in flight.
        /// Does not clear holder or context, as it might stop while held or after an event.
        /// </summary>
        public void Stop()
        {
            Velocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            IsInFlight = false;
            IsRolling = false;
        }
        /// <summary>
        /// Sets the ball to rolling state. Used when the ball transitions from flight to rolling on the ground.
        /// </summary>
        public void StartRolling()
        {
            if (Holder == null) // Only start rolling if not held
            {
                IsInFlight = false;
                IsRolling = true;
            }
        }
    }

    // --- Player Simulation State ---
    /// <summary>
    /// Represents the dynamic state of a player within the simulation.
    /// Links to the base PlayerData and includes current position, action, stamina, etc.
    /// </summary>
    public class SimPlayer
    {
        // --- Static Info (Reference) ---
        /// <summary>Reference to the persistent player data (attributes, contract info etc.).</summary>
        public PlayerData BaseData { get; private set; }
        /// <summary>Simulation Team ID (0 = Home, 1 = Away).</summary>
        public int TeamSimId { get; private set; }

        // --- Dynamic State ---
        /// <summary>Current 2D position on the pitch.</summary>
        public Vector2 Position { get; internal set; } // Encapsulated
        /// <summary>Current 2D velocity.</summary>
        public Vector2 Velocity { get; internal set; } // Encapsulated
        /// <summary>True if this player is currently holding the ball.</summary>
        public bool HasBall { get; set; } = false; // Allow external set by SimBall
        /// <summary>Current stamina level (1.0 = full, 0.0 = empty).</summary>
        public float Stamina { get; set; } = 1.0f;
        /// <summary>True if the player is currently considered active on the court.</summary>
        public bool IsOnCourt { get; set; } = false;
        /// <summary>Seconds remaining if player is serving a suspension.</summary>
        public float SuspensionTimer { get; set; } = 0f;
        /// <summary>The player's current primary action/intent.</summary>
        public PlayerAction CurrentAction { get; set; } = PlayerAction.Idle;
        /// <summary>The target position the player is trying to reach.</summary>
        public Vector2 TargetPosition { get; set; }
        /// <summary>Reference to another player targeted by the current action (pass, tackle, mark).</summary>
        public SimPlayer TargetPlayer { get; set; } = null;
        /// <summary>Countdown timer for actions requiring preparation (pass/shot windup, tackle attempt).</summary>
        public float ActionTimer { get; set; } = 0f;

        /// <summary>Calculated maximum speed based on base attributes and current stamina.</summary>
        public float EffectiveSpeed { get; private set; } = 0f;

        /// <summary>
        /// Initializes a new SimPlayer instance.
        /// </summary>
        /// <param name="baseData">The persistent PlayerData for this player.</param>
        /// <param name="teamSimId">The simulation team ID (0 or 1).</param>
        /// <exception cref="ArgumentNullException">Thrown if baseData is null.</exception>
        public SimPlayer(PlayerData baseData, int teamSimId)
        {
            BaseData = baseData ?? throw new ArgumentNullException(nameof(baseData), "SimPlayer cannot be created with null PlayerData.");
            if (teamSimId != 0 && teamSimId != 1) {
                Debug.LogWarning($"[SimPlayer] Invalid TeamSimId ({teamSimId}) provided for player {baseData.FullName}. Defaulting to 0 (Home).");
                teamSimId = 0;
            }

            TeamSimId = teamSimId;
            Position = Vector2.zero; // Initial position set later by setup logic
            Velocity = Vector2.zero;
            TargetPosition = Position;
            Stamina = 1.0f;
            UpdateEffectiveSpeed();
        }

        /// <summary>Checks if the player is currently serving a suspension.</summary>
        public bool IsSuspended() => SuspensionTimer > SimConstants.FLOAT_EPSILON; // Use epsilon for float comparison
        /// <summary>Checks if the player's primary role is Goalkeeper.</summary>
        public bool IsGoalkeeper() => BaseData?.PrimaryPosition == PlayerPosition.Goalkeeper;

        /// <summary>
        /// Updates the player's effective maximum speed based on their base speed attribute and current stamina level.
        /// </summary>
        public void UpdateEffectiveSpeed()
        {
             // Use default/benchmark values if BaseData is somehow null
             float baseSpeedAttr = BaseData?.Speed ?? SimConstants.PLAYER_DEFAULT_ATTRIBUTE_VALUE;
             float maxSpeedPossible = (baseSpeedAttr / 100f) * SimConstants.PLAYER_DEFAULT_MAX_SPEED;

             float staminaFactor = 1.0f;
             if (Stamina < SimConstants.PLAYER_STAMINA_LOW_THRESHOLD) {
                 staminaFactor = Mathf.Lerp(SimConstants.PLAYER_STAMINA_MIN_SPEED_FACTOR, 1.0f, Stamina / SimConstants.PLAYER_STAMINA_LOW_THRESHOLD);
             }
             EffectiveSpeed = maxSpeedPossible * staminaFactor;
        }

        // --- Safe Accessors for BaseData Properties ---
        /// <summary>Safely gets the player's persistent Team ID.</summary>
        public int GetTeamId() => BaseData?.CurrentTeamID ?? -1;
        /// <summary>Safely gets the player's persistent Player ID.</summary>
        public int GetPlayerId() => BaseData?.PlayerID ?? -1;
    }

    // --- Action Result Structure (Refined) ---
    /// <summary>Represents the outcome of a resolved player action or simulation event.</summary>
    public struct ActionResult
    {
        public ActionResultOutcome Outcome;
        public SimPlayer PrimaryPlayer;
        public SimPlayer SecondaryPlayer;
        public FoulSeverity FoulSeverity;
        public Vector2? ImpactPosition;
        public string Reason;
    }

    // --- Enums ---

    /// <summary>Possible outcomes of resolving a player action or simulation event.</summary>
    public enum ActionResultOutcome { Success, Failure, Intercepted, Saved, Blocked, Goal, Miss, FoulCommitted, OutOfBounds, Turnover }

    /// <summary>Severity levels for fouls.</summary>
     public enum FoulSeverity { None, FreeThrow, TwoMinuteSuspension, RedCard, OffensiveFoul }

    /// <summary>Possible primary actions a player can be performing.</summary>
    public enum PlayerAction { Idle, MovingToPosition, MovingWithBall, PreparingPass, ReceivingPass, PreparingShot, AttemptingTackle, AttemptingBlock, AttemptingIntercept, MarkingPlayer, ChasingBall, GoalkeeperPositioning, GoalkeeperSaving, Suspended, Fallen, GettingUp }

    /// <summary>Represents a logged event during the match simulation.</summary>
    public struct MatchEvent
    {
        public float TimeSeconds;
        public string Description;
        public int? TeamId;
        public int? PlayerId;

        public MatchEvent(float timeSeconds, string description, int? teamId = null, int? playerId = null)
        {
            TimeSeconds = timeSeconds;
            Description = description;
            TeamId = teamId;
            PlayerId = playerId;
        }

        public override string ToString()
        {
            float minutes = Mathf.Floor(TimeSeconds / 60f);
            float seconds = Mathf.Floor(TimeSeconds % 60f);
            return $"[{minutes:00}:{seconds:00}] {Description}";
        }
    }

    // Note: GamePhase enum definition removed from here. Assumed to be defined elsewhere
    // (e.g., MatchState.cs or Core.Enums.cs) and accessible via appropriate 'using' directive where needed.

}