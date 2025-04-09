using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO; // Required for File I/O
using System.Linq; // Required for Linq operations
using HandballManager.Data;          // Core data structures
using HandballManager.Simulation;    // Simulation engines
using HandballManager.UI;            // UI manager
using HandballManager.Gameplay;    // Gameplay systems (Tactic, Contract, Transfer)
using HandballManager.Management;    // League, Schedule, Finance managers
using HandballManager.Core.MatchData; // For MatchInfo struct (adjust namespace if different)

// Ensure required namespaces exist, even if classes are basic placeholders
namespace HandballManager.Management { public class LeagueManager { public void ProcessMatchResult(MatchResult result) { Debug.Log($"[LeagueManager] Processing result: {result}"); } public void UpdateStandings() { /* TODO */ } public void FinalizeSeason() { /* TODO */ } public void ResetTablesForNewSeason() { /* TODO */ } public void InitializeLeagueTable(int leagueId, bool forceReinit = false) { /* TODO */ } public Dictionary<int, List<LeagueStandingEntry>> GetTablesForSave() { return new Dictionary<int, List<LeagueStandingEntry>>(); } public void RestoreTablesFromSave(Dictionary<int, List<LeagueStandingEntry>> loadedTables) { /* TODO */ } } }
namespace HandballManager.Management { public class ScheduleManager { public List<MatchInfo> GetMatchesForDate(DateTime date) { /* TODO: Return scheduled matches */ return new List<MatchInfo>(); } public void GenerateNewSchedule() { /* TODO */ } public void HandleRescheduling(MatchInfo postponedMatch, DateTime newDate) { /* TODO */ } public void HandleSeasonTransition() { /* TODO */ } } }
namespace HandballManager.Management { public class FinanceManager { public void ProcessWeeklyPayments(List<TeamData> allTeams) { /* TODO */ } public void ProcessMonthly(List<TeamData> allTeams) { /* TODO */ } } }

// Ensure required data structures exist
namespace HandballManager.Data { [Serializable] public class LeagueStandingEntry { public int TeamID; public string TeamName; public int Played = 0; public int Wins = 0; public int Draws = 0; public int Losses = 0; public int GoalsFor = 0; public int GoalsAgainst = 0; public int GoalDifference => GoalsFor - GoalsAgainst; public int Points => (Wins * 2) + (Draws * 1); } }


namespace HandballManager.Core
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

    // Basic placeholder LeagueData
    [Serializable]
    public class LeagueData { public int LeagueID; public string Name; /* Add standings, teams list etc. */ }
    // Basic placeholder MatchInfo for schedule (ensure namespace matches if defined elsewhere)
    namespace MatchData { [Serializable] public struct MatchInfo { public DateTime Date; public int HomeTeamID; public int AwayTeamID; } }


    /// <summary>
    /// Singleton GameManager responsible for overall game state,
    /// managing core systems, and triggering the main game loop updates.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        private static GameManager _instance;
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameManager>();
                     if (_instance == null)
                     {
                         GameObject singletonObject = new GameObject("GameManager");
                         _instance = singletonObject.AddComponent<GameManager>();
                         Debug.Log("GameManager instance was null. Created a new GameManager object.");
                     }
                }
                return _instance;
            }
        }

        // --- Public Properties ---
        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        // --- Core System References ---
        public TimeManager TimeManager { get; private set; }
        public UIManager UIManagerRef { get; private set; }
        public MatchEngine MatchEngine { get; private set; }
        public TrainingSimulator TrainingSimulator { get; private set; }
        public MoraleSimulator MoraleSimulator { get; private set; }
        public PlayerDevelopment PlayerDevelopment { get; private set; }
        public TransferManager TransferManager { get; private set; }
        public ContractManager ContractManager { get; private set; }
        // New Manager References
        public LeagueManager LeagueManager { get; private set; }
        public ScheduleManager ScheduleManager { get; private set; }
        public FinanceManager FinanceManager { get; private set; }

        // --- Game Data (Loaded/Managed by GameManager) ---
        public List<LeagueData> AllLeagues { get; private set; } = new List<LeagueData>();
        public List<TeamData> AllTeams { get; private set; } = new List<TeamData>();
        public List<PlayerData> AllPlayers { get; private set; } = new List<PlayerData>(); // Caution: Potentially large!
        public List<StaffData> AllStaff { get; private set; } = new List<StaffData>();
        public TeamData PlayerTeam { get; private set; } // Reference to the player-controlled team within AllTeams

        // --- Constants ---
        private const string SAVE_FILE_NAME = "handball_manager_save.json";
        // Use configurable season dates or constants
        private static readonly DateTime SEASON_START_DATE = new DateTime(DateTime.Now.Year, 7, 1); // July 1st
        private static readonly DateTime OFFSEASON_START_DATE = new DateTime(DateTime.Now.Year, 6, 1); // June 1st

        // --- Unity Methods ---
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("Duplicate GameManager detected. Destroying new instance.");
                Destroy(this.gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(this.gameObject);

            InitializeSystems();
        }

        private void Start()
        {
            Debug.Log("GameManager Started. Initial State: " + CurrentState);
            UIManagerRef?.UpdateUIForGameState(CurrentState); // Ensure UI matches initial state
        }

        private void Update()
        {
            // Simple state-based updates or debug controls
            if (Input.GetKeyDown(KeyCode.Space) && IsInActivePlayState())
            {
                AdvanceTime();
            }
             else if (Input.GetKeyDown(KeyCode.M) && IsInActivePlayState() && PlayerTeam != null)
             {
                 SimulateNextPlayerMatch(); // Debug key to simulate the *next scheduled match*
             }
             else if (Input.GetKeyDown(KeyCode.F5)) // Quick Save
             {
                 SaveGame();
             }
             else if (Input.GetKeyDown(KeyCode.F9)) // Quick Load
             {
                 LoadGame();
             }
        }

        // --- Initialization ---
        private void InitializeSystems()
        {
            // Time Manager first
            TimeManager = new TimeManager(new DateTime(2024, 7, 1)); // Default start date

            // Find essential MonoBehaviour systems
            UIManagerRef = UIManager.Instance;
            if (UIManagerRef == null) Debug.LogError("UIManager could not be found or created!");

            // Create instances of core non-MonoBehaviour systems
            MatchEngine = new MatchEngine();
            TrainingSimulator = new TrainingSimulator();
            MoraleSimulator = new MoraleSimulator();
            PlayerDevelopment = new PlayerDevelopment();
            TransferManager = new TransferManager();
            ContractManager = new ContractManager();
            LeagueManager = new LeagueManager();           // Instantiate
            ScheduleManager = new ScheduleManager();       // Instantiate
            FinanceManager = new FinanceManager();        // Instantiate

            Debug.Log("Core systems initialized.");

            // Subscribe to TimeManager events AFTER all systems are initialized
            TimeManager.OnDayAdvanced += HandleDayAdvanced;
            TimeManager.OnWeekAdvanced += HandleWeekAdvanced;
            TimeManager.OnMonthAdvanced += HandleMonthAdvanced;
        }

        // --- Game State Management ---
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            GameState previousState = CurrentState;
            Debug.Log($"Game State Changing: {previousState} -> {newState}");
            CurrentState = newState;

            // Pause/Resume Time based on state
            if (TimeManager != null) {
                bool shouldBePaused = !IsInActivePlayState() && newState != GameState.SimulatingMatch;
                TimeManager.IsPaused = shouldBePaused;
            }

            // Trigger actions based on state change ENTERING newState
            switch (newState)
            {
                case GameState.MainMenu:
                    // TODO: Unload game data if returning from a game (clear lists etc.)
                    break;
                case GameState.Loading:
                    // Loading visualization is handled by UIManager potentially
                    break;
                case GameState.SimulatingMatch:
                    // Time is paused by the check above
                    break;
                // Other states generally just need UI update
                case GameState.InSeason:
                case GameState.OffSeason:
                case GameState.TransferWindow:
                case GameState.ManagingTeam:
                case GameState.MatchReport:
                case GameState.Paused:
                    break;
            }

             // Update UI to reflect the new state AFTER state logic
             UIManagerRef?.UpdateUIForGameState(newState);
        }

        /// <summary>Helper to check if the game is in a state where time can advance.</summary>
        private bool IsInActivePlayState()
        {
             return CurrentState == GameState.InSeason
                 || CurrentState == GameState.ManagingTeam
                 || CurrentState == GameState.TransferWindow
                 || CurrentState == GameState.OffSeason
                 || CurrentState == GameState.MatchReport; // Allow advancing from report screen
        }

        // --- Game Actions ---
        public void StartNewGame()
        {
            Debug.Log("Starting New Game...");
            ChangeState(GameState.Loading);
            UIManagerRef?.DisplayPopup("Loading New Game...");

            // 1. Clear Existing Data
            AllLeagues.Clear(); AllTeams.Clear(); AllPlayers.Clear(); AllStaff.Clear(); PlayerTeam = null;
            LeagueManager?.ResetTablesForNewSeason(); // Ensure tables are clear
            ScheduleManager?.HandleSeasonTransition(); // Clear old schedule

            // 2. Load Default Database
            LoadDefaultDatabase(); // Populates the lists

            // 3. Assign Player Team
            if (AllTeams.Count > 0) {
                PlayerTeam = AllTeams[0]; // Simplified: Assign first team
                Debug.Log($"Player assigned control of team: {PlayerTeam.Name} (ID: {PlayerTeam.TeamID})");
            } else {
                Debug.LogError("No teams loaded in database! Cannot start new game.");
                ChangeState(GameState.MainMenu); UIManagerRef?.DisplayPopup("Error: No teams found in database!");
                return;
            }

            // 4. Set Initial Time
            TimeManager.SetDate(new DateTime(2024, 7, 1)); // Standard start date

            // 5. Initial Setup (schedule, league tables)
            ScheduleManager?.GenerateNewSchedule(); // Generate AFTER teams are loaded
            foreach(var league in AllLeagues) { // Initialize tables for all leagues
                LeagueManager?.InitializeLeagueTable(league.LeagueID, true);
            }

            // 6. Transition to Initial Game State
            Debug.Log("New Game Setup Complete.");
            ChangeState(GameState.OffSeason); // Start in OffSeason
            if(UIManagerRef != null && PlayerTeam != null) {
                UIManagerRef.ShowTeamScreen(PlayerTeam); // Show team screen initially
                ChangeState(GameState.ManagingTeam); // Set state to managing team
            }
        }

        /// <summary>Loads initial data into the game lists. Placeholder implementation.</summary>
        private void LoadDefaultDatabase()
        {
            Debug.Log("Loading Default Database (Placeholders)...");
            // TODO: Replace with actual loading from files (ScriptableObjects, JSON, etc.)

            AllLeagues.Add(new LeagueData { LeagueID = 1, Name = "Handball Premier League" });

            TeamData pTeam = CreatePlaceholderTeam(1, "HC Player United", 5000, 1000000);
            AllTeams.Add(pTeam);
            AllPlayers.AddRange(pTeam.Roster);

            for (int i = 2; i <= 8; i++) {
                TeamData aiTeam = CreatePlaceholderTeam(i, $"AI Team {i-1}", 4000 + (i*100), 750000 - (i*20000));
                aiTeam.LeagueID = 1;
                AllTeams.Add(aiTeam);
                AllPlayers.AddRange(aiTeam.Roster);
            }
             Debug.Log($"Loaded {AllLeagues.Count} leagues, {AllTeams.Count} teams, {AllPlayers.Count} players.");
        }


        public void LoadGame()
        {
            string filePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"Save file not found at: {filePath}"); UIManagerRef?.DisplayPopup("No save file found."); return;
            }

            Debug.Log($"Loading Game from {filePath}...");
            ChangeState(GameState.Loading); UIManagerRef?.DisplayPopup("Loading Game...");

            try
            {
                string json = File.ReadAllText(filePath);
                SaveData saveData = JsonUtility.FromJson<SaveData>(json);

                if (saveData != null)
                {
                    // Restore Data Lists (Clear existing first)
                    AllLeagues = saveData.Leagues ?? new List<LeagueData>();
                    AllTeams = saveData.Teams ?? new List<TeamData>();
                    AllPlayers = saveData.Players ?? new List<PlayerData>();
                    AllStaff = saveData.Staff ?? new List<StaffData>();

                    // Restore Time
                    TimeManager.SetDate(new DateTime(saveData.CurrentDateTicks));

                    // Restore Player Team Reference
                    PlayerTeam = AllTeams.FirstOrDefault(t => t.TeamID == saveData.PlayerTeamID);
                    if (PlayerTeam == null && AllTeams.Count > 0) {
                        Debug.LogWarning($"Saved Player Team ID {saveData.PlayerTeamID} not found, assigning first team."); PlayerTeam = AllTeams[0];
                    }

                    // Restore LeagueManager state from lists
                    Dictionary<int, List<LeagueStandingEntry>> loadedTables = new Dictionary<int, List<LeagueStandingEntry>>();
                    if (saveData.LeagueTableKeys != null && saveData.LeagueTableValues != null && saveData.LeagueTableKeys.Count == saveData.LeagueTableValues.Count) {
                        for(int i=0; i<saveData.LeagueTableKeys.Count; i++) {
                            loadedTables.Add(saveData.LeagueTableKeys[i], saveData.LeagueTableValues[i]);
                        }
                    }
                    LeagueManager?.RestoreTablesFromSave(loadedTables);

                    // TODO: Restore ScheduleManager state if saving it

                    // Restore Game State (Set directly before ChangeState triggers UI/logic)
                    CurrentState = saveData.CurrentGameState;

                    Debug.Log($"Game Loaded Successfully. Date: {TimeManager.CurrentDate.ToShortDateString()}, State: {CurrentState}");
                    ChangeState(CurrentState); // Trigger state logic and UI update for loaded state
                } else { throw new Exception("Failed to deserialize save data (SaveData is null)."); }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading game: {e.Message}\n{e.StackTrace}");
                UIManagerRef?.DisplayPopup($"Error loading game: {e.Message}");
                 // Revert to main menu on failure
                 AllLeagues.Clear(); AllTeams.Clear(); AllPlayers.Clear(); AllStaff.Clear(); PlayerTeam = null;
                 InitializeSystems(); // Re-initialize to default state? Risky if systems hold refs. Better to restart app?
                ChangeState(GameState.MainMenu);
            }
        }

        public void SaveGame()
        {
             if (!IsInActivePlayState() && CurrentState != GameState.MainMenu && CurrentState != GameState.Paused) {
                 Debug.LogWarning($"Cannot save game in current state: {CurrentState}");
                 UIManagerRef?.DisplayPopup($"Cannot save in state: {CurrentState}"); return;
             }

             string filePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
             Debug.Log($"Saving Game to {filePath}...");
             UIManagerRef?.DisplayPopup("Saving Game..."); // Temporary popup

             try
             {
                 // Create SaveData object and populate it
                 SaveData saveData = new SaveData {
                     CurrentGameState = this.CurrentState, CurrentDateTicks = TimeManager.CurrentDate.Ticks,
                     Leagues = this.AllLeagues, Teams = this.AllTeams, Players = this.AllPlayers, Staff = this.AllStaff,
                     PlayerTeamID = this.PlayerTeam?.TeamID ?? -1
                 };

                 // Prepare LeagueManager data for save (convert dictionary to lists)
                 var leagueTables = LeagueManager?.GetTablesForSave();
                 if (leagueTables != null) {
                     saveData.LeagueTableKeys = leagueTables.Keys.ToList();
                     saveData.LeagueTableValues = leagueTables.Values.ToList();
                 } else {
                      saveData.LeagueTableKeys = new List<int>();
                      saveData.LeagueTableValues = new List<List<LeagueStandingEntry>>();
                 }

                 // TODO: Prepare ScheduleManager data if saving it

                 // Serialize to JSON
                 string json = JsonUtility.ToJson(saveData, true); // Pretty print for debug

                 // Write to file
                 File.WriteAllText(filePath, json);

                 Debug.Log("Game Saved Successfully.");
                 UIManagerRef?.DisplayPopup("Game Saved!");
             }
             catch (Exception e)
             {
                 Debug.LogError($"Error saving game: {e.Message}\n{e.StackTrace}");
                 UIManagerRef?.DisplayPopup($"Error saving game: {e.Message}");
             }
        }


        /// <summary>Advances time by one day, triggering relevant daily updates.</summary>
        public void AdvanceTime()
        {
            if (!IsInActivePlayState()) { Debug.LogWarning($"Cannot advance time in state: {CurrentState}"); return; }
            TimeManager?.AdvanceDay(); // Events trigger daily processing
        }

        // --- Event Handlers ---
        private void HandleDayAdvanced()
        {
            // 1. Check for scheduled matches today
            List<MatchInfo> matchesToday = ScheduleManager?.GetMatchesForDate(TimeManager.CurrentDate) ?? new List<MatchInfo>();
            bool playerMatchSimulatedToday = false;
            foreach (var matchInfo in matchesToday)
            {
                if (playerMatchSimulatedToday) break; // Only process one player match per day step

                 TeamData home = AllTeams.FirstOrDefault(t => t.TeamID == matchInfo.HomeTeamID);
                 TeamData away = AllTeams.FirstOrDefault(t => t.TeamID == matchInfo.AwayTeamID);
                 if (home != null && away != null) {
                     bool isPlayerMatch = (home == PlayerTeam || away == PlayerTeam);
                     if (isPlayerMatch) {
                          Debug.Log($"Player match scheduled today: {home.Name} vs {away.Name}. Triggering simulation.");
                          SimulateMatch(home, away); // This changes state and pauses time
                          playerMatchSimulatedToday = true;
                     } else {
                          // AI vs AI match - Simulate silently in the background
                          SimulateMatch(home, away);
                     }
                 } else { Debug.LogWarning($"Could not find teams for scheduled match: HomeID={matchInfo.HomeTeamID}, AwayID={matchInfo.AwayTeamID}"); }
            }

            // If a player match was triggered and paused time, stop further daily processing
            if (playerMatchSimulatedToday) return;

            // --- Continue Daily Processing if no player match paused time ---

            // 2. Update player injury status (ALL players)
             foreach (var player in AllPlayers) { player.UpdateInjuryStatus(); }

            // 3. Process transfer/contract daily steps (Placeholders)
             // TransferManager?.ProcessDaily();
             // ContractManager?.ProcessDaily();

            // 4. Update player condition (non-training recovery)
              foreach (var player in AllPlayers) {
                 if (!player.IsInjured() && player.Condition < 1.0f) {
                     player.Condition = Mathf.Clamp(player.Condition + 0.02f * (player.NaturalFitness / 75f), 0.1f, 1f);
                 }
              }
             // 5. News Generation TODO
        }

         private void HandleWeekAdvanced()
         {
             Debug.Log($"GameManager handling Week Advanced: Week starting {TimeManager.CurrentDate.ToShortDateString()}");
             if (LeagueManager == null || FinanceManager == null || TrainingSimulator == null || MoraleSimulator == null) {
                 Debug.LogError("One or more managers are null during HandleWeekAdvanced!"); return;
             }

             // 1. Simulate Training for ALL Teams
             foreach (var team in AllTeams) {
                 TrainingFocus focus = (team == PlayerTeam) ? TrainingFocus.General : GetAITrainingFocus(team); // TODO: Get player focus setting
                 TrainingSimulator.SimulateWeekTraining(team, focus);
             }

             // 2. Update Morale for ALL Teams
              foreach (var team in AllTeams) { MoraleSimulator.UpdateMoraleWeekly(team); }

             // 3. Update Finances for ALL Teams
             FinanceManager.ProcessWeeklyPayments(AllTeams);

             // 4. Update League Tables
             LeagueManager.UpdateStandings(); // Recalculate and sort tables based on results processed daily
         }

        private void HandleMonthAdvanced()
        {
            Debug.Log($"GameManager handling Month Advanced: New Month {TimeManager.CurrentDate:MMMM yyyy}");
            if (FinanceManager == null) { Debug.LogError("FinanceManager is null during HandleMonthAdvanced!"); return; }

            // 1. Monthly Finances
            FinanceManager.ProcessMonthly(AllTeams);

            // 2. Scouting / Youth Dev TODOs...

            // 3. Check Season Transition
             CheckSeasonTransition();
        }

        // --- Simulation Trigger ---
        public void SimulateMatch(TeamData home, TeamData away)
        {
            if (MatchEngine == null) { Debug.LogError("MatchEngine is not initialized!"); return; }
            if (home == null || away == null) { Debug.LogError("Cannot simulate match with null teams."); return; }
            if (LeagueManager == null || MoraleSimulator == null) { Debug.LogError("LeagueManager or MoraleSimulator is null during SimulateMatch!"); return; }


            bool isPlayerMatch = (home == PlayerTeam || away == PlayerTeam);

            // If it's player match, change state BEFORE simulation starts
            if (isPlayerMatch) { ChangeState(GameState.SimulatingMatch); }

            // Debug.Log($"Simulating Match: {home.Name} vs {away.Name} on {TimeManager.CurrentDate.ToShortDateString()}"); // Less verbose logging maybe

            Tactic homeTactic = home.CurrentTactic ?? new Tactic();
            Tactic awayTactic = away.CurrentTactic ?? new Tactic();

            // *** RUN SIMULATION ***
            MatchResult result = MatchEngine.SimulateMatch(home, away, homeTactic, awayTactic);
            result.MatchDate = TimeManager.CurrentDate; // Ensure date is set

            Debug.Log($"Match Result: {result}"); // Log the basic scoreline

            // --- Post-Match Processing ---
            // 1. Update Morale
            MoraleSimulator.UpdateMoralePostMatch(home, result);
            MoraleSimulator.UpdateMoralePostMatch(away, result);

            // 2. Apply Fatigue
            Action<TeamData> processFatigue = (team) => {
                 if(team?.Roster != null) {
                     // Apply to players assumed to have played (needs better tracking from MatchEngine ideally)
                     foreach(var p in team.Roster.Take(10)) { // Simple: affect first 10 players
                         if (!p.IsInjured()) p.Condition = Mathf.Clamp(p.Condition - UnityEngine.Random.Range(0.1f, 0.25f), 0.1f, 1.0f);
                     }
                 }
            };
            processFatigue(home);
            processFatigue(away);

            // 3. Update League Tables
            LeagueManager.ProcessMatchResult(result); // Send result to league manager

            // 4. Generate News TODO

            // --- Update UI and State for Player Match ---
            if (isPlayerMatch) {
                UIManagerRef?.ShowMatchPreview(result); // Show results panel
                ChangeState(GameState.MatchReport); // Go to report state (Time remains paused)
            }
            // AI vs AI match processing finishes here for GameManager. Time continues if not paused by player match.
        }

        // --- SimulateNextPlayerMatch() --- (Debug Helper)
        private void SimulateNextPlayerMatch() {
            if (PlayerTeam == null || ScheduleManager == null) return;

            List<MatchInfo> upcoming = ScheduleManager.GetMatchesForDate(TimeManager.CurrentDate);
            DateTime checkDate = TimeManager.CurrentDate;
            int safety = 0;
            // Find next match involving player team, starting from today
            while (!upcoming.Any(m => m.HomeTeamID == PlayerTeam.TeamID || m.AwayTeamID == PlayerTeam.TeamID) && safety < 365) {
                checkDate = checkDate.AddDays(1);
                upcoming = ScheduleManager.GetMatchesForDate(checkDate);
                safety++;
            }

            var nextMatch = upcoming.FirstOrDefault(m => m.HomeTeamID == PlayerTeam.TeamID || m.AwayTeamID == PlayerTeam.TeamID);

            if (nextMatch.Date != default) {
                TeamData home = AllTeams.FirstOrDefault(t => t.TeamID == nextMatch.HomeTeamID);
                TeamData away = AllTeams.FirstOrDefault(t => t.TeamID == nextMatch.AwayTeamID);
                if(home != null && away != null) {
                    if (nextMatch.Date == TimeManager.CurrentDate) {
                        Debug.Log($"Debug: Simulating player match scheduled for today: {home.Name} vs {away.Name}");
                         SimulateMatch(home, away); // Will change state and pause
                    } else {
                         Debug.LogWarning($"Next player match is on {nextMatch.Date.ToShortDateString()}, not today ({TimeManager.CurrentDate.ToShortDateString()}). Use Spacebar to advance time.");
                    }
                } else { Debug.LogWarning($"Could not find teams for next player match: {nextMatch.HomeTeamID} vs {nextMatch.AwayTeamID}");}
            } else { Debug.Log("Debug: No upcoming player match found in schedule."); }
        }


        // --- Season Transition Logic ---
        private void CheckSeasonTransition()
         {
             DateTime currentDate = TimeManager.CurrentDate;
             int currentYear = currentDate.Year;
             DateTime offSeasonStart = new DateTime(currentYear, OFFSEASON_START_DATE.Month, OFFSEASON_START_DATE.Day);
             DateTime newSeasonStart = new DateTime(currentYear, SEASON_START_DATE.Month, SEASON_START_DATE.Day);
             DateTime nextSeasonStartCheck = newSeasonStart;

             if (currentDate.Date >= newSeasonStart.Date) { // If it's July 1st or later this year...
                 offSeasonStart = offSeasonStart.AddYears(1); // ...off-season starts next year...
                 nextSeasonStartCheck = newSeasonStart.AddYears(1); // ...and next season starts next year.
             }
             // Else (before July 1st), use current year's dates for checks.

             // Trigger OffSeason start?
             if (CurrentState == GameState.InSeason && currentDate.Date >= offSeasonStart.Date && currentDate.Date < nextSeasonStartCheck.Date) {
                 StartOffSeason();
             }
             // Trigger New Season start? (Only on the exact date)
             else if ((CurrentState == GameState.OffSeason || CurrentState == GameState.MainMenu) && currentDate.Date == newSeasonStart.Date) {
                  StartNewSeason();
             }
         }

        private void StartOffSeason()
        {
            if (CurrentState == GameState.OffSeason) return; // Avoid double trigger
            Debug.Log($"--- Starting Off-Season {TimeManager.CurrentDate.Year} ---");
            ChangeState(GameState.OffSeason);

            LeagueManager?.FinalizeSeason(); // Awards, Promotions/Relegations

            // ContractManager?.ProcessExpiries(AllPlayers, AllStaff, AllTeams); // TODO

            foreach(var player in AllPlayers) { PlayerDevelopment?.ProcessAnnualDevelopment(player); }

            // Staff Expiries TODO
            // News TODO

            // Generate New Schedule (clears old one implicitly)
             ScheduleManager?.GenerateNewSchedule();

            UIManagerRef?.DisplayPopup("Off-Season has begun!");
        }

        private void StartNewSeason()
        {
             if (CurrentState == GameState.InSeason) return; // Avoid double trigger
             Debug.Log($"--- Starting New Season {TimeManager.CurrentDate.Year}/{(TimeManager.CurrentDate.Year + 1)} ---");
             ChangeState(GameState.InSeason);

             // Ensure League Tables are ready/reset for the new season
             // FinalizeSeason might have already reset them, or init here if needed.
             foreach(var league in AllLeagues) {
                 LeagueManager?.InitializeLeagueTable(league.LeagueID, true); // Re-initialize based on current team league IDs
             }

             // League Structure Updates (Promotions reflected in TeamData.LeagueID) TODO
             // Season Objectives TODO
             // News TODO
             // Transfer Window updates TODO

             UIManagerRef?.DisplayPopup("The new season has started!");
        }


        // --- OnDestroy ---
        private void OnDestroy()
        {
             if (TimeManager != null) {
                TimeManager.OnDayAdvanced -= HandleDayAdvanced; TimeManager.OnWeekAdvanced -= HandleWeekAdvanced; TimeManager.OnMonthAdvanced -= HandleMonthAdvanced;
             }
             if (_instance == this) { _instance = null; }
         }


        // --- Helper Methods ---
        private TeamData CreatePlaceholderTeam(int id, string name, int reputation, float budget)
        {
            TeamData team = new TeamData { TeamID = id, Name = name, Reputation = reputation, Budget = budget, LeagueID = 1 };
            team.CurrentTactic = new Tactic { TacticName = "Balanced Default" };
            team.Roster = new List<PlayerData>();
            // Add players (Ensure PlayerData constructor assigns ID)
            team.AddPlayer(CreatePlaceholderPlayer(name + " GK", PlayerPosition.Goalkeeper, 25, 65, 75, team.TeamID));
            team.AddPlayer(CreatePlaceholderPlayer(name + " PV", PlayerPosition.Pivot, 28, 70, 72, team.TeamID));
            team.AddPlayer(CreatePlaceholderPlayer(name + " LB", PlayerPosition.LeftBack, 22, 72, 85, team.TeamID));
            team.AddPlayer(CreatePlaceholderPlayer(name + " RW", PlayerPosition.RightWing, 24, 68, 78, team.TeamID));
            team.AddPlayer(CreatePlaceholderPlayer(name + " CB", PlayerPosition.CentreBack, 26, 75, 78, team.TeamID));
            team.AddPlayer(CreatePlaceholderPlayer(name + " RB", PlayerPosition.RightBack, 23, 66, 82, team.TeamID));
            team.AddPlayer(CreatePlaceholderPlayer(name + " LW", PlayerPosition.LeftWing, 21, 69, 88, team.TeamID));
            for(int i=0; i<7; i++) {
                 PlayerPosition pos = (PlayerPosition)(i % 7);
                 if(pos == PlayerPosition.Goalkeeper) pos = PlayerPosition.Pivot; // Avoid too many GKs
                 team.AddPlayer(CreatePlaceholderPlayer(name + $" Sub{i+1}", pos, UnityEngine.Random.Range(19, 29), UnityEngine.Random.Range(50, 65), UnityEngine.Random.Range(60, 80), team.TeamID));
             }
            team.UpdateWageBill();
            return team;
        }

        private PlayerData CreatePlaceholderPlayer(string name, PlayerPosition pos, int age, int caEstimate, int pa, int? teamId)
        {
            // Assumes PlayerData constructor handles ID generation
            PlayerData player = new PlayerData {
                FirstName = name.Contains(" ") ? name.Split(' ')[0] : name, LastName = name.Contains(" ") ? name.Split(' ')[1] : "Player", Age = age,
                PrimaryPosition = pos, PotentialAbility = pa, CurrentTeamID = teamId,
                ShootingAccuracy = Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-5, 5), 30, 90), Passing = Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-5, 5), 30, 90),
                Speed = Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-10, 10), 30, 90), Strength = Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-10, 10), 30, 90),
                DecisionMaking = Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-5, 5), 30, 90),
                Reflexes = (pos == PlayerPosition.Goalkeeper) ? Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-5, 5), 30, 90) : 20,
                PositioningGK = (pos == PlayerPosition.Goalkeeper) ? Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-5, 5), 30, 90) : 20,
                Wage = 1000 + (caEstimate * UnityEngine.Random.Range(40, 60)), ContractExpiryDate = TimeManager.CurrentDate.AddYears(UnityEngine.Random.Range(1, 4)),
                Morale = UnityEngine.Random.Range(0.6f, 0.8f), Condition = 1.0f, Resilience = UnityEngine.Random.Range(40, 85)
            };
            // player.CalculateCurrentAbility(); // Constructor should call this
            return player;
        }

        private TrainingFocus GetAITrainingFocus(TeamData team) {
             Array values = Enum.GetValues(typeof(TrainingFocus));
             return (TrainingFocus)values.GetValue(UnityEngine.Random.Range(0, values.Length - 1)); // Exclude YouthDevelopment for now
        }

    } // End GameManager Class
} 