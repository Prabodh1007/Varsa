using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUIManager : MonoBehaviour
{
    public enum UIState
    {
        Lobby,
        FriendsMenu,
        JoinRoom,
        QuickMatchSearch,
        RoomWaiting,
        Hidden
    }
    
    private UIState currentState;
    private bool isJoiningRoom = false;
    private NetworkRoomManager roomManager;
    
    // UI References (auto-found)
    private GameObject lobbyPanel;
    private GameObject friendsMenuPanel;
    private GameObject joinRoomPanel;
    private GameObject quickMatchSearchPanel;
    private GameObject roomWaitingPanel;
    
    private Button playFriendsButton;
    private Button playRandomButton;
    private Button createRoomButton;
    private Button joinRoomButton;
    private Button goButton;
    private Button startGameButton;
    private Button copyButton;
    
    private TMP_Text roomCodeText;
    private TMP_Text playerCountText;
    private TMP_Text statusText;
    private TMP_Text roomTitleText;
    private TMP_InputField roomIdInput;
    private TMP_Text connectionStatusText;
    
    void Start()
    {
        StartCoroutine(InitializeAfterFrame());
    }
    
    System.Collections.IEnumerator InitializeAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame(); // Wait for UI to be built
        
        FindUIReferences();
        SetupButtonListeners();
        ShowLobby();
        
        Debug.Log("✅ LobbyUIManager initialized!");
    }
    
    void FindUIReferences()
    {
        // Find panels
        lobbyPanel = GameObject.Find("LobbyPanel");
        friendsMenuPanel = GameObject.Find("FriendsMenuPanel");
        joinRoomPanel = GameObject.Find("JoinRoomPanel");
        quickMatchSearchPanel = GameObject.Find("QuickMatchSearchPanel");
        roomWaitingPanel = GameObject.Find("RoomWaitingPanel");
        
        // Find buttons
        playFriendsButton = GameObject.Find("PlayFriendsBtn")?.GetComponent<Button>();
        playRandomButton = GameObject.Find("PlayRandomBtn")?.GetComponent<Button>();
        createRoomButton = GameObject.Find("CreateRoomBtn")?.GetComponent<Button>();
        joinRoomButton = GameObject.Find("JoinRoomBtn")?.GetComponent<Button>();
        goButton = GameObject.Find("GoBtn")?.GetComponent<Button>();
        startGameButton = GameObject.Find("StartGameBtn")?.GetComponent<Button>();
        copyButton = GameObject.Find("CopyBtn")?.GetComponent<Button>();
        
        // Find text components
        roomCodeText = GameObject.Find("RoomCodeText")?.GetComponent<TMP_Text>();
        playerCountText = GameObject.Find("PlayerCountText")?.GetComponent<TMP_Text>();
        statusText = GameObject.Find("StatusText")?.GetComponent<TMP_Text>();
        roomTitleText = GameObject.Find("RoomTitle")?.GetComponent<TMP_Text>();
        roomIdInput = GameObject.Find("RoomInput")?.GetComponent<TMP_InputField>();
        connectionStatusText = GameObject.Find("ConnectionStatus")?.GetComponent<TMP_Text>();
        
        // Find room manager
        roomManager = FindObjectOfType<NetworkRoomManager>();
        
        Debug.Log("UI References found and connected!");
    }
    
    void SetupButtonListeners()
    {
        // Lobby buttons
        if (playFriendsButton) playFriendsButton.onClick.AddListener(() => ShowFriendsMenu());
        if (playRandomButton) playRandomButton.onClick.AddListener(() => StartQuickMatch());
        
        // Friends menu buttons
        if (createRoomButton) createRoomButton.onClick.AddListener(() => CreateRoom());
        if (joinRoomButton) joinRoomButton.onClick.AddListener(() => ToggleJoinRoom());
        
        // Join room button
        if (goButton) goButton.onClick.AddListener(() => JoinRoom());
        
        // Room waiting buttons
        if (startGameButton) startGameButton.onClick.AddListener(() => StartGame());
        if (copyButton) copyButton.onClick.AddListener(() => CopyRoomCode());
        
        // Setup back buttons
        SetupBackButtons();
        
        // Input field validation
        if (roomIdInput) roomIdInput.onValueChanged.AddListener(ValidateRoomInput);
        
        Debug.Log("Button listeners setup complete!");
    }
    
    void SetupBackButtons()
    {
        Button[] allButtons = FindObjectsOfType<Button>();
        foreach (Button btn in allButtons)
        {
            if (btn.name.Contains("BackBtn") || btn.name.Contains("Back"))
            {
                btn.onClick.AddListener(() => HandleBackButton());
            }
        }
    }
    
    void HandleBackButton()
    {
        switch (currentState)
        {
            case UIState.FriendsMenu:
            case UIState.QuickMatchSearch:
                ShowLobby();
                break;
            case UIState.RoomWaiting:
                // Leave the network room when going back
                if (roomManager && roomManager.IsInRoom)
                    roomManager.LeaveRoom();
                else
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
    
    public void ShowLobby()
    {
        SetUIState(UIState.Lobby);
        SetPanelActive(lobbyPanel, true);
        SetPanelActive(friendsMenuPanel, false);
        SetPanelActive(joinRoomPanel, false);
        SetPanelActive(roomWaitingPanel, false);
        SetPanelActive(quickMatchSearchPanel, false);
        
        isJoiningRoom = false;
        Debug.Log("🏠 Showing Lobby");
    }
    
    public void ShowFriendsMenu()
    {
        SetUIState(UIState.FriendsMenu);
        SetPanelActive(lobbyPanel, false);
        SetPanelActive(friendsMenuPanel, true);
        SetPanelActive(joinRoomPanel, false);
        SetPanelActive(roomWaitingPanel, false);
        SetPanelActive(quickMatchSearchPanel, false);
        
        Debug.Log("👥 Showing Friends Menu");
    }
    
    public void ShowJoinRoom()
    {
        SetUIState(UIState.JoinRoom);
        SetPanelActive(joinRoomPanel, true);
        if (roomIdInput) 
        {
            roomIdInput.text = "";
            ValidateRoomInput("");
        }
        
        Debug.Log("🔢 Showing Join Room");
    }
    
    public void ShowQuickMatchSearch()
    {
        SetUIState(UIState.QuickMatchSearch);
        SetPanelActive(lobbyPanel, false);
        SetPanelActive(friendsMenuPanel, false);
        SetPanelActive(joinRoomPanel, false);
        SetPanelActive(roomWaitingPanel, false);
        SetPanelActive(quickMatchSearchPanel, true);
        
        Debug.Log("🔍 Showing Quick Match Search");
    }
    
    public void ShowRoomWaiting(string roomCode, bool isQuickMatch = false)
    {
        SetUIState(UIState.RoomWaiting);
        SetPanelActive(lobbyPanel, false);
        SetPanelActive(friendsMenuPanel, false);
        SetPanelActive(joinRoomPanel, false);
        SetPanelActive(quickMatchSearchPanel, false);
        SetPanelActive(roomWaitingPanel, true);
        
        if (roomCodeText) roomCodeText.text = roomCode;
        if (roomTitleText) roomTitleText.text = isQuickMatch ? "MATCH FOUND" : "ROOM CREATED";
        
    int currentPlayers = roomManager ? roomManager.PlayerCount : 1;
    int maxPlayers = roomManager ? roomManager.MaxPlayers : 4;
    UpdatePlayerCount(currentPlayers, maxPlayers);
    if (startGameButton) startGameButton.interactable = currentPlayers >= maxPlayers;
        
        Debug.Log($"🏠 Showing Room Waiting - Code: {roomCode}");
    }
    
    void SetPanelActive(GameObject panel, bool active)
    {
        if (panel) panel.SetActive(active);
    }
    
    void SetUIState(UIState newState)
    {
        currentState = newState;
    }
    
    void ToggleJoinRoom()
    {
        isJoiningRoom = !isJoiningRoom;
        if (isJoiningRoom)
        {
            ShowJoinRoom();
        }
        else
        {
            SetPanelActive(joinRoomPanel, false);
        }
    }
    
    void StartQuickMatch()
    {
        Debug.Log("🎮 Starting quick match...");
        ShowQuickMatchSearch();
        
        if (roomManager && roomManager.IsConnectedToPhoton) 
            roomManager.StartQuickMatch();
        else
            Debug.LogError("❌ Not connected to network or RoomManager not found!");
    }
    
    void CreateRoom()
    {
        Debug.Log("🏗️ Creating room...");
        if (roomManager && roomManager.IsConnectedToPhoton)
        {
            string roomCode = roomManager.CreateRoom();
            if (!string.IsNullOrEmpty(roomCode))
                ShowRoomWaiting(roomCode, false);
        }
        else
        {
            Debug.LogError("❌ Not connected to network or RoomManager not found!");
        }
    }
    
    void JoinRoom()
    {
        if (roomIdInput)
        {
            string roomCode = roomIdInput.text.ToUpper();
            if (roomCode.Length == 6)
            {
                Debug.Log($"🚪 Attempting to join room: {roomCode}");
                if (roomManager && roomManager.IsConnectedToPhoton)
                {
                    bool success = roomManager.JoinRoom(roomCode);
                    // Note: Success/failure will be handled by NetworkRoomManager callbacks
                    // ShowRoomWaiting will be called from OnJoinedRoom callback
                }
                else
                {
                    Debug.LogError("❌ Not connected to network or RoomManager not found!");
                }
            }
        }
    }
    
    void ValidateRoomInput(string input)
    {
        if (roomIdInput)
        {
            string cleaned = input.ToUpper().Replace(" ", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[^A-Z0-9]", "");
            
            if (cleaned != input)
            {
                roomIdInput.text = cleaned;
            }
            
            if (goButton)
            {
                bool canInteract = cleaned.Length == 6 && roomManager && roomManager.IsMatchmakingReady;
                goButton.interactable = canInteract;
            }
        }
    }
    
    void StartGame()
    {
        Debug.Log("🎮 Starting game!");
        if (roomManager && roomManager.IsRoomMaster) 
            roomManager.StartGame();
        else if (!roomManager.IsRoomMaster)
            Debug.LogWarning("⚠️ Only the room master can start the game!");
    }
    
    void CopyRoomCode()
    {
        if (roomCodeText)
        {
            string roomCode = roomCodeText.text;
            GUIUtility.systemCopyBuffer = roomCode;
            Debug.Log($"📋 Room code copied: {roomCode}");
        }
    }
    
    public void UpdatePlayerCount(int current, int max)
    {
    if (playerCountText) playerCountText.text = $"{current}/{max} PLAYERS JOINED";
        
        // Update player avatars
        UpdatePlayerAvatars(current);
        
        // Update status and start button
        if (statusText)
            statusText.text = current >= max ? "Ready to start!" : "Waiting for players...";
        if (startGameButton)
            startGameButton.interactable = current >= max;
        
        Debug.Log($"👥 Player count updated: {current}/{max}");
    }
    
    void Update()
    {
        // Update connection status
        if (connectionStatusText && roomManager)
        {
            if (roomManager.IsConnectedToPhoton)
            {
                connectionStatusText.text = "🟢 Connected";
                connectionStatusText.color = Color.green;
            }
            else
            {
                connectionStatusText.text = "🔴 Connecting...";
                connectionStatusText.color = Color.red;
            }
        }
    }
    
    void UpdatePlayerAvatars(int joinedPlayers)
    {
        const int maxAvatars = 4;
        for (int i = 1; i <= maxAvatars; i++)
        {
            GameObject avatarObj = GameObject.Find($"Player{i}Avatar");
            if (!avatarObj)
                continue;

            Image avatarImg = avatarObj.GetComponent<Image>();
            if (!avatarImg)
                continue;

            avatarImg.color = joinedPlayers >= i ? Color.green : Color.gray;
        }
    }

        public void SetMatchmakingEnabled(bool enabled)
        {
            if (playFriendsButton) playFriendsButton.interactable = enabled;
            if (playRandomButton) playRandomButton.interactable = enabled;
            if (createRoomButton) createRoomButton.interactable = enabled;
            if (joinRoomButton) joinRoomButton.interactable = enabled;
            if (goButton)
            {
                bool validCode = roomIdInput && roomIdInput.text.Length == 6;
                goButton.interactable = enabled && validCode;
            }
        }
}