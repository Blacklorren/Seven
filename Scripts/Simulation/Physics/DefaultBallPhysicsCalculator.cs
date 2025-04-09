using UnityEngine;
using HandballManager.Simulation.Interfaces;
using HandballManager.Simulation.Common;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Core; // Added for SimConstants
using System;

namespace HandballManager.Simulation.Physics // Namespace corrected
{
    public class DefaultBallPhysicsCalculator : IBallPhysicsCalculator
    {
        private readonly IGeometryProvider _geometry;

        public DefaultBallPhysicsCalculator(IGeometryProvider geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        public void UpdateBallMovement(SimBall ball, float deltaTime)
        {
            // --- Logic copied from previous thought process ---
             if (ball.Holder != null)
             {
                 Vector2 playerPos2D = ball.Holder.Position;
                 Vector2 offsetDir2D = Vector2.right * (ball.Holder.TeamSimId == 0 ? 1f : -1f);
                 if (ball.Holder.Velocity.sqrMagnitude > SimConstants.VELOCITY_NEAR_ZERO_SQ) { offsetDir2D = ball.Holder.Velocity.normalized; }
                 Vector2 ballPos2D = playerPos2D + offsetDir2D * SimConstants.BALL_OFFSET_FROM_HOLDER;
                 ball.Position = new Vector3(ballPos2D.x, SimConstants.BALL_DEFAULT_HEIGHT, ballPos2D.y);
                 ball.Velocity = Vector3.zero;
                 ball.AngularVelocity = Vector3.zero;
             }
             else if (ball.IsInFlight)
             {
                 Vector3 force = Vector3.zero;
                 float speed = ball.Velocity.magnitude;
                 force += SimConstants.GRAVITY * SimConstants.BALL_MASS;
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
                     ball.Position = new Vector3(ball.Position.x, SimConstants.BALL_RADIUS, ball.Position.z);
                     Vector3 incomingVelocity = ball.Velocity;
                     Vector3 normal = Vector3.up;
                     float vDotN = Vector3.Dot(incomingVelocity, normal);
                     if (vDotN < 0)
                     {
                         Vector3 reflectedVelocity = incomingVelocity - 2 * vDotN * normal;
                         reflectedVelocity *= SimConstants.COEFFICIENT_OF_RESTITUTION;
                         Vector3 horizontalVelocity = new Vector3(reflectedVelocity.x, 0, reflectedVelocity.z);
                         horizontalVelocity *= (1f - SimConstants.FRICTION_COEFFICIENT_SLIDING);
                         ball.Velocity = new Vector3(horizontalVelocity.x, reflectedVelocity.y, horizontalVelocity.z);

                         if (Mathf.Abs(ball.Velocity.y) < SimConstants.ROLLING_TRANSITION_VEL_Y_THRESHOLD)
                         {
                             float horizontalSpeed = new Vector2(ball.Velocity.x, ball.Velocity.z).magnitude;
                             if (horizontalSpeed > SimConstants.ROLLING_TRANSITION_VEL_XZ_THRESHOLD)
                             {
                                 ball.IsInFlight = false;
                                 ball.IsRolling = true;
                                 ball.Velocity = new Vector3(ball.Velocity.x, 0, ball.Velocity.z);
                             } else { ball.Stop(); }
                         }
                     } else {
                         ball.Velocity = new Vector3(ball.Velocity.x, 0, ball.Velocity.z);
                         ball.IsInFlight = false; // Stop flight even on grazing impact
                         ball.IsRolling = true; // Start rolling
                     }
                 }
             }
             else if (ball.IsRolling)
             {
                 Vector3 horizontalVelocity = new Vector3(ball.Velocity.x, 0, ball.Velocity.z);
                 float horizontalSpeed = horizontalVelocity.magnitude;
                 if (horizontalSpeed > SimConstants.FLOAT_EPSILON)
                 {
                     float frictionDeceleration = SimConstants.FRICTION_COEFFICIENT_ROLLING * 9.81f;
                     float speedReduction = frictionDeceleration * deltaTime;
                     float newSpeed = Mathf.Max(0, horizontalSpeed - speedReduction);
                     if (newSpeed < SimConstants.ROLLING_TRANSITION_VEL_XZ_THRESHOLD) { ball.Stop(); }
                     else
                     {
                         ball.Velocity = horizontalVelocity.normalized * newSpeed;
                         ball.Position += ball.Velocity * deltaTime;
                         ball.Position = new Vector3(ball.Position.x, SimConstants.BALL_RADIUS, ball.Position.z);
                     }
                 } else { ball.Stop(); }
             }
        }

        public Vector3 EstimateBallGoalLineImpact3D(SimBall ball, int defendingTeamSimId)
        {
            // --- Logic copied from previous thought process ---
             if (ball == null) return Vector3.zero;
             try {
                 float goalPlaneX = (defendingTeamSimId == 0) ? 0.1f : _geometry.PitchLength - 0.1f;
                 Vector3 ballPos3D = ball.Position;
                 Vector3 ballVel3D = ball.Velocity;
                 if (Mathf.Abs(ballVel3D.x) < 0.1f) return new Vector3(goalPlaneX, ballPos3D.y, ballPos3D.z);

                 float timeToPlane = (goalPlaneX - ballPos3D.x) / ballVel3D.x;
                 if (timeToPlane < -0.05f || timeToPlane > 2.0f) return new Vector3(goalPlaneX, ballPos3D.y, ballPos3D.z);

                 Vector3 impactPoint = ballPos3D + ballVel3D * timeToPlane;
                 impactPoint.x = goalPlaneX;
                 impactPoint.y = Mathf.Max(impactPoint.y, SimConstants.BALL_RADIUS);
                 return impactPoint;
             } catch (Exception ex) {
                 Debug.LogError($"Error predicting impact point: {ex.Message}");
                 float fallbackX = (defendingTeamSimId == 0) ? 0f : _geometry.PitchLength;
                 return new Vector3(fallbackX, Mathf.Max(SimConstants.BALL_RADIUS, ball?.Position.y ?? SimConstants.BALL_RADIUS), _geometry.Center.z);
             }
        }

        public Vector2 EstimatePassInterceptPoint(SimBall ball, SimPlayer receiver)
        {
            // --- Logic copied from previous thought process ---
             if (ball == null || !ball.IsInFlight)
             {
                 return receiver?.Position ?? Vector2.zero;
             }
             return new Vector2(ball.Position.x, ball.Position.z);
        }
    }
}