using HandballManager.Simulation.Core;
using UnityEngine;

namespace HandballManager.Simulation.Utils // Changed from Services to Utils
{
    public class PitchGeometryProvider : IGeometryProvider
    {
        // Implement all properties and methods from IGeometryProvider
        // by copying the static PitchGeometry class content here.
        // Replace hardcoded values with references if moved to a config class.

        public float PitchWidth => 20f;
        public float PitchLength => 40f;
        public float GoalWidth => 3f;
        public float GoalHeight => 2f;
        public float GoalAreaRadius => 6f;
        public float FreeThrowLineRadius => 9f;
        public float SidelineBuffer => 1f; // Missing property from interface
        
        // Center property required by interface
        public Vector3 Center => new Vector3(PitchLength / 2f, SimConstants.BALL_RADIUS, PitchWidth / 2f);
        
        // Renamed to match interface
        public Vector3 HomeGoalCenter3D => new Vector3(0f, SimConstants.BALL_RADIUS, PitchWidth / 2f);
        public Vector3 AwayGoalCenter3D => new Vector3(PitchLength, SimConstants.BALL_RADIUS, PitchWidth / 2f);
        
        private float SevenMeterMarkX => 7f;
        public Vector3 HomePenaltySpot3D => new Vector3(SevenMeterMarkX, SimConstants.BALL_RADIUS, Center.z);
        public Vector3 AwayPenaltySpot3D => new Vector3(PitchLength - SevenMeterMarkX, SimConstants.BALL_RADIUS, Center.z);

        public Vector2 GetGoalCenter(int teamSimId)
        {
            Vector3 goalCenter3D = teamSimId == 0 ? HomeGoalCenter3D : AwayGoalCenter3D;
            return new Vector2(goalCenter3D.x, goalCenter3D.z);
        }

        public Vector2 GetOpponentGoalCenter(int teamSimId)
        {
            return GetGoalCenter(teamSimId == 0 ? 1 : 0);
        }

        public bool IsInGoalArea(Vector2 position, bool checkHomeGoalArea)
        {
            Vector3 pos3D = new Vector3(position.x, SimConstants.BALL_RADIUS, position.y);
            return IsInGoalArea(pos3D, checkHomeGoalArea);
        }

        public bool IsInGoalArea(Vector3 position, bool checkHomeGoalArea)
        {
            Vector3 goalCenter = checkHomeGoalArea ? HomeGoalCenter3D : AwayGoalCenter3D;
            float distSqXZ = (position.x - goalCenter.x) * (position.x - goalCenter.x) +
                             (position.z - goalCenter.z) * (position.z - goalCenter.z);
            return distSqXZ <= GoalAreaRadius * GoalAreaRadius;
        }
    }
}