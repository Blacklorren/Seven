using HandballManager.Data;
using HandballManager.Core; // Added for PlayerPosition
using System.Collections.Generic;
using HandballManager.Simulation.Core.MatchData;

namespace HandballManager.Simulation.Utils // Changed from Interfaces to Utils
{
    public interface IPlayerSetupHandler
    {
        bool PopulateAllPlayers(MatchState state);
        bool SelectStartingLineups(MatchState state);
        bool SelectStartingLineup(MatchState state, TeamData team, int teamSimId);
        void PlacePlayersInFormation(MatchState state, List<SimPlayer> players, bool isHomeTeam, bool isKickOff);
        SimPlayer FindPlayerByPosition(MatchState state, List<SimPlayer> lineup, PlayerPosition position); // Removed Core. prefix
    }
}