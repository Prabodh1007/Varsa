using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class UXMLLobbyController : MonoBehaviour
{
    [Header("UXML Setup")]
    public StyleSheet styleSheet;
    
    private UIDocument uiDocument;
    private VisualElement root;
    private NetworkRoomManager networkManager;
    
    // Panel references
    private VisualElement lobbyPanel;
    private VisualElement friendsMenuPanel;
    private VisualElement joinRoomPanel;
    private VisualElement quickMatchSearchPanel;
    private VisualElement roomWaitingPanel;
    
    // Button references
    private Button playFriendsBtn;
    private Button playRandomBtn;
    private Button createRoomBtn;
    private Button joinRoomBtn;
    private Button goBtn;
    private Button startGameBtn;
    private Button copyBtn;
    
    // Other UI elements
    private TextField roomInput;
    private Label roomCodeText;
    private Label playerCountText;
    private Label statusText;
    private Label roomTitle;
    private Label searchDots;
    
    public enum UIState
    {
        Lobby,
        FriendsMenu,
        JoinRoom,
        QuickMatchSearch,
        RoomWaiting
    }
    
    private UIState currentState = UIState.Lobby;
    
    void Start()
    {
        Debug.Log("🚀 Starting UXML Lobby Controller...");
        InitializeUI();
        LocateNetworkManager();
        SetupEventHandlers();
        SetMatchmakingEnabled(false);
        ShowLobby();
        StartCoroutine(AnimateSearchDots());
        
        Debug.Log("🎮 UXML Heritage Lobby is now running!");
    }
    
    void InitializeUI()
    {
        Debug.Log("🔧 Initializing UI...");
        
        // Get UI Document component
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("❌ UIDocument component not found!");
            return;
        }
        
        root = uiDocument.rootVisualElement;
        Debug.Log($"✅ Root element found: {root != null}");
        
        // Apply stylesheet
        if (styleSheet != null)
        {
            root.styleSheets.Add(styleSheet);
            Debug.Log("✅ StyleSheet applied!");
        }
        else
        {
            Debug.LogWarning("⚠️ StyleSheet not assigned!");
        }
        
        // Get all UI element references
        FindUIElements();
        
        Debug.Log("✅ UXML UI Initialized!");
    }
    
    void FindUIElements()
    {
        Debug.Log("🔍 Finding UI elements...");
        
        // Find panels
        lobbyPanel = root.Q<VisualElement>("LobbyPanel");
        friendsMenuPanel = root.Q<VisualElement>("FriendsMenuPanel");
        joinRoomPanel = root.Q<VisualElement>("JoinRoomPanel");
        quickMatchSearchPanel = root.Q<VisualElement>("QuickMatchSearchPanel");
        roomWaitingPanel = root.Q<VisualElement>("RoomWaitingPanel");
        
        // Find buttons
        playFriendsBtn = root.Q<Button>("PlayFriendsBtn");
        playRandomBtn = root.Q<Button>("PlayRandomBtn");
        createRoomBtn = root.Q<Button>("CreateRoomBtn");
        joinRoomBtn = root.Q<Button>("JoinRoomBtn");
        goBtn = root.Q<Button>("GoBtn");
        startGameBtn = root.Q<Button>("StartGameBtn");
        copyBtn = root.Q<Button>("CopyBtn");
        
        // Find other elements
        roomInput = root.Q<TextField>("RoomInput");
        roomCodeText = root.Q<Label>("RoomCodeText");
        playerCountText = root.Q<Label>("PlayerCountText");
        statusText = root.Q<Label>("StatusText");
        roomTitle = root.Q<Label>("RoomTitle");
        searchDots = root.Q<Label>("SearchDots");
        
        // Debug what we found
        Debug.Log($"📋 Found panels:");
        Debug.Log($"  - Lobby: {lobbyPanel != null}");
        Debug.Log($"  - Friends: {friendsMenuPanel != null}");
        Debug.Log($"  - Join: {joinRoomPanel != null}");
        Debug.Log($"  - Search: {quickMatchSearchPanel != null}");
        Debug.Log($"  - Waiting: {roomWaitingPanel != null}");
        
        Debug.Log($"🔘 Found buttons:");
        Debug.Log($"  - PlayFriends: {playFriendsBtn != null}");
        Debug.Log($"  - PlayRandom: {playRandomBtn != null}");
        Debug.Log($"  - CreateRoom: {createRoomBtn != null}");
        Debug.Log($"  - JoinRoom: {joinRoomBtn != null}");
        
        // If any critical element is missing, log the entire UI tree
        if (lobbyPanel == null || friendsMenuPanel == null || playFriendsBtn == null)
        {
            Debug.LogWarning("⚠️ Some elements missing! UI Tree:");
            LogUITree(root, 0);
        }
    }
    
    void LogUITree(VisualElement element, int depth)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"{indent}- {element.GetType().Name} (name: '{element.name}')");
        
        foreach (var child in element.Children())
        {
            LogUITree(child, depth + 1);
        }
    }
    
    void LocateNetworkManager()
    {
        if (networkManager != null)
            return;
        networkManager = FindObjectOfType<NetworkRoomManager>();
        if (networkManager != null)
        {
            networkManager.RegisterUxmlController(this);
            Debug.Log("✅ NetworkRoomManager linked to UXML controller");
        }
        else
            Debug.LogError("❌ NetworkRoomManager not found in scene! UXML lobby will not function.");
    }

    public void RegisterNetworkManager(NetworkRoomManager manager)
    {
        networkManager = manager;
        if (networkManager != null)
            SetMatchmakingEnabled(networkManager.IsMatchmakingReady);
    }

    void SetupEventHandlers()
    {
        Debug.Log("🔗 Setting up event handlers...");
        
        // Main lobby buttons
        if (playFriendsBtn != null)
        {
            playFriendsBtn.clicked += () => {
                Debug.Log("🎯 PLAY WITH FRIENDS clicked!");
                ShowFriendsMenu();
            };
            Debug.Log("✅ PlayFriends button handler attached");
        }
        else
        {
            Debug.LogError("❌ PlayFriends button not found!");
        }
        
        if (playRandomBtn != null)
        {
            playRandomBtn.clicked += () => {
                Debug.Log("🎯 PLAY WITH RANDOM PEOPLE clicked!");
                StartQuickMatch();
            };
            Debug.Log("✅ PlayRandom button handler attached");
        }
        else
        {
            Debug.LogError("❌ PlayRandom button not found!");
        }
        
        // Friends menu buttons
        if (createRoomBtn != null)
        {
            createRoomBtn.clicked += () => {
                Debug.Log("🎯 CREATE ROOM clicked!");
                CreateRoom();
            };
        }
        
        if (joinRoomBtn != null)
        {
            joinRoomBtn.clicked += () => {
                Debug.Log("🎯 JOIN ROOM clicked!");
                ShowJoinRoom();
            };
        }
        
        // Join room
        if (goBtn != null)
            goBtn.clicked += JoinRoom;
        
        // Room waiting
        if (startGameBtn != null)
            startGameBtn.clicked += StartGame;
        if (copyBtn != null)
            copyBtn.clicked += CopyRoomCode;
        
        // Input validation
        if (roomInput != null)
            roomInput.RegisterValueChangedCallback(ValidateRoomInput);
        
        // Setup back buttons
        var backButtons = root.Query<Button>(className: "back-btn").ToList();
        Debug.Log($"🔙 Found {backButtons.Count} back buttons");
        foreach (var btn in backButtons)
        {
            btn.clicked += () => {
                Debug.Log("🔙 Back button clicked!");
                HandleBackButton();
            };
        }
        
        Debug.Log("✅ Event handlers setup complete!");
    }
    
    // ========== UI STATE MANAGEMENT ==========
    
    public void ShowLobby()
    {
        Debug.Log("🏠 Showing Lobby");
        SetUIState(UIState.Lobby);
        SetPanelVisibility("lobby");
    }
    
    public void ShowFriendsMenu()
    {
        Debug.Log("👥 Showing Friends Menu");
        SetUIState(UIState.FriendsMenu);
        SetPanelVisibility("friends");
    }
    
    void ShowJoinRoom()
    {
        Debug.Log("🔢 Showing Join Room");
        SetUIState(UIState.JoinRoom);
        SetPanelVisibility("join");
        if (roomInput != null)
        {
            roomInput.value = "";
            ValidateRoomInputValue("");
        }
    }
    
    public void ShowQuickMatchSearch()
    {
        Debug.Log("🔍 Showing Quick Match Search");
        SetUIState(UIState.QuickMatchSearch);
        SetPanelVisibility("search");
    }
    
    public void ShowRoomWaiting(string roomCode, bool isQuickMatch = false)
    {
        Debug.Log($"🏠 Showing Room Waiting - Code: {roomCode}");
        SetUIState(UIState.RoomWaiting);
        SetPanelVisibility("waiting");
        
        if (roomCodeText != null)
            roomCodeText.text = roomCode;
        if (roomTitle != null)
            roomTitle.text = isQuickMatch ? "MATCH FOUND" : "ROOM CREATED";

        if (startGameBtn != null)
        {
            bool canStart = networkManager != null && networkManager.IsRoomMaster && networkManager.PlayerCount >= networkManager.MaxPlayers;
            startGameBtn.SetEnabled(canStart);
        }
        if (statusText != null && networkManager != null)
        {
            statusText.text = networkManager.PlayerCount >= networkManager.MaxPlayers
                ? (networkManager.IsRoomMaster ? "Ready to start!" : "Waiting for host...")
                : "Waiting for players...";
        }

        if (networkManager != null)
            UpdatePlayerCount(networkManager.PlayerCount, networkManager.MaxPlayers);
        else
            UpdatePlayerCount(1, 4);
    }

    public void SetMatchmakingEnabled(bool enabled)
    {
        playFriendsBtn?.SetEnabled(enabled);
        playRandomBtn?.SetEnabled(enabled);
        createRoomBtn?.SetEnabled(enabled);
        joinRoomBtn?.SetEnabled(enabled);
        goBtn?.SetEnabled(enabled && roomInput != null && roomInput.value.Length == 6);

        if (!enabled && statusText != null && currentState == UIState.RoomWaiting)
            statusText.text = "Connecting to network...";
    }
    
    void SetPanelVisibility(string activePanel)
    {
        Debug.Log($"🎭 Setting panel visibility: {activePanel}");
        
        // Hide all panels
        if (lobbyPanel != null)
        {
            lobbyPanel.AddToClassList("hidden");
            Debug.Log("  - Lobby hidden");
        }
        if (friendsMenuPanel != null)
        {
            friendsMenuPanel.AddToClassList("hidden");
            Debug.Log("  - Friends hidden");
        }
        if (joinRoomPanel != null)
        {
            joinRoomPanel.AddToClassList("hidden");
            Debug.Log("  - Join hidden");
        }
        if (quickMatchSearchPanel != null)
        {
            quickMatchSearchPanel.AddToClassList("hidden");
            Debug.Log("  - Search hidden");
        }
        if (roomWaitingPanel != null)
        {
            roomWaitingPanel.AddToClassList("hidden");
            Debug.Log("  - Waiting hidden");
        }
        
        // Show active panel
        switch (activePanel)
        {
            case "lobby":
                if (lobbyPanel != null)
                {
                    lobbyPanel.RemoveFromClassList("hidden");
                    Debug.Log("  ✅ Lobby shown");
                }
                else
                {
                    Debug.LogError("  ❌ Lobby panel is null!");
                }
                break;
            case "friends":
                if (friendsMenuPanel != null)
                {
                    friendsMenuPanel.RemoveFromClassList("hidden");
                    Debug.Log("  ✅ Friends menu shown");
                }
                else
                {
                    Debug.LogError("  ❌ Friends panel is null!");
                }
                break;
            case "join":
                if (joinRoomPanel != null)
                {
                    joinRoomPanel.RemoveFromClassList("hidden");
                    Debug.Log("  ✅ Join room shown");
                }
                else
                {
                    Debug.LogError("  ❌ Join panel is null!");
                }
                break;
            case "search":
                if (quickMatchSearchPanel != null)
                {
                    quickMatchSearchPanel.RemoveFromClassList("hidden");
                    Debug.Log("  ✅ Search shown");
                }
                else
                {
                    Debug.LogError("  ❌ Search panel is null!");
                }
                break;
            case "waiting":
                if (roomWaitingPanel != null)
                {
                    roomWaitingPanel.RemoveFromClassList("hidden");
                    Debug.Log("  ✅ Waiting room shown");
                }
                else
                {
                    Debug.LogError("  ❌ Waiting panel is null!");
                }
                break;
        }
    }
    
    void SetUIState(UIState newState)
    {
        currentState = newState;
        Debug.Log($"🎮 UI State changed to: {newState}");
    }
    
    void HandleBackButton()
    {
        Debug.Log($"🔙 Back button pressed from state: {currentState}");
        switch (currentState)
        {
            case UIState.FriendsMenu:
            case UIState.QuickMatchSearch:
            case UIState.RoomWaiting:
                if (networkManager != null && networkManager.IsInRoom)
                    networkManager.LeaveRoom();
                ShowLobby();
                break;
            case UIState.JoinRoom:
                ShowFriendsMenu();
                break;
            default:
                ShowLobby();
                break;
        }
    }
    
    // ========== GAME LOGIC ==========
    
    void StartQuickMatch()
    {
        Debug.Log("🎮 Starting quick match...");
        if (networkManager != null)
        {
            networkManager.StartQuickMatch();
        }
        else
        {
            Debug.LogError("❌ Cannot start quick match - NetworkRoomManager missing!");
        }
    }
    
    void CreateRoom()
    {
        Debug.Log("🏗️ Creating room...");
        if (networkManager != null)
        {
            string roomCode = networkManager.CreateRoom();
            if (string.IsNullOrEmpty(roomCode))
                Debug.LogError("❌ Room creation failed or returned empty code");
        }
        else
        {
            Debug.LogError("❌ Cannot create room - NetworkRoomManager missing!");
        }
    }
    
    void JoinRoom()
    {
        if (roomInput != null)
        {
            string roomCode = roomInput.value.ToUpper();
            if (roomCode.Length == 6)
            {
                Debug.Log($"🚪 Joining room: {roomCode}");
                if (networkManager != null)
                {
                    bool joinStarted = networkManager.JoinRoom(roomCode);
                    if (!joinStarted)
                        Debug.LogError("❌ Failed to initiate join room request");
                }
                else
                {
                    Debug.LogError("❌ Cannot join room - NetworkRoomManager missing!");
                }
            }
        }
    }
    
    void StartGame()
    {
        Debug.Log("🎮 Attempting to start game!");
        if (networkManager != null)
        {
            networkManager.StartGame();
        }
        else
        {
            Debug.LogError("❌ Cannot start game - NetworkRoomManager missing!");
        }
    }
    
    void CopyRoomCode()
    {
        if (roomCodeText != null)
        {
            string roomCode = roomCodeText.text;
            GUIUtility.systemCopyBuffer = roomCode;
            Debug.Log($"📋 Copied room code: {roomCode}");
            
            // Show temporary feedback
            if (copyBtn != null)
            {
                copyBtn.text = "✓";
                StartCoroutine(ResetCopyButton());
            }
        }
    }
    
    IEnumerator ResetCopyButton()
    {
        yield return new WaitForSeconds(1f);
        if (copyBtn != null)
            copyBtn.text = "📋";
    }
    
    void ValidateRoomInput(ChangeEvent<string> evt)
    {
        ValidateRoomInputValue(evt.newValue);
    }
    
    void ValidateRoomInputValue(string inputValue)
    {
        string cleaned = inputValue.ToUpper().Replace(" ", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[^A-Z0-9]", "");
        
        if (cleaned != inputValue && roomInput != null)
        {
            roomInput.value = cleaned;
        }
        
        if (goBtn != null)
        {
            bool canInteract = cleaned.Length == 6 && (networkManager == null || networkManager.IsMatchmakingReady);
            goBtn.SetEnabled(canInteract);
        }
    }
    
    public void UpdatePlayerCount(int current, int max)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);
        if (playerCountText != null)
            playerCountText.text = $"<color=#FFC300>{current}</color>/{max} PLAYERS JOINED";

        for (int i = 1; i <= max; i++)
        {
            var avatar = root.Q<VisualElement>($"Player{i}Avatar");
            if (avatar == null)
                continue;

            var icon = avatar.Q<Label>();
            bool isReady = i <= current;

            if (isReady)
            {
                avatar.RemoveFromClassList("avatar-waiting");
                avatar.AddToClassList("avatar-ready");
                if (icon != null) icon.text = "👤";
            }
            else
            {
                avatar.RemoveFromClassList("avatar-ready");
                avatar.AddToClassList("avatar-waiting");
                if (icon != null) icon.text = "?";
            }
        }

        if (statusText != null)
        {
            if (current >= max)
                statusText.text = networkManager != null && networkManager.IsRoomMaster ? "Ready to start!" : "Waiting for host...";
            else
                statusText.text = "Waiting for players...";
        }

        if (startGameBtn != null)
        {
            bool canStart = networkManager != null && networkManager.IsRoomMaster && current >= max;
            startGameBtn.SetEnabled(canStart);
        }

        Debug.Log($"👥 Player count updated: {current}/{max}");
    }
    
    IEnumerator AnimateSearchDots()
    {
        string[] dotPatterns = { ".", ". .", ". . .", ". .", "." };
        int index = 0;
        
        while (true)
        {
            if (searchDots != null && currentState == UIState.QuickMatchSearch)
            {
                searchDots.text = dotPatterns[index];
                index = (index + 1) % dotPatterns.Length;
            }
            
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    public void ShowLobbyUI(bool show)
    {
        if (root != null)
            root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }
}