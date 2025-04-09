using UnityEngine;
using HandballManager.Simulation.Utils; // Changed from Interfaces to Utils
using HandballManager.Simulation.MatchData;

namespace HandballManager.Simulation.Utils // Changed from Services to Utils
{
    public class PitchGeometryProvider : IGeometryProvider
    {
        // Implement all properties and methods from IGeometryProvider
        // by copying the static PitchGeometry class content here.
        // Replace hardcoded values with references if moved to a config class.

        public float PitchWidth => 20f;
        public float PitchLength => 40f;
        public Vector3 PitchCenter => new Vector3(PitchLength / 2f, SimConstants.BALL_RADIUS, PitchWidth / 2f);
        public Vector3 HomeGoalCenter => new Vector3(0f, SimConstants.BALL_RADIUS, PitchWidth / 2f);
        public Vector3 AwayGoalCenter => new Vector3(PitchLength, SimConstants.BALL_RADIUS, PitchWidth / 2f);
        public float GoalAreaRadius => 6f;
        public float FreeThrowLineRadius => 9f;
        public float GoalWidth => 3f;
        public float GoalHeight => 2f;
        public float SevenMeterMarkX => 7f;
        public Vector3 HomePenaltySpot => new Vector3(SevenMeterMarkX, SimConstants.BALL_RADIUS, PitchCenter.z);
        public Vector3 AwayPenaltySpot => new Vector3(PitchLength - SevenMeterMarkX, SimConstants.BALL_RADIUS, PitchCenter.z);

        public bool IsInGoalArea(Vector3 position, bool checkHomeGoalArea)
        {
            Vector3 goalCenter = checkHomeGoalArea ? HomeGoalCenter : AwayGoalCenter;
            float distSqXZ = (position.x - goalCenter.x) * (position.x - goalCenter.x) +
                             (position.z - goalCenter.z) * (position.z - goalCenter.z);
            return distSqXZ <= GoalAreaRadius * GoalAreaRadius;
        }
        // Removed obsolete 2D version
    }
}