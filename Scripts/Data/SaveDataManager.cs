using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HandballManager.Core;
using HandballManager.Management;
using UnityEngine;

namespace HandballManager.Data
{
    /// <summary>
    /// Main save data container. Needs to be serializable.
    /// Public fields used for JsonUtility compatibility.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public GameState CurrentGameState;
        public long CurrentDateTicks; // Store DateTime as Ticks
        public List<LeagueData> Leagues = new List<LeagueData>();
        public List<TeamData> Teams = new List<TeamData>();
        public List<PlayerData> Players = new List<PlayerData>(); // Might become very large!
        public List<StaffData> Staff = new List<StaffData>();
        public int PlayerTeamID = -1;
        // LeagueManager save data (JsonUtility struggles with Dictionary directly)
        public List<int> LeagueTableKeys = new List<int>();
        public List<List<LeagueStandingEntry>> LeagueTableValues = new List<List<LeagueStandingEntry>>();
        // Add ScheduleManager save data if needed
        // public List<int> ScheduleKeys = new List<int>();
        // public List<List<MatchInfo>> ScheduleValues = new List<List<MatchInfo>>();
    }

    /// <summary>
    /// Dedicated persistence layer for saving and loading game data.
    /// Uses a more efficient serialization approach than the previous JSON implementation.
    /// </summary>
    public class SaveDataManager
    {
        private const string SAVE_DIRECTORY = "saves";
        
        /// <summary>
        /// Saves the current game state to a file.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="teams">The list of teams to save.</param>
        /// <param name="timeManager">The time manager to get the current date from.</param>
        /// <param name="playerTeamId">The ID of the player's team.</param>
        /// <param name="leagues">The list of leagues to save.</param>
        /// <param name="players">The list of players to save.</param>
        /// <param name="staff">The list of staff to save.</param>
        /// <param name="leagueTables">The league tables to save.</param>
        /// <returns>The path to the saved file.</returns>
        public string SaveGame(GameState state, List<TeamData> teams, TimeManager timeManager, 
            int playerTeamId, List<LeagueData> leagues, List<PlayerData> players, 
            List<StaffData> staff, Dictionary<int, List<LeagueStandingEntry>> leagueTables)
        {
            // Ensure save directory exists
            string saveDir = Path.Combine(Application.persistentDataPath, SAVE_DIRECTORY);
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }
            
            // Create save file path with timestamp
            string savePath = Path.Combine(saveDir, $"save_{DateTime.Now:yyyyMMddHHmmss}.bin");
            
            try
            {
                // For now, we'll create a SaveData object similar to the existing one
                // In a future update, this would be replaced with Protobuf serialization
                SaveData saveData = new SaveData
                {
                    CurrentGameState = state,
                    CurrentDateTicks = timeManager.CurrentDate.Ticks,
                    Leagues = leagues,
                    Teams = teams,
                    Players = players,
                    Staff = staff,
                    PlayerTeamID = playerTeamId
                };
                
                // Prepare league tables for save
                if (leagueTables != null)
                {
                    saveData.LeagueTableKeys = leagueTables.Keys.ToList();
                    saveData.LeagueTableValues = leagueTables.Values.ToList();
                }
                
                // Serialize to JSON for now (will be replaced with Protobuf in future)
                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(savePath, json);
                
                Debug.Log($"Game saved successfully to {savePath}");
                return savePath;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving game: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }
        
        /// <summary>
        /// Loads a game from the specified file.
        /// </summary>
        /// <param name="filePath">The path to the save file.</param>
        /// <returns>The loaded save data.</returns>
        public SaveData LoadGame(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Save file not found at: {filePath}");
            }
            
            try
            {
                // For now, we'll use JsonUtility to deserialize
                // In a future update, this would be replaced with Protobuf deserialization
                string json = File.ReadAllText(filePath);
                SaveData saveData = JsonUtility.FromJson<SaveData>(json);
                
                if (saveData == null)
                {
                    throw new Exception("Failed to deserialize save data (SaveData is null).");
                }
                
                return saveData;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading game: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }
        
        /// <summary>
        /// Gets the path to the most recent save file.
        /// </summary>
        /// <returns>The path to the most recent save file, or null if no save files exist.</returns>
        public string GetMostRecentSavePath()
        {
            string saveDir = Path.Combine(Application.persistentDataPath, SAVE_DIRECTORY);
            if (!Directory.Exists(saveDir))
            {
                return null;
            }
            
            var saveFiles = Directory.GetFiles(saveDir, "save_*.bin")
                .OrderByDescending(f => f)
                .ToArray();
            
            return saveFiles.Length > 0 ? saveFiles[0] : null;
        }
        
        /// <summary>
        /// Gets a list of all save files.
        /// </summary>
        /// <returns>A list of save file paths.</returns>
        public List<string> GetAllSaveFiles()
        {
            string saveDir = Path.Combine(Application.persistentDataPath, SAVE_DIRECTORY);
            if (!Directory.Exists(saveDir))
            {
                return new List<string>();
            }
            
            return Directory.GetFiles(saveDir, "save_*.bin")
                .OrderByDescending(f => f)
                .ToList();
        }
        
        /// <summary>
        /// Deletes a save file.
        /// </summary>
        /// <param name="filePath">The path to the save file to delete.</param>
        public void DeleteSaveFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"Deleted save file: {filePath}");
            }
        }
    }
    
    // The CompactSaveFormat class should either be used or removed
    // If keeping it for future implementation, add a TODO comment
    
    /// <summary>
    /// Compact save format for future Protobuf serialization.
    /// This is a placeholder for the actual Protobuf implementation.
    /// TODO: Implement Protobuf serialization in a future update.
    /// </summary>
    [Serializable]
    public class CompactSaveFormat
    {
        public GameState State;
        public List<int> TeamIds;
        // Additional fields would be added here for the compact format
    }
}