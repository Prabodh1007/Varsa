using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PhotonView))]
public class NetworkRoomManager : MonoBehaviourPunCallbacks
{
    [Header("Room Settings")]
    public int maxPlayersPerRoom = 2;
    public float quickMatchTimeout = 30f;
    [Tooltip("Scene to load once the lobby starts the match")]
    public string gameplaySceneName = "ChallasAathGameplay";
    
    private string currentRoomCode;
    private bool isHost;
    private bool isQuickMatch = false;
    private LobbyUIManager uiManager;
    private UXMLLobbyController uxmlController;
    
    // Connection states
    private bool isConnecting = false;
    private bool isSearchingForMatch = false;
    private bool isMatchmakingReady = false;
    
    void Start()
    {
        StartCoroutine(InitializeAfterFrame());
    }
    
    System.Collections.IEnumerator InitializeAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame(); // Wait for UI to be ready
        
        uiManager = FindObjectOfType<LobbyUIManager>();
        uxmlController = FindObjectOfType<UXMLLobbyController>();
        if (uxmlController != null)
            uxmlController.RegisterNetworkManager(this);
        UpdateMatchmakingAvailability(PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby);
        
        // Connect to Photon
        ConnectToPhoton();
        
        Debug.Log("🌐 NetworkRoomManager initialized!");
    }
    
    void ConnectToPhoton()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("🔌 Connecting to Photon...");
            isConnecting = true;
            UpdateMatchmakingAvailability(false);
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            Debug.Log("✅ Already connected to Photon!");
            UpdateMatchmakingAvailability(PhotonNetwork.InLobby);
        }
    }
    
    public override void OnConnectedToMaster()
    {
        Debug.Log("✅ Connected to Photon Master Server!");
        isConnecting = false;
        UpdateMatchmakingAvailability(false);
        
        // Join lobby to see available rooms
        PhotonNetwork.JoinLobby();
    }
    
    public override void OnJoinedLobby()
    {
        Debug.Log("✅ Joined Photon Lobby!");
        UpdateMatchmakingAvailability(true);
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError($"❌ Disconnected from Photon: {cause}");
        isConnecting = false;
        isSearchingForMatch = false;
        UpdateMatchmakingAvailability(false);
        
        // Try to reconnect after a delay
        StartCoroutine(ReconnectAfterDelay());
    }
    
    IEnumerator ReconnectAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        ConnectToPhoton();
    }
    
    public string CreateRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InLobby)
        {
            Debug.LogError("❌ Not connected to Photon!");
            return null;
        }
        
        currentRoomCode = GenerateRoomCode();
        isHost = true;
        isQuickMatch = false;
        
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = (byte)maxPlayersPerRoom,
            IsVisible = false, // Private room
            IsOpen = true,
            CustomRoomProperties = new Hashtable { { "RoomCode", currentRoomCode } }
        };
        
        Debug.Log($"🏗️ Creating room with code: {currentRoomCode}");
        PhotonNetwork.CreateRoom(currentRoomCode, roomOptions);
        
        return currentRoomCode;
    }
    
    public override void OnCreatedRoom()
    {
        Debug.Log($"✅ Room created successfully: {PhotonNetwork.CurrentRoom.Name}");
    UpdatePlayerCount();
    }
    
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"❌ Failed to create room: {message}");
        
        // Generate a new code and try again
        currentRoomCode = GenerateRoomCode();
        CreateRoom();
    }
    
    public bool JoinRoom(string roomCode)
    {
        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InLobby)
        {
            Debug.LogError("❌ Not connected to Photon!");
            return false;
        }
        
        currentRoomCode = roomCode.ToUpper();
        isHost = false;
        isQuickMatch = false;
        
        Debug.Log($"🚪 Attempting to join room: {currentRoomCode}");
        PhotonNetwork.JoinRoom(currentRoomCode);
        
        return true; // We'll get the actual result in OnJoinedRoom or OnJoinRoomFailed
    }
    
    public override void OnJoinedRoom()
    {
        Debug.Log($"✅ Successfully joined room: {PhotonNetwork.CurrentRoom.Name}");
        
        // Get the room code from custom properties or use room name
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("RoomCode", out object roomCodeObj))
        {
            currentRoomCode = roomCodeObj.ToString();
        }
        else
        {
            currentRoomCode = PhotonNetwork.CurrentRoom.Name;
        }
        
        UpdatePlayerCount();
        
        // Show the waiting panel
        if (uiManager) 
            uiManager.ShowRoomWaiting(currentRoomCode, isQuickMatch);
        if (uxmlController) 
            uxmlController.ShowRoomWaiting(currentRoomCode, isQuickMatch);
    }
    
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"❌ Failed to join room: {message}");
        
        // Show error to user and return to previous screen
        if (uiManager) uiManager.ShowFriendsMenu();
        if (uxmlController) uxmlController.ShowFriendsMenu();
    }
    
    public void RegisterUxmlController(UXMLLobbyController controller)
    {
        uxmlController = controller;
        if (controller != null)
            controller.SetMatchmakingEnabled(isMatchmakingReady);
        if (PhotonNetwork.InRoom && controller != null)
        {
            controller.ShowRoomWaiting(currentRoomCode, isQuickMatch);
            controller.UpdatePlayerCount(PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
        }
    }

    public void StartQuickMatch()
    {
        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InLobby)
        {
            Debug.LogError("❌ Not connected to Photon!");
            return;
        }
        
        Debug.Log("🔍 Starting quick match search...");
        isQuickMatch = true;
        isSearchingForMatch = true;
        
        // Show search UI
        if (uiManager) uiManager.ShowQuickMatchSearch();
        if (uxmlController) uxmlController.ShowQuickMatchSearch();
        
        // Try to join a random room first
        PhotonNetwork.JoinRandomRoom();
    }
    
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("🏗️ No available rooms found, creating new quick match room...");
        
        // No available rooms, create a new one for quick match
        currentRoomCode = GenerateRoomCode();
        isHost = true;
        
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = (byte)maxPlayersPerRoom,
            IsVisible = true, // Visible for quick match
            IsOpen = true,
            CustomRoomProperties = new Hashtable { { "RoomCode", currentRoomCode }, { "QuickMatch", true } }
        };
        
        PhotonNetwork.CreateRoom(currentRoomCode, roomOptions);
    }
    
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"🎉 Player joined: {newPlayer.NickName}");
        UpdatePlayerCount();
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"🚪 Player left: {otherPlayer.NickName}");
        UpdatePlayerCount();
        
        // If we're in game and someone leaves, handle it
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            // Show opponent left message or return to lobby
            if (uiManager) uiManager.ShowLobby();
            if (uxmlController) uxmlController.ShowLobby();
        }
    }
    
    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("⚠️ Only the room master can start the game!");
            return;
        }
        
        if (PhotonNetwork.CurrentRoom.PlayerCount < PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            Debug.LogWarning($"⚠️ Need {PhotonNetwork.CurrentRoom.MaxPlayers} players to start the game!");
            return;
        }
        
        Debug.Log($"🎮 Starting game in room: {currentRoomCode}");
        
        // Close the room so no one else can join
        PhotonNetwork.CurrentRoom.IsOpen = false;
        
        // Notify all players to start the game
        photonView.RPC("StartGameForAllPlayers", RpcTarget.All);
    }
    
    [PunRPC]
    void StartGameForAllPlayers()
    {
        Debug.Log("🎮 Game starting for all players!");
        
        // Here you would typically:
        // 1. Load your 3D Challas Aath game scene
        // 2. Initialize game state
        // 3. Set up player roles (who goes first, etc.)
        
        StartCoroutine(LoadGameScene());
    }
    
    public void LeaveRoom()
    {
        Debug.Log("🚪 Leaving room...");
        
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        
        currentRoomCode = "";
        isHost = false;
        isQuickMatch = false;
        isSearchingForMatch = false;
    }
    
    public override void OnLeftRoom()
    {
        Debug.Log("✅ Successfully left room");
        
        // Return to lobby
        if (uiManager) uiManager.ShowLobby();
        if (uxmlController) uxmlController.ShowLobby();
        UpdateMatchmakingAvailability(PhotonNetwork.InLobby);
    }
    
    void UpdatePlayerCount()
    {
        if (PhotonNetwork.InRoom)
        {
            int currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
            int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
            
            Debug.Log($"👥 Player count: {currentPlayers}/{maxPlayers}");
            
            // Update UI
            if (uiManager) 
                uiManager.UpdatePlayerCount(currentPlayers, maxPlayers);
            if (uxmlController) 
                uxmlController.UpdatePlayerCount(currentPlayers, maxPlayers);
        }
    }

    void UpdateMatchmakingAvailability(bool ready)
    {
        isMatchmakingReady = ready;
        if (uiManager)
            uiManager.SetMatchmakingEnabled(ready);
        if (uxmlController)
            uxmlController.SetMatchmakingEnabled(ready);
    }
    
    string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string result = "";
        
        for (int i = 0; i < 6; i++)
        {
            result += chars[Random.Range(0, chars.Length)];
        }
        
        return result;
    }
    
    IEnumerator LoadGameScene()
    {
        if (string.IsNullOrEmpty(gameplaySceneName))
        {
            Debug.LogError("❌ Gameplay scene name not set on NetworkRoomManager.");
            yield break;
        }

        Debug.Log($"🎮 Loading gameplay scene '{gameplaySceneName}'...");
        SceneManager.LoadScene(gameplaySceneName);
    }
    
    // Public getters for UI to check connection status
    public bool IsConnectedToPhoton => PhotonNetwork.IsConnectedAndReady;
    public bool IsInRoom => PhotonNetwork.InRoom;
    public string CurrentRoomCode => currentRoomCode;
    public bool IsRoomMaster => PhotonNetwork.IsMasterClient;
    public int PlayerCount => PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
    public int MaxPlayers => PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.MaxPlayers : maxPlayersPerRoom;
    public bool IsMatchmakingReady => isMatchmakingReady;
}