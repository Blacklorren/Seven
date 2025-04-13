using UnityEngine;
using UnityEngine.UI; // Required for basic UI components like Button, Text
using TMPro; // Required if using TextMeshPro components
using HandballManager.Data; // To understand data types being shown
using HandballManager.Core; // For GameState access potentially
using System; // For DateTime formatting

namespace HandballManager.UI
{
    /// <summary>
    /// Singleton UI Manager responsible for controlling UI panels, navigation,
    /// and displaying game data.
    /// Assumes UI elements are potentially assigned via the Inspector.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        private static UIManager _instance;
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<UIManager>();
                    if (_instance == null)
                    {
                        // Optionally create one if it doesn't exist, but usually UI is scene-specific
                        // GameObject singletonObject = new GameObject("UIManager");
                        // _instance = singletonObject.AddComponent<UIManager>();
                        Debug.LogError("UIManager instance is null and couldn't find one in the scene. Ensure a UIManager object with the UIManager script exists.");
                    }
                }
                return _instance;
            }
        }

        // --- UI Panel References (Assign in Inspector) ---
        [Header("Main Panels")]
        [Tooltip("The main menu panel with New Game, Load Game etc.")]
        [SerializeField] private GameObject mainMenuPanel;
        [Tooltip("The main gameplay hub screen (e.g., showing office, upcoming match)")]
        [SerializeField] private GameObject hubScreenPanel;
        [Tooltip("Panel for managing team roster, tactics, training etc.")]
        [SerializeField] private GameObject teamScreenPanel;
        [Tooltip("Panel showing match preview or post-match report.")]
        [SerializeField] private GameObject matchInfoPanel;
        [Tooltip("Generic popup panel for messages.")]
        [SerializeField] private GameObject popupPanel;
        // Add references to other panels: TransferScreen, FinancesScreen, LeagueTableScreen etc.

        // --- UI Element References (Assign in Inspector or Find dynamically) ---
        [Header("Common Elements")]
        [Tooltip("Text element to display simple popup messages.")]
        [SerializeField] private TextMeshProUGUI popupText;
        [Tooltip("Button to close the generic popup.")]
        [SerializeField] private Button popupOkButton;
         [Tooltip("Text element to display the current game date.")]
        [SerializeField] private TextMeshProUGUI dateText; // Example: Top bar date display

         [Header("Match Info Panel Elements")]
         [SerializeField] private TextMeshProUGUI matchInfoTitleText;
         [SerializeField] private TextMeshProUGUI matchInfoScoreText;
         // Add Texts for scorers, stats etc.

         [Header("Team Screen Elements")]
         [SerializeField] private TextMeshProUGUI teamScreenTitleText;
         // Add references for Roster list display, tactic selectors, budget display etc.


        // --- Private State ---
        private GameObject _activePanel = null;

        private void Awake()
        {
            // Singleton Setup
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("Duplicate UIManager detected. Destroying new instance.");
                Destroy(this.gameObject);
                return;
            }
             _instance = this;
            // Decide whether UI manager should persist across scenes
            // DontDestroyOnLoad(this.gameObject); // Use if UI elements persist or are dynamically loaded

             // Ensure essential references are set
             if (popupPanel != null && popupOkButton != null)
             {
                 popupOkButton.onClick.AddListener(HidePopup); // Wire up OK button
                 popupPanel.SetActive(false); // Start with popup hidden
             } else {
                  Debug.LogWarning("Popup Panel or OK Button not assigned in UIManager Inspector.");
             }
        }

        private void Start()
        {
            // Initialize UI state based on game state? Usually called by GameManager state change.
            // ShowMainMenu(); // Example: Start on main menu
             HideAllPanels(); // Ensure clean state initially
             // If GameManager exists, maybe ask it for initial state?
             if (GameManager.Instance != null) {
                 UpdateUIForGameState(GameManager.Instance.CurrentState);
             } else {
                 ShowPanel(mainMenuPanel); // Default to main menu if no GameManager yet
             }
        }

         private void Update()
         {
             // Update UI elements that change frequently (like date)
             if (dateText != null && GameManager.Instance?.TimeManager != null)
             {
                 dateText.text = GameManager.Instance.TimeManager.CurrentDate.ToString("dddd, dd MMMM yyyy");
             }
         }


        // --- Panel Management ---

        /// <summary>
        /// Deactivates all managed panels.
        /// </summary>
        private void HideAllPanels()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (hubScreenPanel != null) hubScreenPanel.SetActive(false);
            if (teamScreenPanel != null) teamScreenPanel.SetActive(false);
            if (matchInfoPanel != null) matchInfoPanel.SetActive(false);
            // Deactivate other panels...

            _activePanel = null;
        }

        /// <summary>
        /// Activates the specified panel and deactivates the previously active one.
        /// </summary>
        /// <param name="panelToShow">The GameObject of the panel to activate.</param>
        private void ShowPanel(GameObject panelToShow)
        {
            if (panelToShow == null)
            {
                 Debug.LogWarning("Attempted to show a null panel.");
                 return;
            }
            if (panelToShow == _activePanel) return; // Already showing this panel

            // Hide the currently active panel (if any)
            if (_activePanel != null)
            {
                _activePanel.SetActive(false);
            }

            // Show the new panel
            panelToShow.SetActive(true);
            _activePanel = panelToShow;
             Debug.Log($"[UIManager] Showing Panel: {panelToShow.name}");
        }

         /// <summary>
         /// Updates the visible UI panel based on the current GameState.
         /// Should be called by GameManager when the state changes.
         /// </summary>
         public void UpdateUIForGameState(GameState newState)
         {
             Debug.Log($"[UIManager] Updating UI for Game State: {newState}");
             switch (newState)
             {
                 case GameState.MainMenu:
                     ShowPanel(mainMenuPanel);
                     break;
                 case GameState.Loading:
                     // Optionally show a dedicated loading screen panel
                     // Or just rely on DisplayPopup("Loading...")
                     HideAllPanels(); // Hide main panels during loading
                     break;
                 case GameState.InSeason:
                 case GameState.OffSeason:
                 case GameState.TransferWindow: // Often share the same main 'hub'
                     ShowPanel(hubScreenPanel);
                     break;
                 case GameState.ManagingTeam:
                     ShowPanel(teamScreenPanel);
                     // Data population happens in ShowTeamScreen
                     break;
                 case GameState.MatchReport:
                     ShowPanel(matchInfoPanel);
                     // Data population happens in ShowMatchPreview/Result
                     break;
                 case GameState.SimulatingMatch:
                      // May show a simplified "Match in Progress" overlay or hide main UI
                     HideAllPanels(); // Example: Hide everything during simulation
                     DisplayPopup("Simulating Match..."); // Simple indicator
                     break;
                 case GameState.Paused:
                      // Show pause menu panel?
                     break;
                 default:
                     ShowPanel(hubScreenPanel); // Default fallback
                     break;
             }
         }


        // --- Data Display Methods ---

        /// <summary>
        /// Shows the team management screen and populates it with data.
        /// </summary>
        /// <param name="team">The team data to display.</param>
        public void ShowTeamScreen(TeamData team)
        {
             if (team == null) return;
             if (teamScreenPanel == null) { Debug.LogError("Team Screen Panel not assigned!"); return; }

             Debug.Log($"[UIManager] Populating Team Screen for: {team.Name}");
             ShowPanel(teamScreenPanel);

             // Populate UI elements within the teamScreenPanel
             if (teamScreenTitleText != null) teamScreenTitleText.text = $"{team.Name} - Team Management";
             // TODO: Populate Roster List (needs UI list component - e.g., scroll view with prefabs)
             // TODO: Populate Tactic Display/Selectors (dropdowns, buttons linked to team.CurrentTactic)
             // TODO: Populate Budget/Finance Info Text fields

             // Example: Find a Text component named "BudgetText" within the panel
             TextMeshProUGUI budgetText = teamScreenPanel.GetComponentInChildren<TextMeshProUGUI>(true); // Naive search
             if (budgetText != null && budgetText.name.Contains("Budget")) // Simple check if it's the right one
             {
                budgetText.text = $"Budget: {team.Budget:C}\nWage Bill: {team.WageBill:C}/week"; // Example format
             }
        }

        /// <summary>
        /// Shows the match info panel (preview or result).
        /// </summary>
        /// <param name="matchResult">Match result data.</param>
        public void ShowMatchPreview(MatchResult matchResult) // Renamed from prompt slightly
        {
            if (matchResult == null) return;
            if (matchInfoPanel == null) { Debug.LogError("Match Info Panel not assigned!"); return; }

             Debug.Log($"[UIManager] Populating Match Info Screen: {matchResult}");
             ShowPanel(matchInfoPanel);

             // Populate UI elements
             if (matchInfoTitleText != null) matchInfoTitleText.text = $"Match Result - {matchResult.MatchDate:d MMM yyyy}";
             if (matchInfoScoreText != null) matchInfoScoreText.text = $"{matchResult.HomeTeamName} {matchResult.HomeScore} - {matchResult.AwayScore} {matchResult.AwayTeamName}";
             // TODO: Populate lists of scorers, assists, player ratings, stats etc.
        }

         /// <summary>
        /// Shows the main menu panel. Called via button or GameManager.
        /// </summary>
        public void ShowMainMenu()
        {
            if (mainMenuPanel == null) { Debug.LogError("Main Menu Panel not assigned!"); return; }
            Debug.Log("[UIManager] Showing Main Menu.");
            ShowPanel(mainMenuPanel);
        }


        // --- Popup Management ---

        /// <summary>
        /// Displays a simple popup message to the player.
        /// </summary>
        /// <param name="message">The message to display.</param>
        public void DisplayPopup(string message)
        {
            if (popupPanel == null || popupText == null)
            {
                 Debug.LogWarning("Popup Panel or Text not assigned. Cannot display popup.");
                 // Fallback to console log
                 Debug.Log($"[UI Popup Fallback]: {message}");
                 return;
            }
             Debug.Log($"[UIManager] Displaying Popup: '{message}'");
             popupText.text = message;
             popupPanel.SetActive(true);
             // Optional: Pause game time while popup is visible
             // if (GameManager.Instance?.TimeManager != null) GameManager.Instance.TimeManager.IsPaused = true;
        }

        /// <summary>
        /// Hides the generic popup panel. Called by the popup's OK button.
        /// </summary>
        public void HidePopup()
        {
             if (popupPanel != null)
             {
                 popupPanel.SetActive(false);
                  Debug.Log("[UIManager] Hiding Popup.");
                 // Optional: Resume game time if it was paused
                 // if (GameManager.Instance?.TimeManager != null) GameManager.Instance.TimeManager.IsPaused = false;
             }
        }

        // TODO: Add methods for:
        // - Specific screen updates (e.g., UpdatePlayerList(List<PlayerData> players))
        // - Handling button clicks that trigger GameManager actions (e.g., OnAdvanceTimeButtonClicked -> GameManager.Instance.AdvanceTime())
        // - Displaying more complex dialogs/windows (transfers, contract negotiations).
        // - Tooltips and hover effects.

        // --- Button Handlers (Examples - Assign these in the Inspector) ---

         public void OnNewGameButtonClicked()
         {
             Debug.Log("New Game button clicked.");
             GameManager.Instance?.StartNewGame();
         }

         public void OnLoadGameButtonClicked()
         {
             Debug.Log("Load Game button clicked.");
             GameManager.Instance?.LoadGame();
         }

          public void OnSaveGameButtonClicked()
         {
             Debug.Log("Save Game button clicked.");
             GameManager.Instance?.SaveGame();
         }

         public void OnContinueButtonClicked() // Example for advancing time
         {
              Debug.Log("Continue button clicked.");
              GameManager.Instance?.AdvanceTime();
         }

          public void OnQuitButtonClicked()
         {
             Debug.Log("Quit button clicked.");
             #if UNITY_EDITOR
             UnityEditor.EditorApplication.isPlaying = false;
             #else
             Application.Quit();
             #endif
         }
    }
}