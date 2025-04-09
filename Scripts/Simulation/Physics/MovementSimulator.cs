using UnityEngine;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Core; // Added for SimConstants
using HandballManager.Simulation.Common; // Added for IGeometryProvider
using System.Collections.Generic;
using System;

namespace HandballManager.Simulation.Physics
{
    public class MovementSimulator : IMovementSimulator
    {
        private readonly IGeometryProvider _geometry; // Added field

        public MovementSimulator(IGeometryProvider geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        // Player Movement Constants (mostly unchanged)

        private const float PLAYER_ACCELERATION_BASE = 15.0f; // Base acceleration m/s^2
        private const float PLAYER_DECELERATION_BASE = 20.0f; // Base deceleration m/s^2
        private const float PLAYER_AGILITY_MOD_MIN = 0.8f;    // Effect of 0 Agility on accel/decel
        private const float PLAYER_AGILITY_MOD_MAX = 1.2f;    // Effect of 100 Agility on accel/decel
        private const float PLAYER_MAX_SPEED_OVERSHOOT_FACTOR = 1.01f; // Allowed overshoot before clamping
        private const float PLAYER_NEAR_STOP_VELOCITY_THRESHOLD = 0.5f; // Speed below which accel limit is always used

        private const float PLAYER_COLLISION_RADIUS = 0.4f;
        private const float PLAYER_COLLISION_DIAMETER = PLAYER_COLLISION_RADIUS * 2f;
        private const float PLAYER_COLLISION_DIAMETER_SQ = PLAYER_COLLISION_DIAMETER * PLAYER_COLLISION_DIAMETER;
        private const float COLLISION_RESPONSE_FACTOR = 0.5f; // How strongly players push apart
        private const float COLLISION_MIN_DIST_SQ_CHECK = 0.0001f; // Lower bound for collision distance check

        private const float MIN_SPACING_DISTANCE = 2.0f; // How close teammates can get before spacing push
        private const float MIN_SPACING_DISTANCE_SQ = MIN_SPACING_DISTANCE * MIN_SPACING_DISTANCE;
        private const float SPACING_PUSH_FACTOR = 0.4f;
        private const float SPACING_PROXIMITY_POWER = 2.0f; // Power for spacing push magnitude (higher = stronger when very close)

        // Stamina Constants
        private const float STAMINA_DRAIN_BASE = MatchSimulator.BASE_STAMINA_DRAIN_PER_SECOND;
        private const float STAMINA_SPRINT_MULTIPLIER = MatchSimulator.SPRINT_STAMINA_MULTIPLIER;
        private const float STAMINA_RECOVERY_RATE = 0.003f;
        private const float NATURAL_FITNESS_RECOVERY_MOD = 0.2f; // +/- 20% effect on recovery rate based on 0-100 NF (0 = 0.8x, 100 = 1.2x)
        private const float STAMINA_ATTRIBUTE_DRAIN_MOD = 0.3f; // +/- 30% effect on drain rate based on 0-100 Stamina (0=1.3x, 100=0.7x)
        private const float STAMINA_LOW_THRESHOLD = SimConstants.PLAYER_STAMINA_LOW_THRESHOLD; // Use constant
        private const float STAMINA_MIN_SPEED_FACTOR = SimConstants.PLAYER_STAMINA_MIN_SPEED_FACTOR; // Use constant
        private const float SPRINT_MIN_EFFORT_THRESHOLD = 0.85f; // % of BASE max speed considered sprinting
        private const float SIGNIFICANT_MOVEMENT_EFFORT_THRESHOLD = 0.2f; // % of BASE max speed considered 'moving' for stamina drain

        // Sprinting / Arrival Constants
        private const float SPRINT_MIN_DISTANCE = 3.0f;
        private const float SPRINT_MIN_STAMINA = 0.3f;
        private const float SPRINT_TARGET_SPEED_FACTOR = 0.6f; // Must be trying to move faster than this % of effective speed to sprint
        private const float NON_SPRINT_SPEED_CAP_FACTOR = 0.85f; // % cap on effective speed when not sprinting
        private const float ARRIVAL_SLOWDOWN_RADIUS = 1.5f;
        private const float ARRIVAL_SLOWDOWN_MIN_DIST = 0.05f; // Min distance for slowdown logic to apply
        private const float ARRIVAL_DAMPING_FACTOR = 0.5f; // Velocity multiplier when arriving at target


        /// <summary>
        /// Main update entry point called by MatchSimulator. Updates ball and player movement, handles collisions.
        /// </summary>
        public void UpdateMovement(MatchState state, float deltaTime)
        {
            // Safety check for essential state
            if (state == null || state.Ball == null) {
                Debug.LogError("[MovementSimulator] UpdateMovement called with null state or ball.");
                return;
            }

            UpdateBallMovement(state, deltaTime);
            UpdatePlayersMovement(state, deltaTime);
            HandleCollisionsAndBoundaries(state, deltaTime); // Handles player-player, spacing, and boundary clamping
        }

        /// <summary>
        /// Updates the ball's 3D position and velocity based on physics.
        /// Applies gravity, air resistance, Magnus effect, and handles ground interactions.
        /// </summary>
        private void UpdateBallMovement(MatchState state, float deltaTime)
        {
            SimBall ball = state.Ball;

            if (ball.Holder != null)
            {
                // Ball stays attached to the holder on the ground plane
                Vector2 playerPos2D = ball.Holder.Position;
                Vector2 offsetDir2D = Vector2.right * (ball.Holder.TeamSimId == 0 ? 1f : -1f);
                if (ball.Holder.Velocity.sqrMagnitude > SimConstants.VELOCITY_NEAR_ZERO_SQ) {
                    offsetDir2D = ball.Holder.Velocity.normalized;
                }
                Vector2 ballPos2D = playerPos2D + offsetDir2D * SimConstants.BALL_OFFSET_FROM_HOLDER;
                ball.Position = new Vector3(ballPos2D.x, SimConstants.BALL_DEFAULT_HEIGHT, ballPos2D.y);
                ball.Velocity = Vector3.zero;
                ball.AngularVelocity = Vector3.zero;
            }
            else if (ball.IsInFlight)
            {
                // --- Apply Forces (Air Resistance, Magnus, Gravity) ---
                Vector3 force = Vector3.zero;
                float speed = ball.Velocity.magnitude;

                // 1. Gravity
                force += SimConstants.GRAVITY * SimConstants.BALL_MASS;

                if (speed > SimConstants.FLOAT_EPSILON)
                {
                    // 2. Air Resistance (Drag) F = -0.5 * rho * v^2 * Cd * A * v_normalized
                    float dragMagnitude = 0.5f * SimConstants.AIR_DENSITY * speed * speed * 
                                         SimConstants.DRAG_COEFFICIENT * SimConstants.BALL_CROSS_SECTIONAL_AREA;
                    force += -ball.Velocity.normalized * dragMagnitude;

                    // 3. Magnus Effect (Simplified) F = k * cross(spin, velocity)
                    Vector3 magnusForce = SimConstants.MAGNUS_COEFFICIENT_SIMPLE * 
                                         Vector3.Cross(ball.AngularVelocity, ball.Velocity);
                    force += magnusForce;
                }

                // Decay Angular Velocity (Spin) over time
                ball.AngularVelocity *= Mathf.Pow(SimConstants.SPIN_DECAY_FACTOR, deltaTime);

                // Apply force to get acceleration (F = ma, so a = F/m)
                Vector3 acceleration = force / SimConstants.BALL_MASS;

                // Update velocity using acceleration
                ball.Velocity += acceleration * deltaTime;

                // Update position using velocity
                ball.Position += ball.Velocity * deltaTime;

                // --- Ground Collision Check ---
                if (ball.Position.y <= SimConstants.BALL_RADIUS) // Ball hit the ground
                {
                    // Correct position to be exactly at ground level
                    ball.Position = new Vector3(ball.Position.x, SimConstants.BALL_RADIUS, ball.Position.z);

                    // Handle bounce physics
                    Vector3 incomingVelocity = ball.Velocity;
                    Vector3 normal = Vector3.up; // Ground normal points up

                    // Reflect velocity with energy loss (coefficient of restitution)
                    float vDotN = Vector3.Dot(incomingVelocity, normal);
                    if (vDotN < 0) // Only bounce if moving toward the surface
                    {
                        // Reflect velocity with energy loss
                        Vector3 reflectedVelocity = incomingVelocity - 2 * vDotN * normal;
                        reflectedVelocity *= SimConstants.COEFFICIENT_OF_RESTITUTION;

                        // Apply horizontal friction during bounce
                        Vector3 horizontalVelocity = new Vector3(reflectedVelocity.x, 0, reflectedVelocity.z);
                        horizontalVelocity *= (1f - SimConstants.FRICTION_COEFFICIENT_SLIDING);

                        // Recombine velocities
                        ball.Velocity = new Vector3(horizontalVelocity.x, reflectedVelocity.y, horizontalVelocity.z);

                        // Check if ball should transition to rolling state
                        if (Mathf.Abs(ball.Velocity.y) < SimConstants.ROLLING_TRANSITION_VEL_Y_THRESHOLD)
                        {
                            float horizontalSpeed = new Vector2(ball.Velocity.x, ball.Velocity.z).magnitude;
                            if (horizontalSpeed > SimConstants.ROLLING_TRANSITION_VEL_XZ_THRESHOLD)
                            {
                                // Transition to rolling state
                                ball.IsInFlight = false;
                                ball.IsRolling = true;
                                ball.Velocity = new Vector3(ball.Velocity.x, 0, ball.Velocity.z); // Zero out vertical velocity
                            }
                            else
                            {
                                // Ball stops completely
                                ball.Stop();
                            }
                        }
                    }
                }
            }
            else if (ball.IsRolling)
            {
                // Handle rolling physics - apply rolling friction
                Vector3 horizontalVelocity = new Vector3(ball.Velocity.x, 0, ball.Velocity.z);
                float horizontalSpeed = horizontalVelocity.magnitude;

                if (horizontalSpeed > SimConstants.FLOAT_EPSILON)
                {
                    // Apply rolling friction (proportional to velocity)
                    float frictionDeceleration = SimConstants.FRICTION_COEFFICIENT_ROLLING * 9.81f; // g * μ
                    float speedReduction = frictionDeceleration * deltaTime;
                    
                    // Ensure we don't reverse direction due to excessive deceleration
                    float newSpeed = Mathf.Max(0, horizontalSpeed - speedReduction);
                    
                    if (newSpeed < SimConstants.ROLLING_TRANSITION_VEL_XZ_THRESHOLD)
                    {
                        // Ball stops rolling
                        ball.Stop();
                    }
                    else
                    {
                        // Update velocity with friction applied
                        ball.Velocity = horizontalVelocity.normalized * newSpeed;
                        
                        // Update position
                        ball.Position += ball.Velocity * deltaTime;
                        
                        // Ensure ball stays at correct height while rolling
                        ball.Position = new Vector3(ball.Position.x, SimConstants.BALL_RADIUS, ball.Position.z);
                    }
                }
                else
                {
                    // Ball has effectively stopped
                    ball.Stop();
                }
            }
            // Else: Ball is loose but not moving - no physics update needed
        }

        /// <summary>
        /// Iterates through all players on the court and updates their movement based on current action, target,
        /// acceleration limits, and stamina.
        /// </summary>
        private void UpdatePlayersMovement(MatchState state, float deltaTime)
        {
            // Removed ToList() as we are not modifying the list structure here, only player properties.
            if(state.PlayersOnCourt == null) return; // Safety check

            foreach (var player in state.PlayersOnCourt)
            {
                 if (player == null) continue; // Safety check for player object

                if (player.IsSuspended()) {
                     player.Velocity = Vector2.zero; // Ensure no movement while suspended
                     continue;
                }

                // 1. Determine Target Velocity based on Current Action
                Vector2 targetVelocity = CalculateActionTargetVelocity(player, state, out bool allowSprint, out bool applyArrivalSlowdown);

                // 2. Apply Acceleration/Deceleration towards Target Velocity
                ApplyAcceleration(player, targetVelocity, allowSprint, applyArrivalSlowdown, deltaTime);

                // 3. Update Position based on new Velocity
                player.Position += player.Velocity * deltaTime;

                // 4. Apply Stamina Effects based on exertion
                ApplyStaminaEffects(player, deltaTime);
            }
        }

        /// <summary>
        /// Determines the target velocity vector based on the player's current action.
        /// Also outputs flags indicating if sprinting or arrival slowdown should be considered.
        /// </summary>
        private Vector2 CalculateActionTargetVelocity(SimPlayer player, MatchState state, out bool allowSprint, out bool applyArrivalSlowdown)
        {
             // Default values
             Vector2 targetVelocity = Vector2.zero;
             allowSprint = false;
             applyArrivalSlowdown = true;

             // Safety check for player data needed for effective speed calculation
             if (player?.BaseData == null) return targetVelocity; // Return zero velocity if no data

             switch (player.CurrentAction)
             {
                 // States involving targeted movement towards TargetPosition
                 case PlayerAction.MovingToPosition:
                 case PlayerAction.MovingWithBall:
                 case PlayerAction.ChasingBall:
                 case PlayerAction.MarkingPlayer: // May involve bursts of speed
                 case PlayerAction.ReceivingPass: // Move towards estimated intercept point
                 case PlayerAction.AttemptingIntercept: // Move aggressively towards intercept point
                 case PlayerAction.AttemptingBlock: // Move towards block spot
                 case PlayerAction.GoalkeeperPositioning: // GK movement towards target
                      Vector2 direction = (player.TargetPosition - player.Position);
                      if (direction.sqrMagnitude > MIN_DISTANCE_CHECK_SQ) { // Only move if not already at target
                           targetVelocity = direction.normalized * player.EffectiveSpeed; // Target max effective speed
                      }
                      // Sprinting generally allowed for field players during active movement
                      allowSprint = !player.IsGoalkeeper() && player.CurrentAction != PlayerAction.AttemptingBlock;
                     break;

                 // Actions implying being stationary or very specific small movements
                 case PlayerAction.PreparingPass:
                 case PlayerAction.PreparingShot:
                 case PlayerAction.AttemptingTackle: // Rooted during action timer
                      targetVelocity = Vector2.zero;
                      applyArrivalSlowdown = false; // No slowdown needed if target is zero velocity
                      break;

                 // Default: Stop or idle behavior
                 case PlayerAction.Idle:
                 case PlayerAction.Suspended: // Already handled
                 default:
                     targetVelocity = Vector2.zero; // Target is to stop
                     applyArrivalSlowdown = false;
                     break;
             }
             return targetVelocity;
        }


        /// <summary>
        /// Modifies player velocity based on acceleration/deceleration limits towards a target velocity.
        /// Incorporates sprinting logic and arrival slowdown. Uses player Agility attribute.
        /// </summary>
        private void ApplyAcceleration(SimPlayer player, Vector2 targetVelocity, bool allowSprint, bool applyArrivalSlowdown, float deltaTime)
        {
             // Safety check
             if (player?.BaseData == null) return;

             Vector2 currentVelocity = player.Velocity;
             float currentSpeed = currentVelocity.magnitude;
             Vector2 directionToTarget = (player.TargetPosition - player.Position);
             float distanceToTarget = directionToTarget.magnitude;

             // --- Determine if Sprinting ---
             bool isSprinting = allowSprint &&
                                targetVelocity.sqrMagnitude > (player.EffectiveSpeed * SPRINT_TARGET_SPEED_FACTOR) * (player.EffectiveSpeed * SPRINT_TARGET_SPEED_FACTOR) && // Trying to move fast
                                distanceToTarget > SPRINT_MIN_DISTANCE && // Far enough from target
                                player.Stamina > SPRINT_MIN_STAMINA; // Enough stamina

             // --- Adjust Target Speed based on Sprinting & Arrival ---
             float finalTargetSpeed = targetVelocity.magnitude; // Start with the speed implied by targetVelocity vector

             if (isSprinting) {
                  finalTargetSpeed = player.EffectiveSpeed; // Sprint at max current effective speed
             } else {
                  // Cap non-sprint speed slightly below max effective speed
                  finalTargetSpeed = Mathf.Min(finalTargetSpeed, player.EffectiveSpeed * NON_SPRINT_SPEED_CAP_FACTOR);
             }

             // Apply arrival slowdown if getting close to target
             if (applyArrivalSlowdown && distanceToTarget < ARRIVAL_SLOWDOWN_RADIUS && distanceToTarget > ARRIVAL_SLOWDOWN_MIN_DIST)
             {
                  // Scale speed based on remaining distance (sqrt for smoother slowdown)
                  finalTargetSpeed *= Mathf.Sqrt(Mathf.Clamp01(distanceToTarget / ARRIVAL_SLOWDOWN_RADIUS));
                  isSprinting = false; // Force stop sprinting when arriving
             }

             // Recalculate final target velocity vector with the adjusted speed
             Vector2 finalTargetVelocity = (finalTargetSpeed > 0.01f) ? targetVelocity.normalized * finalTargetSpeed : Vector2.zero;

             // --- Calculate Required and Applied Acceleration ---
             Vector2 requiredAcceleration = (finalTargetVelocity - currentVelocity) / deltaTime; // Ideal acceleration needed

             // Modify max accel/decel based on Agility
             float agilityFactor = Mathf.Lerp(PLAYER_AGILITY_MOD_MIN, PLAYER_AGility_MOD_MAX, (player.BaseData?.Agility ?? 50f) / 100f); // Use default 50 if BaseData null
             float maxAccel = PLAYER_ACCELERATION_BASE * agilityFactor;
             float maxDecel = PLAYER_DECELERATION_BASE * agilityFactor;

             // Determine if accelerating (increasing speed or changing direction significantly) or decelerating
             bool isAccelerating = Vector2.Dot(requiredAcceleration, currentVelocity.normalized) > -0.1f || currentSpeed < PLAYER_NEAR_STOP_VELOCITY_THRESHOLD; // Accelerating if vectors align, or nearly stopped
             float maxAccelerationMagnitude = isAccelerating ? maxAccel : maxDecel;

             // Clamp the applied acceleration vector magnitude
             Vector2 appliedAcceleration = Vector2.ClampMagnitude(requiredAcceleration, maxAccelerationMagnitude);

             // --- Update Velocity ---
             player.Velocity += appliedAcceleration * deltaTime;

             // --- Hard Speed Limit Check ---
             // Prevent exceeding max effective speed if accelerating in that direction
             float maxSpeedSq = player.EffectiveSpeed * player.EffectiveSpeed;
             if (player.Velocity.sqrMagnitude > maxSpeedSq * PLAYER_MAX_SPEED_OVERSHOOT_FACTOR && Vector2.Dot(player.Velocity, appliedAcceleration) > 0)
             {
                 // Clamp velocity magnitude to player's current effective speed
                 player.Velocity = player.Velocity.normalized * player.EffectiveSpeed;
             }

             // --- NaN Check --- (Important safety check)
             if (float.IsNaN(player.Velocity.x) || float.IsNaN(player.Velocity.y))
             {
                player.Velocity = Vector2.zero; // Reset to zero if NaN occurs
             }
        }


        /// <summary>
        /// Applies stamina drain or recovery based on player's current exertion level (speed relative to max possible speed).
        /// Recalculates the player's effective speed based on the new stamina level.
        /// Uses Natural Fitness for recovery and Stamina attribute for drain resistance.
        /// Incorporates the NATURAL_FITNESS_RECOVERY_MOD constant.
        /// </summary>
        private void ApplyStaminaEffects(SimPlayer player, float deltaTime)
        {
            // Safety check
            if (player?.BaseData == null) return;

            float currentSpeed = player.Velocity.magnitude;
            // Base max speed before stamina effects
            float baseMaxSpeed = (player.BaseData.Speed / 100f) * MatchSimulator.MAX_PLAYER_SPEED;
            if (baseMaxSpeed < 0.1f) baseMaxSpeed = 0.1f; // Avoid division by zero

            float relativeEffort = Mathf.Clamp01(currentSpeed / baseMaxSpeed); // Effort relative to potential max speed
            bool isMovingSignificantly = relativeEffort > SIGNIFICANT_MOVEMENT_EFFORT_THRESHOLD;
            bool isSprinting = relativeEffort > SPRINT_MIN_EFFORT_THRESHOLD;

            float staminaChange = 0f;

            if (isMovingSignificantly)
            {
                 // --- Stamina Drain ---
                 // Higher Stamina attribute reduces drain
                 float staminaAttributeFactor = Mathf.Lerp(1.0f + STAMINA_ATTRIBUTE_DRAIN_MOD, 1.0f - STAMINA_ATTRIBUTE_DRAIN_MOD, (player.BaseData?.Stamina ?? 50f) / 100f); // Use ?? default
                 float intensityFactor = relativeEffort; // Drain scales with effort
                 float sprintMultiplier = isSprinting ? STAMINA_SPRINT_MULTIPLIER : 1.0f;

                 staminaChange = -STAMINA_DRAIN_BASE * intensityFactor * sprintMultiplier * staminaAttributeFactor * deltaTime;
            }
            else // Idle or moving very slowly
            {
                // --- Stamina Recovery ---
                // Higher Natural Fitness increases recovery rate
                float nf = player.BaseData?.NaturalFitness ?? 50f; // Use ?? default
                float recoveryFactor = Mathf.Lerp(1.0f - NATURAL_FITNESS_RECOVERY_MOD, 1.0f + NATURAL_FITNESS_RECOVERY_MOD, nf / 100f); // Apply the modifier correctly
                staminaChange = STAMINA_RECOVERY_RATE * recoveryFactor * deltaTime;
            }

            // Apply change and update effective speed
            player.Stamina = Mathf.Clamp01(player.Stamina + staminaChange);
            player.UpdateEffectiveSpeed(); // Recalculate based on new stamina
        }

        /// <summary>
        /// Handles player-player collisions, teammate spacing adjustments,
        /// and clamps all player/ball positions to pitch boundaries.
        /// Iterates directly over state.PlayersOnCourt assuming no structural changes during this step.
        /// Collision/Spacing complexity is O(N^2) per team, acceptable for small N.
        /// Future Improvement: Could use spatial partitioning (e.g., grids) if N becomes large.
        /// </summary>
        private void HandleCollisionsAndBoundaries(MatchState state, float deltaTime)
        {
             // Safety check for player list
             if (state.PlayersOnCourt == null) return;

             // Iterate directly over the list - Modification of player.Position/Velocity is safe.
             // Removal/Addition during this loop would require iterating a copy (ToList).
             var players = state.PlayersOnCourt;

             // --- Player-Player Collision & Spacing ---
             for (int i = 0; i < players.Count; i++)
             {
                 SimPlayer p1 = players[i];
                 if (p1 == null || p1.IsSuspended()) continue; // Null & suspended check

                 Vector2 totalPush = Vector2.zero; // Accumulate push vectors for p1

                 // Check against other players (Collision & Teammate Spacing)
                 for (int j = i + 1; j < players.Count; j++) // Avoid self-comparison and double checks
                 {
                     SimPlayer p2 = players[j];
                     if (p2 == null || p2.IsSuspended()) continue; // Null & suspended check

                     Vector2 axis = p1.Position - p2.Position;
                     float distSq = axis.sqrMagnitude;

                     // Collision Response (Opponents & Teammates)
                     if (distSq < PLAYER_COLLISION_DIAMETER_SQ && distSq > COLLISION_MIN_DIST_SQ_CHECK)
                     {
                         float overlap = PLAYER_COLLISION_DIAMETER - Mathf.Sqrt(distSq);
                         // Use normalized vector only if distance is non-trivial
                         Vector2 collisionNormal = (distSq > 0.0001f) ? axis.normalized : new Vector2(1, 0); // Default push direction if perfectly overlapped
                         float pushMagnitude = overlap * COLLISION_RESPONSE_FACTOR;

                         // Simple mass approximation based on strength (safer BaseData access)
                         float p1Mass = 1.0f + ((p1.BaseData?.Strength ?? 50f) / 200f);
                         float p2Mass = 1.0f + ((p2.BaseData?.Strength ?? 50f) / 200f);
                         float totalMassInv = 1.0f / (p1Mass + p2Mass + 0.01f); // Precompute inverse

                         // Apply push to both players immediately for simplicity (can cause slight oscillations)
                         Vector2 pushVector = collisionNormal * pushMagnitude;
                         p1.Position += pushVector * (p2Mass * totalMassInv);
                         p2.Position -= pushVector * (p1Mass * totalMassInv); // Apply equal and opposite push to p2
                     }
                 }

                 // Teammate Spacing (Apply cumulative push at the end for p1)
                 // Re-iterate only through teammates needed if separate logic desired, but collision loop handled basics.
                 // For simplicity, spacing could be implicitly handled by the general collision response above
                 // or added here as an additional outward push if within MIN_SPACING_DISTANCE.

                 /* // Example separate spacing push logic (could be redundant with collision):
                 foreach(var teammate in state.GetTeamOnCourt(p1.TeamSimId)) {
                     if (teammate == p1 || teammate == null || teammate.IsSuspended()) continue;
                     Vector2 vecToTeammate = p1.Position - teammate.Position;
                     float distSq = vecToTeammate.sqrMagnitude;
                     if (distSq < MIN_SPACING_DISTANCE_SQ && distSq > COLLISION_MIN_DIST_SQ_CHECK) {
                          float distance = Mathf.Sqrt(distSq);
                          float proximityFactor = Mathf.Pow(1f - distance / MIN_SPACING_DISTANCE, SPACING_PROXIMITY_POWER);
                          Vector2 pushDir = (distSq > 0.0001f) ? vecToTeammate.normalized : Vector2.right;
                          totalPush += pushDir * proximityFactor * SPACING_PUSH_FACTOR;
                     }
                 }
                 p1.Position += totalPush; // Apply cumulative spacing push
                 */
            }

            // Updated boundary clamping
            foreach (var player in players)
            {
                if (player == null) continue;
                player.Position.x = Mathf.Clamp(player.Position.x, SIDELINE_BUFFER, _geometry.Length - SIDELINE_BUFFER);
                player.Position.y = Mathf.Clamp(player.Position.y, SIDELINE_BUFFER, _geometry.Width - SIDELINE_BUFFER);
            }

            // Updated ball clamping
            SimBall ball = state.Ball;
            ball.Position = new Vector3(
                Mathf.Clamp(ball.Position.x, -boundaryBuffer, _geometry.Length + boundaryBuffer),
                ball.Position.y,
                Mathf.Clamp(ball.Position.z, -boundaryBuffer, _geometry.Width + boundaryBuffer)
            );
        }
    }
}


        /// <summary>
        /// Updates the ball's physics based on current state.
        /// Implementation of IMovementSimulator.UpdateBallPhysics
        /// </summary>
        public void UpdateBallPhysics(MatchState state, float timeStep)
        {
            // Call the existing implementation
            UpdateBallMovement(state, timeStep);
        }

        /// <summary>
        /// Resolves collisions between players and between players and boundaries.
        /// Implementation of IMovementSimulator.ResolveCollisions
        /// </summary>
        public void ResolveCollisions(MatchState state)
        {
            // Call the existing implementation with a minimal time step
            HandleCollisionsAndBoundaries(state, 0.01f);
        }