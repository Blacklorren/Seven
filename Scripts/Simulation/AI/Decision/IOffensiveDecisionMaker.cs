using HandballManager.Simulation.Core;
using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Simulation.Core.MatchData;

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Consolidated interface for offensive decision making in the AI system.
    /// Combines the functionality of the former IPassingDecisionMaker and IShootingDecisionMaker interfaces.
    /// </summary>
    public interface IOffensiveDecisionMaker
    {
        /// <summary>
        /// Makes a decision about passing the ball to another player.
        /// </summary>
        /// <param name="context">The current AI context containing game state and player information.</param>
        /// <returns>A decision result containing the pass decision details.</returns>
        DecisionResult MakePassDecision(PlayerAIContext context);
        
        /// <summary>
        /// Makes a decision about shooting the ball at the goal.
        /// </summary>
        /// <param name="context">The current AI context containing game state and player information.</param>
        /// <returns>A decision result containing the shot decision details.</returns>
        DecisionResult MakeShotDecision(PlayerAIContext context);
        
        /// <summary>
        /// Makes a decision about dribbling with the ball.
        /// </summary>
        /// <param name="context">The current AI context containing game state and player information.</param>
        /// <returns>A decision result containing the dribble decision details.</returns>
        DecisionResult MakeDribbleDecision(PlayerAIContext context);
    }
    
    /// <summary>
    /// Represents the result of an AI decision.
    /// </summary>
    public class DecisionResult
    {
        /// <summary>
        /// Gets or sets whether the decision was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }
        
        /// <summary>
        /// Gets or sets the confidence level of the decision (0.0 to 1.0).
        /// </summary>
        public float Confidence { get; set; }
        
        /// <summary>
        /// Gets or sets additional data related to the decision.
        /// </summary>
        public object Data { get; set; }
    }
    
    /// <summary>
    /// Context information provided to AI decision makers.
    /// </summary>
    public class PlayerAIContext
    {
        /// <summary>
        /// Gets or sets the current match state.
        /// </summary>
        public MatchState MatchState { get; set; }
        
        /// <summary>
        /// Gets or sets the player making the decision.
        /// </summary>
        public SimPlayer Player { get; set; }
        
        /// <summary>
        /// Gets or sets the current tactical setup.
        /// </summary>
        public TeamTactics Tactics { get; set; }
    }
}