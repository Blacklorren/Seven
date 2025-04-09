// Updated namespace and imports
using HandballManager.Simulation.Core; // Contains MatchSimulator class
using HandballManager.Data; // For TeamData
using HandballManager.Gameplay; // For Tactic
using System; // For IProgress
using System.Threading; // For CancellationToken

namespace HandballManager.Simulation.Core.Interfaces
{
    /// <summary>
    /// Defines a contract for creating instances of the MatchSimulator.
    /// Encapsulates the setup and dependency injection required for MatchSimulator initialization,
    /// ensuring a clean state for each match simulation.
    /// </summary>
    public interface IMatchSimulatorFactory
    {
        /// <summary>
        /// Creates a new, configured instance of MatchSimulator with default or internally managed teams.
        /// </summary>
        /// <param name="seed">The random seed for the simulation (-1 for default/time-based).</param>
        /// <param name="progress">Optional reporter for simulation progress updates (0.0 to 1.0).</param>
        /// <param name="cancellationToken">Optional token to allow cancellation of the simulation.</param>
        /// <returns>A new instance of MatchSimulator, ready to run a match.</returns>
        /// <remarks>
        /// This overload is for backward compatibility. The implementation should use default teams
        /// or obtain team data from an internal source.
        /// </remarks>
        MatchSimulator Create(int seed = -1, IProgress<float> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new, configured instance of MatchSimulator with specified teams and tactics.
        /// </summary>
        /// <param name="homeTeam">Home team data.</param>
        /// <param name="awayTeam">Away team data.</param>
        /// <param name="homeTactic">Home team tactic.</param>
        /// <param name="awayTactic">Away team tactic.</param>
        /// <param name="seed">The random seed for the simulation (-1 for default/time-based).</param>
        /// <param name="progress">Optional reporter for simulation progress updates (0.0 to 1.0).</param>
        /// <param name="cancellationToken">Optional token to allow cancellation of the simulation.</param>
        /// <returns>A new instance of MatchSimulator, ready to run a match.</returns>
        MatchSimulator Create(
            TeamData homeTeam, 
            TeamData awayTeam, 
            Tactic homeTactic, 
            Tactic awayTactic, 
            int seed = -1, 
            IProgress<float> progress = null, 
            CancellationToken cancellationToken = default);
    }
}