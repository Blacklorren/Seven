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
using HandballManager.Core.MatchData;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Core; // For MatchInfo struct (adjust namespace if different)
using HandballManager.Simulation.Core.Interfaces;
using System.Threading;
using HandballManager.Simulation.Core.Exceptions;

// Ensure required namespaces exist, even if classes are basic placeholders
namespace HandballManager.Management { public class FinanceManager { public void ProcessWeeklyPayments(List<TeamData> allTeams) { /* TODO */ } public void ProcessMonthly(List<TeamData> allTeams) { /* TODO */ } } }

// Ensure required data structures exist
// Note: LeagueStandingEntry is now defined in HandballManager.Management namespace


namespace HandballManager.Core
{
        // Basic placeholder LeagueData
    [Serializable]
    public class LeagueData { public int LeagueID; public string Name; /* Add standings, teams list etc. */ }
    // Basic placeholder MatchInfo for schedule (ensure namespace matches if defined elsewhere)
    namespace MatchData
    {
        [Serializable]
        public struct MatchInfo
        {
            public DateTime Date;
            public int HomeTeamID;
            public int AwayTeamID;
            public string Location;
            public string Referee;

            public override bool Equals(object obj)
            {
                if (obj is MatchInfo other)
                {
                    return Date == other.Date &&
                           HomeTeamID == other.HomeTeamID &&
                           AwayTeamID == other.AwayTeamID;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Date, HomeTeamID, AwayTeamID);
            }

            public static bool operator ==(MatchInfo left, MatchInfo right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(MatchInfo left, MatchInfo right)
            {
                return !(left == right);
            }
        }
    }

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
                    _instance = FindFirstObjectByType<GameManager>();
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
        
        // --- Service Container ---
        private IServiceContainer _serviceContainer;
        
        // --- Core System References ---
        // These properties provide backward compatibility during refactoring
        public TimeManager TimeManager => _serviceContainer.Get<TimeManager>();
        public UIManager UIManagerRef { get; private set; } // Keep direct reference for UI updates
        
        // These properties will be removed after full migration to DI
        private MatchEngine _matchEngine => _serviceContainer.Get<Simulation.Core.Interfaces.IMatchEngine>() as MatchEngine;
        private TrainingSimulator _trainingSimulator => _serviceContainer.Get<TrainingSimulator>();
        private MoraleSimulator _moraleSimulator => _serviceContainer.Get<MoraleSimulator>();
        private PlayerDevelopment _playerDevelopment => _serviceContainer.Get<PlayerDevelopment>();
        private TransferManager _transferManager => _serviceContainer.Get<TransferManager>();
        private ContractManager _contractManager => _serviceContainer.Get<ContractManager>();
        private LeagueManager _leagueManager => _serviceContainer.Get<LeagueManager>();
        private ScheduleManager _scheduleManager => _serviceContainer.Get<ScheduleManager>();
        private FinanceManager _financeManager => _serviceContainer.Get<FinanceManager>();

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
                SimulateNextPlayerMatch(); // Debug key to simulate the next scheduled match
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
            // Initialize service container
            _serviceContainer = new ServiceContainer();
            
            // Register event bus first
            _serviceContainer.Bind<IEventBus, EventBus>();
            
            // Register core services
            var timeManager = new TimeManager(new DateTime(2024, 7, 1)); // Default start date
            _serviceContainer.BindInstance(timeManager);
            
            // Find essential MonoBehaviour systems
            UIManagerRef = UIManager.Instance;
            if (UIManagerRef == null) Debug.LogError("UIManager could not be found or created!");
            _serviceContainer.BindInstance(UIManagerRef);

            // Register simulation services
            _serviceContainer.Bind<IMatchEngine, MatchEngine>();
            _serviceContainer.Bind<IMatchSimulationCoordinator, MatchSimulationCoordinator>();
            _serviceContainer.BindInstance<TrainingSimulator>(new TrainingSimulator());
            _serviceContainer.BindInstance<MoraleSimulator>(new MoraleSimulator());
            _serviceContainer.BindInstance<PlayerDevelopment>(new PlayerDevelopment());
            _serviceContainer.BindInstance<TransferManager>(new TransferManager());
            _serviceContainer.BindInstance<ContractManager>(new ContractManager());
            _serviceContainer.BindInstance<LeagueManager>(new LeagueManager());
            _serviceContainer.BindInstance<ScheduleManager>(new ScheduleManager());
            _serviceContainer.BindInstance<FinanceManager>(new FinanceManager());
            
            // Install simulation bindings
            var simulationInstaller = gameObject.AddComponent<Installers.SimulationInstaller>();
            simulationInstaller.InstallBindings(_serviceContainer);
            
            Debug.Log("Core systems initialized with dependency injection.");
            
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
            
            // Publish state change event before updating state
            _serviceContainer.Get<IEventBus>().Publish(new Simulation.Core.Events.GameStateChangedEvent {
                OldState = CurrentState,
                NewState = newState
            });
            
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
        public void SimulateNextPlayerMatch()
        {
            if (PlayerTeam == null) return;

            // Get next scheduled match for player team
            var nextMatch = _scheduleManager.GetMatchesForDate(TimeManager.CurrentDate)
                .FirstOrDefault(m => m.HomeTeamID == PlayerTeam.TeamID || m.AwayTeamID == PlayerTeam.TeamID);

            if(nextMatch != default(MatchInfo))
            {
                // Get team references from IDs
                var homeTeam = AllTeams.FirstOrDefault(t => t.TeamID == nextMatch.HomeTeamID);
                var awayTeam = AllTeams.FirstOrDefault(t => t.TeamID == nextMatch.AwayTeamID);

                if (homeTeam != null && awayTeam != null)
                {
                    ChangeState(GameState.SimulatingMatch);
                    var progress = new Progress<float>();
                    var cancellationToken = new CancellationTokenSource().Token;
                    _matchEngine.SimulateMatch(homeTeam, awayTeam, homeTeam.CurrentTactic, awayTeam.CurrentTactic,
                        UnityEngine.Random.Range(1, 999999), progress, cancellationToken);
                    ChangeState(GameState.MatchReport);
                }
                else
                {
                    Debug.LogError("Could not find teams for match simulation");
                }
            }
            else
            {
                Debug.Log("No scheduled matches found for player team on current date");
            }
        }
        public void StartNewGame()
        {
            Debug.Log("Starting New Game...");
            ChangeState(GameState.Loading);
            UIManagerRef?.DisplayPopup("Loading New Game...");

            // 1. Clear Existing Data
            AllLeagues.Clear(); AllTeams.Clear(); AllPlayers.Clear(); AllStaff.Clear(); PlayerTeam = null;
            _leagueManager?.ResetTablesForNewSeason(); // Ensure tables are clear
            _scheduleManager?.HandleSeasonTransition(); // Clear old schedule

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
            _scheduleManager?.GenerateNewSchedule(); // Generate AFTER teams are loaded
            foreach(var league in AllLeagues) { // Initialize tables for all leagues
                _leagueManager?.InitializeLeagueTable(league.LeagueID, true);
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
            // Get the SaveDataManager from the service container
            var saveDataManager = _serviceContainer.Get<SaveDataManager>();
            if (saveDataManager == null)
            {
                // Create and register if not already in container
                saveDataManager = new SaveDataManager();
                _serviceContainer.BindInstance(saveDataManager);
            }
            
            // Get the most recent save file path
            string filePath = saveDataManager.GetMostRecentSavePath();
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("No save files found."); 
                UIManagerRef?.DisplayPopup("No save files found."); 
                return;
            }

            Debug.Log($"Loading Game from {filePath}...");
            ChangeState(GameState.Loading); 
            UIManagerRef?.DisplayPopup("Loading Game...");

            try
            {
                // Load the game using the SaveDataManager
                SaveData saveData = saveDataManager.LoadGame(filePath);

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
                        Debug.LogWarning($"Saved Player Team ID {saveData.PlayerTeamID} not found, assigning first team."); 
                        PlayerTeam = AllTeams[0];
                    }

                    // Restore LeagueManager state from lists
                    Dictionary<int, List<LeagueStandingEntry>> loadedTables = new Dictionary<int, List<LeagueStandingEntry>>();
                    if (saveData.LeagueTableKeys != null && saveData.LeagueTableValues != null && saveData.LeagueTableKeys.Count == saveData.LeagueTableValues.Count) {
                        for(int i=0; i<saveData.LeagueTableKeys.Count; i++) {
                            loadedTables.Add(saveData.LeagueTableKeys[i], saveData.LeagueTableValues[i]);
                        }
                    }
                    _leagueManager?.RestoreTablesFromSave(loadedTables);

                    // TODO: Restore ScheduleManager state if saving it

                    // Restore Game State (Set directly before ChangeState triggers UI/logic)
                    CurrentState = saveData.CurrentGameState;

                    Debug.Log($"Game Loaded Successfully. Date: {TimeManager.CurrentDate.ToShortDateString()}, State: {CurrentState}");

                    // Publish load completed event
                    _serviceContainer.Get<IEventBus>().Publish(new Simulation.Core.Events.GameStateChangedEvent
                    {
                        OldState = GameState.Loading,
                        NewState = CurrentState
                    });

                    // Trigger state logic and UI update for loaded state
                    ChangeState(CurrentState); 
                } else { 
                    throw new Exception("Failed to deserialize save data (SaveData is null)."); 
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading game: {e.Message}\n{e.StackTrace}");
                UIManagerRef?.DisplayPopup($"Error loading game: {e.Message}");
                // Revert to main menu on failure
                AllLeagues.Clear(); AllTeams.Clear(); AllPlayers.Clear(); AllStaff.Clear(); PlayerTeam = null;
                InitializeSystems(); // Re-initialize to default state
                ChangeState(GameState.MainMenu);
            }
        }

        public void SaveGame()
        {
             if (!IsInActivePlayState() && CurrentState != GameState.MainMenu && CurrentState != GameState.Paused) {
                 Debug.LogWarning($"Cannot save game in current state: {CurrentState}");
                 UIManagerRef?.DisplayPopup($"Cannot save in state: {CurrentState}"); return;
             }

             Debug.Log("Saving Game...");
             UIManagerRef?.DisplayPopup("Saving Game..."); // Temporary popup

             try
             {
                 // Get the SaveDataManager from the service container
                 var saveDataManager = _serviceContainer.Get<SaveDataManager>();
                 if (saveDataManager == null)
                 {
                     // Create and register if not already in container
                     saveDataManager = new SaveDataManager();
                     _serviceContainer.BindInstance(saveDataManager);
                 }
                 
                 // Get league tables from LeagueManager
                 var leagueTables = _leagueManager?.GetTablesForSave() ?? new Dictionary<int, List<LeagueStandingEntry>>();
                 
                 // Save the game using the SaveDataManager
                 string savePath = saveDataManager.SaveGame(
                     CurrentState,
                     AllTeams,
                     TimeManager,
                     PlayerTeam?.TeamID ?? -1,
                     AllLeagues,
                     AllPlayers,
                     AllStaff,
                     leagueTables
                 );

                 Debug.Log($"Game Saved Successfully to {savePath}.");
                 UIManagerRef?.DisplayPopup("Game Saved!");

                // Publish save completed event
                _serviceContainer.Get<IEventBus>().Publish(new Simulation.Core.Events.GameStateChangedEvent
                {
                    OldState = GameState.Loading,
                    NewState = CurrentState
                });
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
            List<MatchInfo> matchesToday = _scheduleManager?.GetMatchesForDate(TimeManager.CurrentDate) ?? new List<MatchInfo>();
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
             if (_leagueManager == null || _financeManager == null || _trainingSimulator == null || _moraleSimulator == null) {
                 Debug.LogError("One or more managers are null during HandleWeekAdvanced!"); return;
             }

             // 1. Simulate Training for ALL Teams
             foreach (var team in AllTeams) {
                 TrainingFocus focus = (team == PlayerTeam) ? TrainingFocus.General : GetAITrainingFocus(team); // TODO: Get player focus setting
                 _trainingSimulator.SimulateWeekTraining(team, focus);
             }

             // 2. Update Morale for ALL Teams
              foreach (var team in AllTeams) { _moraleSimulator.UpdateMoraleWeekly(team); }

             // 3. Update Finances for ALL Teams
             _financeManager.ProcessWeeklyPayments(AllTeams);

             // 4. Update League Tables
             _leagueManager.UpdateStandings(); // Recalculate and sort tables based on results processed daily
         }

        private void HandleMonthAdvanced()
        {
            Debug.Log($"GameManager handling Month Advanced: New Month {TimeManager.CurrentDate:MMMM yyyy}");
            if (_financeManager == null) { Debug.LogError("FinanceManager is null during HandleMonthAdvanced!"); return; }

            // 1. Monthly Finances
            _financeManager.ProcessMonthly(AllTeams);

            // 2. Scouting / Youth Dev TODOs...

            // 3. Check Season Transition
             CheckSeasonTransition();
        }

        // --- Simulation Trigger ---
        private IMatchSimulationCoordinator _simCoordinator => _serviceContainer.Get<IMatchSimulationCoordinator>();

        public async void SimulateMatch(TeamData home, TeamData away, Tactic homeTactic = null, Tactic awayTactic = null)
        {
            using var cts = new System.Threading.CancellationTokenSource();
            try
            {
                if (_simCoordinator == null)
                {
                    Debug.LogError("Simulation coordinator not available!");
                    return;
                }

                if (home == null || away == null)
                {
                    Debug.LogError("Cannot simulate match with null teams.");
                    return;
                }

                bool isPlayerMatch = (home == PlayerTeam || away == PlayerTeam);
                if (isPlayerMatch)
                {
                    ChangeState(GameState.SimulatingMatch);
                }

                // Handle null tactics safely
                Tactic validatedHomeTactic = homeTactic ?? home?.CurrentTactic ?? new Tactic();
                Tactic validatedAwayTactic = awayTactic ?? away?.CurrentTactic ?? new Tactic();

                // Should handle cancellation before processing results
                MatchResult result = await _simCoordinator.RunSimulationAsync(
                    home,
                    away,
                    validatedHomeTactic,
                    validatedAwayTactic,
                    cts.Token
                ).ConfigureAwait(true); // Keep context for Unity thread

                if (result == null) return;

                result.MatchDate = TimeManager.CurrentDate;
                Debug.Log($"Match Result: {result}");

                // --- Post-Match Processing ---
                // 1. Update Morale
                _moraleSimulator.UpdateMoralePostMatch(home, result);
                _moraleSimulator.UpdateMoralePostMatch(away, result);

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
            _leagueManager.ProcessMatchResult(result); // Send result to league manager

            // 4. Generate News TODO


            // --- Update UI and State for Player Match ---
            if (isPlayerMatch) {
                UIManagerRef?.ShowMatchPreview(result); // Show results panel
                ChangeState(GameState.MatchReport); // Go to report state (Time remains paused)
            }
            _simCoordinator.CleanupResources();
            }
            catch (ValidationException ex)
            {
                HandleInvalidMatchState(ex);
            }
            catch (SimulationException ex) when (ex.ErrorType == SimulationErrorType.RuntimeError)
            {
                HandleRuntimeSimulationFailure(ex);
            }
            finally
            {
                CleanupSimulationResources();
            }
            // AI vs AI match processing finishes here for GameManager. Time continues if not paused by player match.
        }

        /// <summary>
        /// Handles invalid match state exceptions by logging the error and resetting the game state.
        /// </summary>
        private void HandleInvalidMatchState(ValidationException ex)
        {
            Debug.LogError($"Match validation error: {ex.Message}");
            UIManagerRef?.DisplayPopup($"Match simulation failed: {ex.Message}");

            // Reset to a safe state
            ChangeState(GameState.ManagingTeam);

            // Additional cleanup if needed
            CleanupSimulationResources();
        }

        /// <summary>
        /// Handles runtime simulation failures by logging the error and resetting the game state.
        /// </summary>
        private void HandleRuntimeSimulationFailure(SimulationException ex)
        {
            Debug.LogError($"Match simulation runtime error: {ex.Message}");
            UIManagerRef?.DisplayPopup($"Match simulation failed: {ex.Message}");

            // Reset to a safe state
            ChangeState(GameState.ManagingTeam);

            // Additional cleanup if needed
            CleanupSimulationResources();
        }

        /// <summary>
        /// Cleans up any resources used during simulation.
        /// </summary>
        private void CleanupSimulationResources()
        {
            // Release any resources that might be held during simulation
            // This could include temporary data structures, cached results, etc.
            // Currently a placeholder for future implementation
        }


        // --- FindNextPlayerMatch() --- (Debug Helper)
        private MatchInfo FindNextPlayerMatch()
        {
            if (PlayerTeam == null || _scheduleManager == null) return default;

            List<MatchInfo> upcoming = _scheduleManager.GetMatchesForDate(TimeManager.CurrentDate);
            DateTime checkDate = TimeManager.CurrentDate;
            int safety = 0;
            // Find next match involving player team, starting from today
            while (!upcoming.Any(m => m.HomeTeamID == PlayerTeam.TeamID || m.AwayTeamID == PlayerTeam.TeamID) && safety < 365) {
                checkDate = checkDate.AddDays(1);
                upcoming = _scheduleManager.GetMatchesForDate(checkDate);
                safety++;
            }

            return upcoming.FirstOrDefault(m => m.HomeTeamID == PlayerTeam.TeamID || m.AwayTeamID == PlayerTeam.TeamID);
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

            _leagueManager?.FinalizeSeason(); // Awards, Promotions/Relegations

            // ContractManager?.ProcessExpiries(AllPlayers, AllStaff, AllTeams); // TODO

            foreach(var player in AllPlayers) { _playerDevelopment?.ProcessAnnualDevelopment(player); }

            // Staff Expiries TODO
            // News TODO

            // Generate New Schedule (clears old one implicitly)
             _scheduleManager?.GenerateNewSchedule();

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
                 _leagueManager?.InitializeLeagueTable(league.LeagueID, true); // Re-initialize based on current team league IDs
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