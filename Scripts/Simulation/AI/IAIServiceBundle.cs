using HandballManager.Simulation.AI.Decision;
using HandballManager.Simulation.AI.Evaluation;
using HandballManager.Simulation.AI.Positioning;

namespace HandballManager.Simulation.AI
{
    /// <summary>
    /// Aggregates all AI-related services to reduce dependency injection complexity.
    /// This interface follows the Facade pattern to simplify access to multiple AI components.
    /// </summary>
    public interface IAIServiceBundle
    {
        /// <summary>Gets the player AI controller.</summary>
        IPlayerAIController PlayerController { get; }
        
        /// <summary>Gets the personality evaluator.</summary>
        IPersonalityEvaluator PersonalityEvaluator { get; }
        
        /// <summary>Gets the tactical evaluator.</summary>
        ITacticalEvaluator TacticalEvaluator { get; }
        
        /// <summary>Gets the game state evaluator.</summary>
        IGameStateEvaluator GameStateEvaluator { get; }
        
        /// <summary>Gets the passing decision maker.</summary>
        IPassingDecisionMaker PassingDecisionMaker { get; }
        
        /// <summary>Gets the shooting decision maker.</summary>
        IShootingDecisionMaker ShootingDecisionMaker { get; }
        
        /// <summary>Gets the dribbling decision maker.</summary>
        IDribblingDecisionMaker DribblingDecisionMaker { get; }
        
        /// <summary>Gets the defensive decision maker.</summary>
        IDefensiveDecisionMaker DefensiveDecisionMaker { get; }
        
        /// <summary>Gets the goalkeeper positioner.</summary>
        IGoalkeeperPositioner GoalkeeperPositioner { get; }
    }
}