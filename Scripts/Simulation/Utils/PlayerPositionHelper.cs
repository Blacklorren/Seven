using HandballManager.Core; // For PlayerPosition enum

namespace HandballManager.Simulation.Utils
{
    /// <summary>
    /// Provides static helper methods related to player positions.
    /// </summary>
    public static class PlayerPositionHelper
    {
        /// <summary>Checks if the position is a wing position.</summary>
        public static bool IsWing(PlayerPosition pos) => pos == PlayerPosition.LeftWing || pos == PlayerPosition.RightWing;

        /// <summary>Checks if the position is a backcourt position (including CentreBack).</summary>
        public static bool IsBack(PlayerPosition pos) => pos == PlayerPosition.LeftBack || pos == PlayerPosition.RightBack || pos == PlayerPosition.CentreBack;

        /// <summary>Checks if the position is a backcourt position or Pivot.</summary>
        public static bool IsBackcourtOrPivot(PlayerPosition pos) => IsBack(pos) || pos == PlayerPosition.Pivot;
    }
}