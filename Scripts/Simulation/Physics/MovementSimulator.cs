using UnityEngine;
using HandballManager.Simulation.Core; // Added for SimConstants
using System;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Core.MatchData;
using System.Linq;

namespace HandballManager.Simulation.Physics
{
    public class MovementSimulator : IMovementSimulator

    {
        private readonly IGeometryProvider _geometry;

        public MovementSimulator(IGeometryProvider geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        // Movement and Physics Constants
        private const float PLAYER_ACCELERATION_BASE = 15.0f;  // Base acceleration m/s^2
        private const float PLAYER_DECELERATION_BASE = 20.0f;  // Base deceleration m/s^2
        private const float PLAYER_NEAR_STOP_VELOCITY_THRESHOLD = 0.5f;  // Speed below which accel limit is always used
        private const float MIN_DISTANCE_CHECK_SQ = 0.01f;  // Minimum squared distance for movement checks
        private const float PLAYER_MAX_SPEED_OVERSHOOT_FACTOR = 1.01f;  // Allowed overshoot before clamping

        // Attribute Modifiers
        private const float PLAYER_AGILITY_MOD_MIN = 0.8f;  // Effect of 0 Agility on accel/decel
        private const float PLAYER_AGILITY_MOD_MAX = 1.2f;  // Effect of 100 Agility on accel/decel

        // Boundary and Spacing Constants
        private const float SIDELINE_BUFFER = 0.5f;  // Buffer from sidelines for player and ball positions

        // Collision Constants
        private const float PLAYER_COLLISION_RADIUS = 0.4f;
        private const float PLAYER_COLLISION_DIAMETER = PLAYER_COLLISION_RADIUS * 2f;
        private const float PLAYER_COLLISION_DIAMETER_SQ = PLAYER_COLLISION_DIAMETER * PLAYER_COLLISION_DIAMETER;
        private const float COLLISION_RESPONSE_FACTOR = 0.5f;  // How strongly players push apart
        private const float COLLISION_MIN_DIST_SQ_CHECK = 0.0001f;  // Lower bound for collision distance check

        // Team Spacing Constants
        private const float MIN_SPACING_DISTANCE = 2.0f;  // How close teammates can get before spacing push
        private const float MIN_SPACING_DISTANCE_SQ = MIN_SPACING_DISTANCE * MIN_SPACING_DISTANCE;
        private const float SPACING_PUSH_FACTOR = 0.4f;
        private const float SPACING_PROXIMITY_POWER = 2.0f;  // Power for spacing push magnitude (higher = stronger when very close)

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
                ball.Position = new Vector3(ballPos2D.x, SimConstants.BALL_DEFAULT_HEIGHT, ballPos2D.y); // y → z mapping for 3D space
                ball.Velocity = Vector3.zero;
                ball.AngularVelocity = Vector3.zero;
            }
            else if (ball.IsInFlight)
            {
                // Apply physics simulation for ball in flight
                SimulateBallFlight(ball, deltaTime);
            }
            else if (ball.IsRolling)
            {
                // Handle rolling physics
                SimulateBallRolling(ball, deltaTime);
            }
        }

        private void SimulateBallFlight(SimBall ball, float deltaTime)
        {
            // --- Apply Forces (Air Resistance, Magnus, Gravity) ---
            Vector3 force = Vector3.zero;
            float speed = ball.Velocity.magnitude;

            // 1. Gravity
            force += SimConstants.GRAVITY * SimConstants.BALL_MASS;

            if (speed > SimConstants.FLOAT_EPSILON)
            {
                // 2. Air Resistance (Drag)
                float dragMagnitude = 0.5f * SimConstants.AIR_DENSITY * speed * speed * 
                                     SimConstants.DRAG_COEFFICIENT * SimConstants.BALL_CROSS_SECTIONAL_AREA;
                force += -ball.Velocity.normalized * dragMagnitude;

                // 3. Magnus Effect
                Vector3 magnusForce = SimConstants.MAGNUS_COEFFICIENT_SIMPLE * 
                                     Vector3.Cross(ball.AngularVelocity, ball.Velocity);
                force += magnusForce;
            }

            // Update physics
            ball.AngularVelocity *= Mathf.Pow(SimConstants.SPIN_DECAY_FACTOR, deltaTime);
            Vector3 acceleration = force / SimConstants.BALL_MASS;
            ball.Velocity += acceleration * deltaTime;
            ball.Position += ball.Velocity * deltaTime;

            // Ground collision check
            if (ball.Position.y <= SimConstants.BALL_RADIUS)
            {
                HandleBallGroundCollision(ball);
            }
        }

        private void HandleBallGroundCollision(SimBall ball)
        {
            ball.Position = new Vector3(ball.Position.x, SimConstants.BALL_RADIUS, ball.Position.z);
            Vector3 incomingVelocity = ball.Velocity;
            float vDotN = Vector3.Dot(incomingVelocity, Vector3.up);

            if (vDotN < 0)
            {
                Vector3 reflectedVelocity = incomingVelocity - 2 * vDotN * Vector3.up;
                reflectedVelocity *= SimConstants.COEFFICIENT_OF_RESTITUTION;

                Vector3 horizontalVelocity = new Vector3(reflectedVelocity.x, 0, reflectedVelocity.z);
                horizontalVelocity *= (1f - SimConstants.FRICTION_COEFFICIENT_SLIDING);

                ball.Velocity = new Vector3(horizontalVelocity.x, reflectedVelocity.y, horizontalVelocity.z);

                if (Mathf.Abs(ball.Velocity.y) < SimConstants.ROLLING_TRANSITION_VEL_Y_THRESHOLD)
                {
                    float horizontalSpeed = new Vector2(ball.Velocity.x, ball.Velocity.z).magnitude;
                    if (horizontalSpeed > SimConstants.ROLLING_TRANSITION_VEL_XZ_THRESHOLD)
                    {
                        ball.StartRolling(); // Start rolling
                        ball.Velocity = new Vector3(ball.Velocity.x, 0, ball.Velocity.z);
                    }
                    else
                    {
                        ball.Stop();
                    }
                }
            }
        }

        private void SimulateBallRolling(SimBall ball, float deltaTime)
        {
            Vector3 horizontalVelocity = new Vector3(ball.Velocity.x, 0, ball.Velocity.z);
            float horizontalSpeed = horizontalVelocity.magnitude;

            if (horizontalSpeed > SimConstants.FLOAT_EPSILON)
            {
                float frictionDeceleration = SimConstants.FRICTION_COEFFICIENT_ROLLING * SimConstants.EARTH_GRAVITY;
                float speedReduction = frictionDeceleration * deltaTime;
                float newSpeed = Mathf.Max(0, horizontalSpeed - speedReduction);

                if (newSpeed < SimConstants.ROLLING_TRANSITION_VEL_XZ_THRESHOLD)
                {
                    ball.Stop();
                }
                else
                {
                    ball.Velocity = horizontalVelocity.normalized * newSpeed;
                    ball.Position += ball.Velocity * deltaTime;
                    ball.Position = new Vector3(ball.Position.x, SimConstants.BALL_RADIUS, ball.Position.z);
                }
            }
            else
            {
                ball.Stop();
            }
        }

        /// <summary>
        /// Updates players' movement based on their current actions and targets.
        /// </summary>
        private void UpdatePlayersMovement(MatchState state, float deltaTime)
        {
            if(state.PlayersOnCourt == null) return;

            foreach (var player in state.PlayersOnCourt)
            {
                if (player == null || player.IsSuspended()) continue;

                Vector2 targetVelocity = CalculateActionTargetVelocity(player, state, out bool allowSprint, out bool applyArrivalSlowdown);
                ApplyAcceleration(player, targetVelocity, allowSprint, applyArrivalSlowdown, deltaTime);
                player.Position += player.Velocity * deltaTime;
                ApplyStaminaEffects(player, deltaTime);
            }
        }

        private Vector2 CalculateActionTargetVelocity(SimPlayer player, MatchState state, out bool allowSprint, out bool applyArrivalSlowdown)
        {
            Vector2 targetVelocity = Vector2.zero;
            allowSprint = false;
            applyArrivalSlowdown = true;

            if (player?.BaseData == null) return targetVelocity;

            switch (player.CurrentAction)
            {
                case PlayerAction.MovingToPosition:
                case PlayerAction.MovingWithBall:
                case PlayerAction.ChasingBall:
                case PlayerAction.MarkingPlayer:
                case PlayerAction.ReceivingPass:
                case PlayerAction.AttemptingIntercept:
                case PlayerAction.AttemptingBlock:
                case PlayerAction.GoalkeeperPositioning:
                    Vector2 direction = (player.TargetPosition - player.Position);
                    if (direction.sqrMagnitude > MIN_DISTANCE_CHECK_SQ)
                    {
                        targetVelocity = direction.normalized * player.EffectiveSpeed;
                    }
                    allowSprint = !player.IsGoalkeeper() && player.CurrentAction != PlayerAction.AttemptingBlock;
                    break;

                case PlayerAction.PreparingPass:
                case PlayerAction.PreparingShot:
                case PlayerAction.AttemptingTackle:
                    targetVelocity = Vector2.zero;
                    applyArrivalSlowdown = false;
                    break;

                default:
                    targetVelocity = Vector2.zero;
                    applyArrivalSlowdown = false;
                    break;
            }
            return targetVelocity;
        }

        private void ApplyAcceleration(SimPlayer player, Vector2 targetVelocity, bool allowSprint, bool applyArrivalSlowdown, float deltaTime)
        {
            if (player?.BaseData == null) return;

            Vector2 currentVelocity = player.Velocity;
            float currentSpeed = currentVelocity.magnitude;
            Vector2 directionToTarget = (player.TargetPosition - player.Position);
            float distanceToTarget = directionToTarget.magnitude;

            bool isSprinting = allowSprint &&
                              targetVelocity.sqrMagnitude > (player.EffectiveSpeed * SPRINT_TARGET_SPEED_FACTOR) * (player.EffectiveSpeed * SPRINT_TARGET_SPEED_FACTOR) &&
                              distanceToTarget > SPRINT_MIN_DISTANCE &&
                              player.Stamina > SPRINT_MIN_STAMINA;

            float finalTargetSpeed = targetVelocity.magnitude;

            if (isSprinting)
            {
                finalTargetSpeed = player.EffectiveSpeed;
            }
            else
            {
                finalTargetSpeed = Mathf.Min(finalTargetSpeed, player.EffectiveSpeed * NON_SPRINT_SPEED_CAP_FACTOR);
            }

            if (applyArrivalSlowdown && distanceToTarget < ARRIVAL_SLOWDOWN_RADIUS && distanceToTarget > ARRIVAL_SLOWDOWN_MIN_DIST)
            {
                finalTargetSpeed *= Mathf.Sqrt(Mathf.Clamp01(distanceToTarget / ARRIVAL_SLOWDOWN_RADIUS));
                isSprinting = false;
            }

            Vector2 finalTargetVelocity = (finalTargetSpeed > 0.01f) ? targetVelocity.normalized * finalTargetSpeed : Vector2.zero;
            Vector2 requiredAcceleration = (finalTargetVelocity - currentVelocity) / deltaTime;

            float agilityFactor = Mathf.Lerp(PLAYER_AGILITY_MOD_MIN, PLAYER_AGILITY_MOD_MAX, (player.BaseData?.Agility ?? 50f) / 100f);
            float maxAccel = PLAYER_ACCELERATION_BASE * agilityFactor;
            float maxDecel = PLAYER_DECELERATION_BASE * agilityFactor;

            bool isAccelerating = Vector2.Dot(requiredAcceleration, currentVelocity.normalized) > -0.1f || currentSpeed < PLAYER_NEAR_STOP_VELOCITY_THRESHOLD;
            float maxAccelerationMagnitude = isAccelerating ? maxAccel : maxDecel;

            Vector2 appliedAcceleration = Vector2.ClampMagnitude(requiredAcceleration, maxAccelerationMagnitude);
            player.Velocity += appliedAcceleration * deltaTime;

            // Clamp final velocity to prevent excessive speed
            float maxAllowedSpeed = player.EffectiveSpeed * PLAYER_MAX_SPEED_OVERSHOOT_FACTOR;
            if (player.Velocity.sqrMagnitude > maxAllowedSpeed * maxAllowedSpeed)
            {
                player.Velocity = player.Velocity.normalized * maxAllowedSpeed;
            }
        }

        private void ApplyStaminaEffects(SimPlayer player, float deltaTime)
        {
            if (player == null || player.BaseData == null) return;

            float currentEffort = player.EffectiveSpeed > 0.01f 
                ? player.Velocity.magnitude / player.EffectiveSpeed
                : 0f;
            bool isMovingSignificantly = currentEffort > SIGNIFICANT_MOVEMENT_EFFORT_THRESHOLD;
            bool isSprinting = currentEffort > SPRINT_MIN_EFFORT_THRESHOLD;

            float staminaDrain = 0f;
            if (isMovingSignificantly)
            {
                staminaDrain = STAMINA_DRAIN_BASE * deltaTime;
                if (isSprinting) staminaDrain *= STAMINA_SPRINT_MULTIPLIER;

                float staminaAttributeMod = 1f - (STAMINA_ATTRIBUTE_DRAIN_MOD * (player.BaseData.Stamina / 100f));
                staminaDrain *= staminaAttributeMod;
            }

            float staminaRecovery = 0f;
            if (!isMovingSignificantly)
            {
                float naturalFitnessMod = 1f + (NATURAL_FITNESS_RECOVERY_MOD * ((player.BaseData.NaturalFitness - 50f) / 50f));
                staminaRecovery = STAMINA_RECOVERY_RATE * naturalFitnessMod * deltaTime;
            }

            player.Stamina = Mathf.Clamp01(player.Stamina - staminaDrain + staminaRecovery);
            player.UpdateEffectiveSpeed(); // Always update, let the method handle thresholds
            
            // Remove the conditional speed reset here
        }

        /// <summary>
        /// Handles collisions and boundaries with proper single implementation
        /// </summary>
        private void HandleCollisionsAndBoundaries(MatchState state, float deltaTime)
        {
            if (state?.PlayersOnCourt == null) return;

            var players = state.PlayersOnCourt;

            // Handle player-player collisions and team spacing
            for (int i = 0; i < players.Count; i++)
            {
                var player1 = players[i];
                if (player1 == null) continue;

                for (int j = i + 1; j < players.Count; j++)
                {
                    var player2 = players[j];
                    if (player2 == null) continue;

                    Vector2 separation = player1.Position - player2.Position;
                    float distanceSq = separation.sqrMagnitude;

                    // Handle collision
                    if (distanceSq < PLAYER_COLLISION_DIAMETER_SQ && distanceSq > COLLISION_MIN_DIST_SQ_CHECK)
                    {
                        float distance = Mathf.Sqrt(distanceSq);
                        Vector2 separationDir = separation / distance;

                        float overlap = PLAYER_COLLISION_DIAMETER - distance;
                        Vector2 responseVector = separationDir * overlap * COLLISION_RESPONSE_FACTOR;

                        player1.Position += responseVector;
                        player2.Position -= responseVector;

                        // Add velocity response (conservation of momentum)
                        Vector2 relativeVelocity = player1.Velocity - player2.Velocity;
                        float velAlongNormal = Vector2.Dot(relativeVelocity, separationDir);
                        
                        if (velAlongNormal > 0) // Only if moving towards each other
                        {
                            // In collision response
                            float p1Mass = 1.0f + ((player1.BaseData?.Strength ?? 50f) / 200f);
                            float p2Mass = 1.0f + ((player2.BaseData?.Strength ?? 50f) / 200f);
                            float totalMassInv = 1.0f / (p1Mass + p2Mass);
                            float impulse = velAlongNormal / (1/player1Mass + 1/player2Mass);
                            Vector2 impulseVector = separationDir * impulse;
                            
                            player1.Velocity -= impulseVector / player1Mass;
                            player2.Velocity += impulseVector / player2Mass;
                        }
                    }

                    // Handle team spacing
                    if (player1.TeamSimId == player2.TeamSimId && distanceSq < MIN_SPACING_DISTANCE_SQ)
                    {
                        float spacingStrength = 1f - (distanceSq / MIN_SPACING_DISTANCE_SQ);
                        spacingStrength = Mathf.Pow(spacingStrength, SPACING_PROXIMITY_POWER);
                        Vector2 spacingForce = separation.normalized * spacingStrength * SPACING_PUSH_FACTOR;

                        player1.Position += spacingForce;
                        player2.Position -= spacingForce;
                    }
                }
            }

            // Boundary clamping for players
            foreach (var player in players)
            {
                if (player == null) continue;
                // In boundary clamping
                float maxX = (_geometry?.PitchLength ?? SimConstants.DEFAULT_PITCH_LENGTH) - SIDELINE_BUFFER;
                float maxY = (_geometry?.PitchWidth ?? SimConstants.DEFAULT_PITCH_WIDTH) - SIDELINE_BUFFER;
                player.Position = new Vector2(
                    Mathf.Clamp(player.Position.x, SIDELINE_BUFFER, maxX),
                    Mathf.Clamp(player.Position.y, SIDELINE_BUFFER, maxY)
                );
            }

            // Ball boundary clamping
            if (state.Ball != null)
            {
                SimBall ball = state.Ball;
                float ballMaxX = (_geometry?.PitchLength ?? SimConstants.DEFAULT_PITCH_LENGTH) - SIDELINE_BUFFER;
                float ballMaxZ = (_geometry?.PitchWidth ?? SimConstants.DEFAULT_PITCH_WIDTH) - SIDELINE_BUFFER;
                ball.Position = new Vector3(
                    Mathf.Clamp(ball.Position.x, SIDELINE_BUFFER, ballMaxX),
                    ball.Position.y,
                    Mathf.Clamp(ball.Position.z, SIDELINE_BUFFER, ballMaxZ)
                );
            }
        }

        /// <summary>
        /// Interface implementation with single method for enforcing boundaries
        /// </summary>
        public void EnforceBoundaries(MatchState state)
        {
            if (state == null) return;
            HandleCollisionsAndBoundaries(state, 0);
        }
    }
}