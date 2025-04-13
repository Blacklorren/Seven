using UnityEngine;

namespace HandballManager.Simulation.Core
{
    public static class SimConstants
    {
        // --- Team Identifiers ---
        /// <summary>Team identifier for home team (left side)</summary>
        public const int HOME_TEAM_ID = 0;
        /// <summary>Team identifier for away team (right side)</summary>
        public const int AWAY_TEAM_ID = 1;

        // --- Physics Parameters ---
        /// <summary>Standard Earth gravity in m/s²</summary>
        public const float EARTH_GRAVITY = 9.81f;
        /// <summary>Air density at sea level (kg/m³)</summary>
        public const float AIR_DENSITY = 1.225f;
        /// <summary>Drag coefficient for a sphere</summary>
        public const float DRAG_COEFFICIENT = 0.47f;

        // --- Goal Line Impact Prediction ---
        /// <summary>Offset from goal line for impact plane calculation</summary>
        public const float GOAL_PLANE_OFFSET = 0.1f;
        /// <summary>Maximum time to predict for goal line impact</summary>
        public const float MAX_GOAL_PREDICTION_TIME = 2.0f;
        /// <summary>Minimum time to predict for goal line impact (allows for slight backtracking)</summary>
        public const float MIN_GOAL_PREDICTION_TIME = -0.05f;
        /// <summary>X-coordinate for home team goal line</summary>
        public const float GOAL_LINE_X_HOME = 0f;
        /// <summary>Threshold for considering velocity near zero</summary>
        public const float VELOCITY_NEAR_ZERO = 0.1f;

        // --- Game Rules ---
        /// <summary>Standard suspension duration in seconds</summary>
        public const float DEFAULT_SUSPENSION_TIME = 120f;
        /// <summary>Red card suspension duration</summary>
        public const float RED_CARD_SUSPENSION_TIME = float.MaxValue;

        // --- Epsilon ---
        /// <summary>Squared magnitude threshold for near-zero velocity checks.</summary>
        public const float VELOCITY_NEAR_ZERO_SQ = 0.01f; // Increased slightly for 3D stopping
        /// <summary>Small value for floating point comparisons.</summary>
        public const float FLOAT_EPSILON = 0.0001f;

        // --- Ball Physics ---

        /// <summary>Standard handball mass in kg</summary>
        public const float BALL_MASS = 0.425f;
        /// <summary>Handball circumference to radius conversion</summary>
        public const float BALL_RADIUS = 0.095f; // ~58cm circumference
        /// <summary>Cross-sectional area of the ball for drag calculations.</summary>
        public const float BALL_CROSS_SECTIONAL_AREA = Mathf.PI * BALL_RADIUS * BALL_RADIUS;
        /// <summary>Default height of the ball above the 'pitch' (now based on radius).</summary>
        public const float BALL_DEFAULT_HEIGHT = BALL_RADIUS; // Start on the ground
        /// <summary>Gravity vector for physics calculations.</summary>
        public static readonly Vector3 GRAVITY = new Vector3(0f, -9.81f, 0f); // Standard gravity
        /// <summary>Coefficient for Magnus effect calculations.</summary>
        public const float MAGNUS_COEFFICIENT_SIMPLE = 0.0001f; // Simplified lift coefficient scaler - NEEDS TUNING
        /// <summary>Factor for angular velocity decay per second.</summary>
        public const float SPIN_DECAY_FACTOR = 0.90f; // Multiplier per second for angular velocity decay
        /// <summary>Bounciness factor (0=no bounce, 1=perfect bounce).</summary>
        public const float COEFFICIENT_OF_RESTITUTION = 0.65f; // Bounciness
        /// <summary>Friction coefficient for sliding on surface.</summary>
        public const float FRICTION_COEFFICIENT_SLIDING = 0.4f; // Friction during bounce/slide
        /// <summary>Friction coefficient for rolling on surface.</summary>
        public const float FRICTION_COEFFICIENT_ROLLING = 0.015f; // Friction when rolling
        /// <summary>Vertical velocity threshold below which rolling might start after bounce.</summary>
        public const float ROLLING_TRANSITION_VEL_Y_THRESHOLD = 0.2f; // Vertical velocity below which rolling might start after bounce
        /// <summary>Horizontal speed threshold below which rolling stops.</summary>
        public const float ROLLING_TRANSITION_VEL_XZ_THRESHOLD = 0.1f; // Horizontal speed below which rolling stops

        // --- Ball State ---
        /// <summary>Offset distance for the ball when held by a player.</summary>
        public const float BALL_OFFSET_FROM_HOLDER = 0.3f;
        /// <summary>Small offset applied when releasing pass/shot to prevent immediate collision.</summary>
        public const float BALL_RELEASE_OFFSET = 0.1f;
        /// <summary>Factor applied to default height for a loose ball.</summary>
        public const float BALL_LOOSE_HEIGHT_FACTOR = 1.0f; // Loose ball can start at normal height if bounced

        // --- SimPlayer ---
        /// <summary>Default max speed if MatchSimulator constant isn't available or BaseData is missing.</summary>
        public const float PLAYER_DEFAULT_MAX_SPEED = 7.0f;
        /// <summary>Stamina threshold below which player speed starts reducing.</summary>
        public const float PLAYER_STAMINA_LOW_THRESHOLD = 0.5f;
        /// <summary>Minimum speed factor (multiplier) when player stamina is zero.</summary>
        public const float PLAYER_STAMINA_MIN_SPEED_FACTOR = 0.4f;
        /// <summary>Default attribute value used if BaseData is missing.</summary>
        public const int PLAYER_DEFAULT_ATTRIBUTE_VALUE = 50;
        
    }
}