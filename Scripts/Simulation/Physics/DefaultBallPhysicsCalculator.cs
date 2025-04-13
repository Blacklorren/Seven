using UnityEngine;
using HandballManager.Simulation.Core; // Added for SimConstants
using System;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Core.MatchData;
using UnityEngine.LowLevelPhysics;

namespace HandballManager.Simulation.Physics
{
    public class DefaultBallPhysicsCalculator : IBallPhysicsCalculator
    {
        private readonly IGeometryProvider _geometry;
        private readonly Vector3 _gravity;
        private readonly Vector3 _up;
        private readonly Vector3 _zero;

        public DefaultBallPhysicsCalculator(IGeometryProvider geometry)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (geometry.PitchLength <= 0) throw new ArgumentException("Pitch length must be positive", nameof(geometry));
            if (geometry.PitchWidth <= 0) throw new ArgumentException("Pitch width must be positive", nameof(geometry));

            _geometry = geometry;
            _gravity = SimConstants.EARTH_GRAVITY * Vector3.down;
            _up = Vector3.up;
            _zero = Vector3.zero;
        }

        /// <summary>
        /// Updates ball physics state based on holder status and flight/rolling conditions
        /// </summary>
        /// <param name="ball">The ball to update</param>
        /// <param name="deltaTime">Time step for physics calculation</param>
        /// <remarks>Handles ball movement when held by player, in flight, or rolling on ground</remarks>
        public void UpdateBallMovement(SimBall ball, float deltaTime)
        {
            if (ball == null)
            {
                Debug.LogError("UpdateBallMovement called with null ball");
                return;
            }

            if (deltaTime < 0 || float.IsNaN(deltaTime))
            {
                Debug.LogError($"UpdateBallMovement called with invalid deltaTime: {deltaTime}");
                return;
            }

            try
            {
                if (ball.Holder != null)
                {
                    Vector2 playerPos2D = ball.Holder.Position;
                    Vector2 offsetDir2D = Vector2.right * (ball.Holder.TeamSimId == SimConstants.HOME_TEAM_ID ? 1f : -1f);
                    if (ball.Holder.Velocity.sqrMagnitude > SimConstants.VELOCITY_NEAR_ZERO_SQ) { offsetDir2D = ball.Holder.Velocity.normalized; }
                    Vector2 ballPos2D = playerPos2D + offsetDir2D * SimConstants.BALL_OFFSET_FROM_HOLDER;
                    ball.Position = new Vector3(ballPos2D.x, SimConstants.BALL_DEFAULT_HEIGHT, ballPos2D.y);
                    ball.Velocity = _zero;
                    ball.AngularVelocity = _zero;
                }
                else if (ball.IsInFlight)
                {
                    // Check for invalid velocity or position
                    if (float.IsNaN(ball.Velocity.x) || float.IsNaN(ball.Velocity.y) || float.IsNaN(ball.Velocity.z) ||
                        float.IsNaN(ball.Position.x) || float.IsNaN(ball.Position.y) || float.IsNaN(ball.Position.z))
                    {
                        Debug.LogError("Ball has NaN position or velocity during flight");
                        ball.Stop();
                        return;
                    }

                    Vector3 force = _zero;
                    float speed = ball.Velocity.magnitude;
                    force += _gravity * SimConstants.BALL_MASS;
                    if (speed > SimConstants.FLOAT_EPSILON)
                    {
                        float dragMagnitude = 0.5f * SimConstants.AIR_DENSITY * speed * speed * SimConstants.DRAG_COEFFICIENT * SimConstants.BALL_CROSS_SECTIONAL_AREA;
                        force += -ball.Velocity.normalized * dragMagnitude;
                        Vector3 magnusForce = SimConstants.MAGNUS_COEFFICIENT_SIMPLE * Vector3.Cross(ball.AngularVelocity, ball.Velocity);
                        force += magnusForce;
                    }
                    ball.AngularVelocity *= Mathf.Pow(SimConstants.SPIN_DECAY_FACTOR, deltaTime);
                    Vector3 acceleration = force / SimConstants.BALL_MASS;
                    ball.Velocity += acceleration * deltaTime;
                    ball.Position += ball.Velocity * deltaTime;

                    if (ball.Position.y <= SimConstants.BALL_RADIUS)
                    {
                        // Ball has hit the ground - handle bounce or transition to rolling
                        ball.Position = new Vector3(ball.Position.x, SimConstants.BALL_RADIUS, ball.Position.z);
                        Vector3 incomingVelocity = ball.Velocity;
                        Vector3 normal = _up;
                        float vDotN = Vector3.Dot(incomingVelocity, normal);

                        if (vDotN < 0) // Ball is moving toward the ground
                        {
                            // Calculate reflected velocity with energy loss
                            Vector3 reflectedVelocity = incomingVelocity - 2 * vDotN * normal;
                            reflectedVelocity *= SimConstants.COEFFICIENT_OF_RESTITUTION;

                            // Apply friction to horizontal component
                            Vector3 horizontalVelocity = new Vector3(reflectedVelocity.x, 0, reflectedVelocity.z);
                            horizontalVelocity *= (1f - SimConstants.FRICTION_COEFFICIENT_SLIDING);
                            ball.Velocity = new Vector3(horizontalVelocity.x, reflectedVelocity.y, horizontalVelocity.z);

                            // Check if vertical velocity is low enough to transition to rolling
                            if (Mathf.Abs(ball.Velocity.y) < SimConstants.ROLLING_TRANSITION_VEL_Y_THRESHOLD)
                            {
                                float horizontalSpeed = new Vector2(ball.Velocity.x, ball.Velocity.z).magnitude;
                                if (horizontalSpeed > SimConstants.ROLLING_TRANSITION_VEL_XZ_THRESHOLD)
                                {
                                    // Transition to rolling state
                                    ball.StartRolling();
                                    ball.Velocity = new Vector3(ball.Velocity.x, 0, ball.Velocity.z);
                                }
                                else
                                {
                                    // Not enough horizontal velocity to roll
                                    ball.Stop();
                                }
                            }
                        }
                        else
                        {
                            // Ball is moving away from or parallel to ground (grazing contact)
                            ball.Velocity = new Vector3(ball.Velocity.x, 0, ball.Velocity.z);
                            ball.StartRolling(); // Start rolling
                        }
                    }

                    // Check for out of bounds
                    if (ball.Position.x < 0 || ball.Position.x > _geometry.PitchLength ||
                        ball.Position.z < 0 || ball.Position.z > _geometry.PitchWidth)
                    {
                        // Clamp position to pitch boundaries
                        ball.Position = new Vector3(
                            Mathf.Clamp(ball.Position.x, 0, _geometry.PitchLength),
                            ball.Position.y,
                            Mathf.Clamp(ball.Position.z, 0, _geometry.PitchWidth)
                        );
                    }
                }
                else if (ball.IsRolling)
                {
                    // Cache velocity components for performance
                    float velX = ball.Velocity.x;
                    float velZ = ball.Velocity.z;
                    float velocitySqrMagnitude = velX * velX + velZ * velZ;

                    if (velocitySqrMagnitude > SimConstants.VELOCITY_NEAR_ZERO_SQ)
                    {
                        float horizontalSpeed = Mathf.Sqrt(velocitySqrMagnitude);
                        float frictionDeceleration = SimConstants.FRICTION_COEFFICIENT_ROLLING * _gravity.magnitude;
                        float speedReduction = frictionDeceleration * deltaTime;
                        float newSpeed = Mathf.Max(0, horizontalSpeed - speedReduction);

                        if (newSpeed < SimConstants.ROLLING_TRANSITION_VEL_XZ_THRESHOLD)
                        {
                            ball.Stop();
                        }
                        else
                        {
                            // Define horizontalVelocity before using it
                            Vector3 horizontalVelocity = new Vector3(ball.Velocity.x, 0, ball.Velocity.z).normalized;
                            ball.Velocity = horizontalVelocity * newSpeed;
                            ball.Position += ball.Velocity * deltaTime;
                            ball.Position = new Vector3(ball.Position.x, SimConstants.BALL_RADIUS, ball.Position.z);

                            // Check for out of bounds
                            if (ball.Position.x < 0 || ball.Position.x > _geometry.PitchLength ||
                                ball.Position.z < 0 || ball.Position.z > _geometry.PitchWidth)
                            {
                                // Clamp position to pitch boundaries
                                ball.Position = new Vector3(
                                    Mathf.Clamp(ball.Position.x, 0, _geometry.PitchLength),
                                    SimConstants.BALL_RADIUS,
                                    Mathf.Clamp(ball.Position.z, 0, _geometry.PitchWidth)
                                );
                            }
                        }
                    }
                }

            }


            catch (Exception ex)
            {
                Debug.LogError($"Error in UpdateBallMovement: {ex.Message}");
                // Try to recover by stopping the ball
                if (ball != null) ball.Stop();
            }
        }
    

    /// <summary>
    /// Predicts ball impact point with goal line for specified defending team
    /// </summary>
    /// <param name="ball">The ball to predict impact for</param>
    /// <param name="defendingTeamSimId">The team ID (0=Home, 1=Away) defending the goal line</param>
    /// <returns>The 3D point where the ball trajectory intersects the goal line plane</returns>
    /// <remarks>Returns a fallback position if calculation fails or ball is null</remarks>
    public Vector3 EstimateBallGoalLineImpact3D(SimBall ball, int defendingTeamSimId)
    {
        if (ball == null) return Vector3.zero;
        try
        {
            // Use constants from SimConstants class
            float goalPlaneX = (defendingTeamSimId == SimConstants.HOME_TEAM_ID) ?
            SimConstants.GOAL_PLANE_OFFSET :
                _geometry.PitchLength - SimConstants.GOAL_PLANE_OFFSET;

            Vector3 ballPos3D = ball.Position;
            Vector3 ballVel3D = ball.Velocity;

            // Check for invalid velocity
            if (float.IsNaN(ballVel3D.x) || float.IsNaN(ballVel3D.y) || float.IsNaN(ballVel3D.z))
            {
                Debug.LogWarning("Ball has NaN velocity during impact calculation");
                return new Vector3(goalPlaneX, ballPos3D.y, ballPos3D.z);
            }

            // Check for near-zero velocity
            if (Mathf.Abs(ballVel3D.x) < SimConstants.VELOCITY_NEAR_ZERO)
                return new Vector3(goalPlaneX, ballPos3D.y, ballPos3D.z);

            float timeToPlane = (goalPlaneX - ballPos3D.x) / ballVel3D.x;
            if (timeToPlane < SimConstants.MIN_GOAL_PREDICTION_TIME || timeToPlane > SimConstants.MAX_GOAL_PREDICTION_TIME)
                return new Vector3(goalPlaneX, ballPos3D.y, ballPos3D.z);

            // Project position at impact time
            Vector3 impactPoint = ProjectBallPosition(ballPos3D, ballVel3D, timeToPlane);

            // Ensure impact point is on the goal plane
            impactPoint.x = goalPlaneX;
            impactPoint.y = Mathf.Max(impactPoint.y, SimConstants.BALL_RADIUS);

            // Check for out of bounds
            if (impactPoint.z < 0 || impactPoint.z > _geometry.PitchWidth)
            {
                // Ball will miss the goal area entirely
                return new Vector3(goalPlaneX, impactPoint.y, Mathf.Clamp(impactPoint.z, 0, _geometry.PitchWidth));
            }

            return impactPoint;
        }
        catch (DivideByZeroException)
        {
            Debug.LogError("Ball velocity.x was zero during impact calculation");
            return FallbackImpactPoint(defendingTeamSimId, ball);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error predicting impact point: {ex.GetType().Name} - {ex.Message}");
            return FallbackImpactPoint(defendingTeamSimId, ball);
        }
    }

    /// <summary>
    /// Provides a fallback impact point when normal calculation fails
    /// </summary>
    /// <param name="defendingTeamSimId">The team ID defending the goal</param>
    /// <param name="ball">The ball (may be null)</param>
    /// <returns>A reasonable default position on the goal line</returns>
    private Vector3 FallbackImpactPoint(int defendingTeamSimId, SimBall ball)
    {
        float fallbackX = (defendingTeamSimId == SimConstants.HOME_TEAM_ID) ?
            SimConstants.GOAL_LINE_X_HOME :
            _geometry.PitchLength;

        return new Vector3(
            fallbackX,
            Mathf.Max(SimConstants.BALL_RADIUS, ball?.Position.y ?? SimConstants.BALL_RADIUS),
            _geometry.Center.z
        );
    }

    /// <summary>
    /// Estimates intercept point for a pass based on current ball trajectory and receiver movement
    /// </summary>
    /// <param name="ball">The ball in flight</param>
    /// <param name="receiver">The player attempting to receive the pass</param>
    /// <returns>The optimal 2D intercept point for the receiver to meet the ball</returns>
    public Vector2 EstimatePassInterceptPoint(SimBall ball, SimPlayer receiver)
    {
        // Handle null or non-flight cases
        if (ball == null || !ball.IsInFlight || receiver == null)
        {
            return receiver?.Position ?? Vector2.zero;
        }

        // Extract 2D positions and velocities from 3D
        Vector2 ballPos2D = new Vector2(ball.Position.x, ball.Position.z);
        Vector2 ballVel2D = new Vector2(ball.Velocity.x, ball.Velocity.z);
        Vector2 receiverPos = receiver.Position;
        float receiverSpeed = receiver.EffectiveSpeed;

        // Validate velocities to avoid NaN errors
        if (float.IsNaN(ballVel2D.x) || float.IsNaN(ballVel2D.y) || ballVel2D.sqrMagnitude < SimConstants.VELOCITY_NEAR_ZERO_SQ)
        {
            return ballPos2D; // Ball barely moving, just go to current position
        }

        // Calculate time range for prediction
        float ballSpeed2D = ballVel2D.magnitude;
        float maxPredictionTime = 2.0f; // Don't predict too far ahead

        // Find optimal intercept time using simple approximation
        float bestInterceptTime = 0.5f; // Default fallback
        float minDistance = float.MaxValue;

        // Sample several potential intercept times
        for (float t = 0.1f; t <= maxPredictionTime; t += 0.1f)
        {
            // Project ball position at time t
            Vector3 projectedBallPos3D = ProjectBallPosition(ball.Position, ball.Velocity, t);
            Vector2 projectedBallPos = new Vector2(projectedBallPos3D.x, projectedBallPos3D.z);

            // Calculate how far the receiver can travel in time t
            float receiverTravelDistance = receiverSpeed * t;
            float distanceToIntercept = Vector2.Distance(receiverPos, projectedBallPos);

            // Calculate the difference between how far the receiver needs to go and can go
            float distanceDifference = Mathf.Abs(distanceToIntercept - receiverTravelDistance);

            // If this is a better match than previous best, update
            if (distanceDifference < minDistance)
            {
                minDistance = distanceDifference;
                bestInterceptTime = t;
            }
        }

        // Calculate final intercept position
        Vector3 interceptPos3D = ProjectBallPosition(ball.Position, ball.Velocity, bestInterceptTime);
        Vector2 interceptPos = new Vector2(interceptPos3D.x, interceptPos3D.z);

        // Ensure the intercept point is within reasonable bounds
        if (float.IsNaN(interceptPos.x) || float.IsNaN(interceptPos.y))
        {
            Debug.LogWarning("EstimatePassInterceptPoint calculated NaN position, using fallback");
            return ballPos2D;
        }

        return interceptPos;
    }

    /// <summary>
    /// Projects the ball's 3D position forward in time, considering physics
    /// </summary>
    /// <param name="startPos">The starting 3D position</param>
    /// <param name="velocity">The initial 3D velocity</param>
    /// <param name="time">The time duration to project forward</param>
    /// <returns>The estimated 3D position after the specified time</returns>
    public Vector3 ProjectBallPosition(Vector3 startPos, Vector3 velocity, float time)
    {
        if (time <= 0f || float.IsNaN(time) ||
            float.IsNaN(startPos.x) || float.IsNaN(startPos.y) || float.IsNaN(startPos.z) ||
            float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
            return startPos;

        try
        {
            // Include air resistance in the calculation
            float dragCoefficient = SimConstants.DRAG_COEFFICIENT * SimConstants.AIR_DENSITY;
            Vector3 velocityStep = velocity;
            Vector3 position = startPos;
            float timeStep = 0.016f; // ~60fps simulation steps

            for (float t = 0; t < time; t += timeStep)
            {
                float stepTime = Mathf.Min(timeStep, time - t);
                Vector3 drag = -velocityStep.normalized * velocityStep.sqrMagnitude * dragCoefficient;
                Vector3 acceleration = _gravity + drag / SimConstants.BALL_MASS; // Use cached _gravity
                velocityStep += acceleration * stepTime;
                position += velocityStep * stepTime;
            }

            return position;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in ProjectBallPosition: {ex.Message}");
            return startPos;
        }
    }
}
}