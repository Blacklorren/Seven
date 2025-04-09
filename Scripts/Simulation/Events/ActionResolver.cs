// --- START OF FILE HandballManager/Simulation/Events/ActionResolver.cs ---
using HandballManager.Simulation.Constants;
using HandballManager.Simulation.Events.Calculators;
using HandballManager.Simulation.Interfaces;
using HandballManager.Simulation.MatchData;
using HandballManager.Simulation.Core; // For ActionResult
using UnityEngine;

namespace HandballManager.Simulation.Events
{
    /// <summary>
    /// Orchestrates the resolution of player actions by delegating to specialized calculators.
    /// Implements the IActionResolver interface.
    /// </summary>
    public class ActionResolver : IActionResolver // Implement the interface
    {
        // Dependencies on calculators
        private readonly PassCalculator _passCalculator;
        private readonly ShotCalculator _shotCalculator;
        private readonly TackleCalculator _tackleCalculator;
        private readonly InterceptionCalculator _interceptionCalculator;
        // FoulCalculator is likely a dependency of TackleCalculator, not needed directly here

        // Constructor for dependency injection (or direct instantiation)
        public ActionResolver(PassCalculator passCalc, ShotCalculator shotCalc, TackleCalculator tackleCalc, InterceptionCalculator interceptionCalc)
        {
            _passCalculator = passCalc ?? throw new System.ArgumentNullException(nameof(passCalc));
            _shotCalculator = shotCalc ?? throw new System.ArgumentNullException(nameof(shotCalc));
            _tackleCalculator = tackleCalc ?? throw new System.ArgumentNullException(nameof(tackleCalc));
            _interceptionCalculator = interceptionCalc ?? throw new System.ArgumentNullException(nameof(interceptionCalc));
        }

        // Overload for simple instantiation (if not using DI container)
        public ActionResolver()
        {
            // Instantiate dependencies here
            FoulCalculator foulCalculator = new FoulCalculator(); // Tackle needs this
            _passCalculator = new PassCalculator();
            _shotCalculator = new ShotCalculator();
            _tackleCalculator = new TackleCalculator(foulCalculator); // Inject dependency
            _interceptionCalculator = new InterceptionCalculator();
        }

        /// <summary>
        /// Resolves a prepared action (Pass, Shot, Tackle) when its timer completes.
        /// Delegates to the appropriate specialized calculator.
        /// </summary>
        public ActionResult ResolvePreparedAction(SimPlayer player, MatchState state)
        {
            if (player == null || state == null)
                return new ActionResult { Outcome = ActionResultOutcome.Failure, Reason = "Null Player/State in ActionResolver" };

            PlayerAction actionToResolve = player.CurrentAction;

            // Reset player state immediately *except* for tackle (calculator handles reset after use)
            if (actionToResolve != PlayerAction.AttemptingTackle) {
                // Note: Consider if resetting here is always correct, or if calculator should handle it
                player.CurrentAction = PlayerAction.Idle;
                player.ActionTimer = 0f;
            }

            switch (actionToResolve)
            {
                case PlayerAction.PreparingPass:
                    // Pass Calculator handles target validation internally now
                    return _passCalculator.ResolvePassAttempt(player, player.TargetPlayer, state);

                case PlayerAction.PreparingShot:
                    return _shotCalculator.ResolveShotAttempt(player, state);

                case PlayerAction.AttemptingTackle:
                    // Tackle Calculator handles target validation and player state reset
                    return _tackleCalculator.ResolveTackleAttempt(player, state);

                default:
                    Debug.LogWarning($"ActionResolver: Attempting to resolve unhandled prepared action: {actionToResolve}");
                    // Ensure state is reset if not already done
                    if (actionToResolve != PlayerAction.AttemptingTackle) {
                         // Already reset above
                    } else {
                         player.CurrentAction = PlayerAction.Idle; // Reset tackle state if default hit
                         player.ActionTimer = 0f;
                    }
                    return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = player, Reason = "Unhandled Prepared Action" };
            }
        }

        /// <summary>
        /// Calculates the probability of a defender intercepting a pass. Delegates to calculator.
        /// </summary>
        public float CalculateInterceptionChance(SimPlayer defender, SimBall ball, MatchState state)
        {
            return _interceptionCalculator.CalculateInterceptionChance(defender, ball, state);
        }

        /// <summary>
        /// Calculates tackle probabilities for AI decision making. Delegates to calculator.
        /// </summary>
        public (float successChance, float foulChance) CalculateTackleProbabilities(SimPlayer tackler, SimPlayer target, MatchState state)
        {
            return _tackleCalculator.CalculateTackleProbabilities(tackler, target, state);
        }
    }
}
// --- END OF FILE HandballManager/Simulation/Engines/ActionResolver.cs ---