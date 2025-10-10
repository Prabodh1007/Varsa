// RoomUI.cs
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public class RoomUI : MonoBehaviourPunCallbacks
{
    public Text roomNameText;
    public Text playersText;
    public Button startButton; // only master client can press

    void Start()
    {
        UpdateUI();
    }

    public override void OnJoinedRoom()
    {
        UpdateUI();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateUI();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        if (PhotonNetwork.CurrentRoom != null && roomNameText != null)
            roomNameText.text = "Room: " + PhotonNetwork.CurrentRoom.Name;

        playersText.text = "Players:\n";
        foreach (var p in PhotonNetwork.PlayerList)
            playersText.text += $"{p.NickName}\n";

        if (startButton != null)
            startButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
    }

    // Master client calls this to force-load the game for everyone
    public void OnStartGameButton()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        PhotonNetwork.LoadLevel("GameScene");
    }
}
