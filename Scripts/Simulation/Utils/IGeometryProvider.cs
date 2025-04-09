using UnityEngine;

namespace HandballManager.Simulation.Utils // Changed from Common to Utils
{
    public interface IGeometryProvider
    {
        float PitchLength { get; }
        float PitchWidth { get; }
        float GoalWidth { get; }
        float GoalHeight { get; }
        float GoalAreaRadius { get; }
        float FreeThrowLineRadius { get; }
        float SidelineBuffer { get; }

        Vector3 Center { get; }
        Vector3 HomeGoalCenter3D { get; }
        Vector3 AwayGoalCenter3D { get; }
        Vector3 HomePenaltySpot3D { get; }
        Vector3 AwayPenaltySpot3D { get; }

        Vector2 GetGoalCenter(int teamSimId);
        Vector2 GetOpponentGoalCenter(int teamSimId);
        bool IsInGoalArea(Vector2 position, bool checkHomeGoalArea);
        bool IsInGoalArea(Vector3 position, bool checkHomeGoalArea);
    }
}