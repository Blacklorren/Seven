// --- START OF FILE SimulationUtils.cs ---

using UnityEngine;
using System; // For Math
using HandballManager.Simulation.Core.MatchData; // Updated to reflect new location of MatchData

namespace HandballManager.Simulation.Core.Utils // Updated to match new folder structure
{
    /// <summary>
    /// Provides static utility methods commonly used across simulation engines.
    /// </summary>
    public static class SimulationUtils
    {
        /// <summary>
        /// Calculates the shortest distance from a point to a line segment.
        /// </summary>
        /// <param name="pointC">The point.</param>
        /// <param name="lineA">Start point of the line segment.</param>
        /// <param name="lineB">End point of the line segment.</param>
        /// <returns>The shortest distance from the point to the line segment.</returns>
        public static float CalculateDistanceToLine(Vector2 pointC, Vector2 lineA, Vector2 lineB)
        {
            // Calculate the squared length of the line segment
            float l2 = Vector2.SqrMagnitude(lineB - lineA);

            // If the line segment has almost zero length, return the distance to one endpoint
            if (l2 < 0.0001f)
            {
                return Vector2.Distance(pointC, lineA);
            }

            // Calculate the projection parameter t of pointC onto the line defined by A and B
            // t = dot(C-A, B-A) / |B-A|^2
            float t = Vector2.Dot(pointC - lineA, lineB - lineA) / l2;

            // Clamp t to the range [0, 1] to ensure the projection point lies on the segment
            t = Mathf.Clamp01(t);

            // Calculate the closest point on the line segment to pointC
            Vector2 projection = lineA + t * (lineB - lineA);

            // Return the distance between the point and its projection on the line segment
            return Vector2.Distance(pointC, projection);
        }

        // Add other common simulation utility methods here if needed in the future
        // e.g., CheckLineSegmentIntersection, etc.
    }

    /// <summary>
    /// Provides static utility methods for coordinate conversions between 2D and 3D simulation space.
    /// </summary>
    public static class CoordinateUtils
    {
        /// <summary>Converts a 3D world position to a 2D ground plane position (XZ -> XY).</summary>
        /// <param name="position3D">The 3D world position (Y is height).</param>
        /// <returns>The corresponding 2D position on the ground plane.</returns>
        public static Vector2 To2DGround(Vector3 position3D)
        {
            return new Vector2(position3D.x, position3D.z);
        }

        /// <summary>Converts a 2D ground plane position to a 3D world position at a specified height.</summary>
        /// <param name="position2D">The 2D ground plane position.</param>
        /// <param name="height">The desired height (Y-coordinate) in 3D space. Defaults to standard ball height.</param>
        /// <returns>The corresponding 3D position.</returns>
        public static Vector3 To3DGround(Vector2 position2D, float height = SimConstants.BALL_DEFAULT_HEIGHT)
        {
            return new Vector3(position2D.x, height, position2D.y);
        }
    }
}
// --- END OF FILE SimulationUtils.cs ---