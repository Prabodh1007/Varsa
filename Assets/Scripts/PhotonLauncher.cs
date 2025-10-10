// PhotonLauncher.cs
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonLauncher : MonoBehaviourPunCallbacks
{
    public static PhotonLauncher Instance { get; private set; }

    [Header("Optional")]
    public string defaultRoomName = "Room";

    void Awake()
    {
        // singleton so we persist the connection across scenes
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        PhotonNetwork.AutomaticallySyncScene = true;
    }

    void Start()
    {
        Debug.Log("PhotonLauncher: Connecting to Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("PhotonLauncher: Connected to Master. Joining Lobby...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("PhotonLauncher: Joined Lobby.");
        // optionally load lobby scene if you started in LoadingScene
        if (SceneManager.GetActiveScene().name != "LobbyScene")
            SceneManager.LoadScene("LobbyScene");
    }

    // Create or join with a name (UI will call this)
    public void CreateOrJoinRoom(string roomName = null)
    {
        if (string.IsNullOrEmpty(roomName)) roomName = defaultRoomName;
        RoomOptions options = new RoomOptions();
        options.MaxPlayers = 4; // change as needed
        PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
    }

    public void JoinRandomRoom()
    {
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("No random room available - creating one.");
        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 2 });
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined Room: " + PhotonNetwork.CurrentRoom.Name);
        // when everyone joined, load game scene (MasterClient decision or automatic)
        // For quick start: auto-load GameScene for everyone when they join a room
        PhotonNetwork.LoadLevel("GameScene");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning("Photon disconnected: " + cause);
        // handle showing an error / go back to LoadingScene
    }

    // optional: room creation failures
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError("CreateRoom failed: " + message);
    }
}
