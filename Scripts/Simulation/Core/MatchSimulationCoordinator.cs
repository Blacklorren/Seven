using UnityEngine;
using HandballManager.Data;
using HandballManager.Simulation.Core.Events;
using HandballManager.Simulation.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using HandballManager.Gameplay;
using System;

namespace HandballManager.Simulation.Core
{
    /// <summary>
    /// Coordinates the match simulation process using a component-based approach.
    /// Uses dependency injection to get required services.
    /// </summary>
    public class MatchSimulationCoordinator : MonoBehaviour, IMatchSimulationCoordinator
    {
        // Injected dependencies
        private IMatchEngine _engine;
        private IPlayerAIService _aiService;
        private IEventBus _eventBus;
        
        private bool IsOnMainThread() => Thread.CurrentThread.ManagedThreadId == 1;
        
        /// <summary>
        /// Initializes the coordinator with the required dependencies.
        /// </summary>
        /// <param name="engine">The match engine service.</param>
        /// <param name="aiService">The player AI service.</param>
        /// <param name="eventBus">The event bus for publishing events.</param>
        public async Task<MatchResult> RunSimulationAsync(TeamData home, TeamData away, Tactic homeTactic, Tactic awayTactic, CancellationToken cancellationToken = default)
        {
            if (!IsOnMainThread())
            {
                throw new InvalidOperationException("Simulation must start from main thread");
            }

            try
            {
                return await Task.Run(async () =>
                {
                    return await _engine.SimulateMatchAsync(
                        home,
                        away,
                        homeTactic,
                        awayTactic,
                        cancellationToken
                    );
                }, cancellationToken).ConfigureAwait(true);
            }
            finally
            {
                CleanupResources();
            }
        }

        public void AbortCurrentSimulation()
        {
            // Cleanup ongoing operations
            _engine.ResetMatch();
        }

        public void CleanupResources()
        {
            // Reset all match-related state
            _engine.ResetMatch();
        }

        // Change 1: Explicit interface implementation
        void IMatchSimulationCoordinator.Initialize(IMatchEngine engine, IPlayerAIService aiService, IEventBus eventBus)
        {
            _engine = engine;
            _aiService = aiService;
            _eventBus = eventBus;
        }
        
        // Remove this conflicting method:
        // public void Initialize(IMatchEngine engine, IPlayerAIService aiService, IEventBus eventBus) {...}
        
        // Change 2: Add cancellation to coroutine
        private System.Collections.IEnumerator RunSimulation(CancellationToken cancellationToken)
        {
            while (!_engine.MatchCompleted && !cancellationToken.IsCancellationRequested)
            {
                _aiService.ProcessDecisions();
                _engine.Advance(Time.deltaTime);
                yield return null;
            }
            
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.LogWarning("Simulation aborted by user request");
                yield break;
            }
            // Match is complete, publish the result
            _eventBus.Publish(new MatchCompletedEvent
            {
                Result = _engine.GetMatchResult()
            });
            
            Debug.Log("Match simulation completed.");
        }
        
        // Change 3: Update interface implementation
        // Remove this non-interface method completely
        // public void Initialize(IMatchEngine engine, IPlayerAIService aiService, IEventBus eventBus)
        
        // Add explicit interface initialization
        void IMatchSimulationCoordinator.InitializeMatch(TeamData home, TeamData away)
        {
            if (!IsOnMainThread())
                throw new InvalidOperationException("Must be called from main thread");
            
            _engine.Initialize(home, away);
            _eventBus.Publish(new MatchStartedEvent {
                HomeTeam = home,
                AwayTeam = away
            });
        }

        // Update coroutine with proper cancellation
        // Fix 1: Remove duplicate RunSimulation methods
        private System.Collections.IEnumerator RunSimulation(CancellationToken token)
        {
            if (!IsOnMainThread())
                throw new InvalidOperationException("Simulation must run on main thread");
        
            while (!_engine.MatchCompleted && !token.IsCancellationRequested)
            {
                try 
                {
                    _aiService.ProcessDecisions();
                    _engine.Advance(Time.deltaTime);
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("Simulation cancelled");
                    yield break;
                }
                yield return null;
            }
            
            if (!token.IsCancellationRequested)
            {
                _eventBus.Publish(new MatchCompletedEvent {
                    Result = _engine.GetMatchResult()
                });
                Debug.Log("Match simulation completed successfully");
            }
        }

        // Fix 2: Update SimulateMatch to handle tactics
        public void SimulateMatch(TeamData home, TeamData away, Tactic homeTactic, Tactic awayTactic, CancellationToken token)
        {
            ((IMatchSimulationCoordinator)this).InitializeMatch(home, away);
            _engine.SetTactics(homeTactic, awayTactic);
            StartCoroutine(RunSimulation(token));
        }

        // Fix 3: Add missing interface method
        void IMatchSimulationCoordinator.Initialize(IMatchEngine engine, IPlayerAIService aiService, IEventBus eventBus)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }
    }
    
    /// <summary>
    /// Event fired when a match starts.
    /// </summary>
    public class MatchStartedEvent : EventBase
    {
        /// <summary>
        /// Gets or sets the home team.
        /// </summary>
        public TeamData HomeTeam { get; set; }
        
        /// <summary>
        /// Gets or sets the away team.
        /// </summary>
        public TeamData AwayTeam { get; set; }
    }
    
    /// <summary>
    /// Interface for the player AI service.
    /// </summary>
    public interface IPlayerAIService
    {
        /// <summary>
        /// Processes decisions for all AI-controlled players.
        /// </summary>
        void ProcessDecisions();
    }
    
}