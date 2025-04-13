using HandballManager.Data;
using HandballManager.Gameplay;
using System;
using System.Threading;
using System.Threading.Tasks; // Added missing namespace for Task<T>

namespace HandballManager.Simulation.Core.Interfaces
{
    /// <summary>
    /// Interface for the match engine that orchestrates the simulation of a handball match.
    /// </summary>
    public interface IMatchEngine
    {
        /// <summary>
        /// Asynchronously simulates a complete handball match between two teams.
        /// </summary>
        /// <param name="homeTeam">The home team data.</param>
        /// <param name="awayTeam">The away team data.</param>
        /// <param name="homeTactic">The tactic for the home team.</param>
        /// <param name="awayTactic">The tactic for the away team.</param>
        /// <param name="cancellationToken">Token to cancel the simulation.</param>
        /// <returns>A task containing the match result with score and statistics.</returns>
        Task<MatchResult> SimulateMatchAsync(
            TeamData homeTeam,
            TeamData awayTeam,
            Tactic homeTactic,
            Tactic awayTactic,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Gets the current match state.
        /// </summary>
        /// <returns>True if the match is complete, false otherwise.</returns>
        bool IsMatchComplete { get; }

        /// <summary>
        /// Resets the match engine state.
        /// </summary>
        void ResetMatch();
    }
}