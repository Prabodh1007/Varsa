using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;

public class ScoreManager : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Score Tracking")]
    public int[] playerScores = new int[4]; // 0-3 pieces scored per player
    public int[] finishPositions = new int[4]; // 0 = not finished, 1-4 = finish position
    private int nextFinishPosition = 1; // Track which position to assign next

    [Header("UI References - Drag from Canvas")]
    [Tooltip("Drag the TextMeshPro component from your Canvas here")]
    public TextMeshProUGUI scoreDisplayText;

    private static ScoreManager instance;
    public static ScoreManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<ScoreManager>();
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        UpdateScoreDisplay();
    }

    [PunRPC]
    public void AddScore(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < 4)
        {
            playerScores[playerIndex]++;
            Debug.Log($"ScoreManager: Player {playerIndex} scored! Total: {playerScores[playerIndex]}/4");

            // Check if player finished (all 4 pieces scored)
            if (playerScores[playerIndex] >= 4 && finishPositions[playerIndex] == 0)
            {
                finishPositions[playerIndex] = nextFinishPosition;
                string[] positionNames = { "", "First", "Second", "Third", "Fourth" };
                Debug.Log($"ScoreManager: Player {playerIndex} FINISHED {positionNames[nextFinishPosition]}!");
                nextFinishPosition++;
            }

            UpdateScoreDisplay();
        }
    }

    public void PlayerReachedGoal(int playerIndex)
    {
        // Call RPC to sync score across all clients
        photonView.RPC(nameof(AddScore), RpcTarget.All, playerIndex);
    }

    void UpdateScoreDisplay()
    {
        if (scoreDisplayText == null) return;

        string scoreText = "SCORES:\n";

        for (int i = 0; i < 4; i++)
        {
            string playerName = GetPlayerName(i);
            if (playerName == "NA")
            {
                scoreText += $"Player {i + 1} (NA)\n";
            }
            else
            {
                string score = playerScores[i].ToString();
                if (finishPositions[i] > 0)
                {
                    string[] positionNames = { "", "First", "Second", "Third", "Fourth" };
                    scoreText += $"Player {i + 1} ({playerName}): Finished {positionNames[finishPositions[i]]}\n";
                }
                else
                {
                    scoreText += $"Player {i + 1} ({playerName}): {score}/4\n";
                }
            }
        }

        scoreDisplayText.text = scoreText;
    }

    string GetPlayerName(int playerIndex)
    {
        // Check if a player with this index is in the room
        foreach (var player in PhotonNetwork.PlayerList)
        {
            int actorIndex = player.ActorNumber - 1;
            if (actorIndex == playerIndex)
            {
                return player.NickName ?? $"Player{player.ActorNumber}";
            }
        }
        return "NA";
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send scores and finish positions to other clients
            for (int i = 0; i < 4; i++)
            {
                stream.SendNext(playerScores[i]);
                stream.SendNext(finishPositions[i]);
            }
            stream.SendNext(nextFinishPosition);
        }
        else
        {
            // Receive scores and finish positions from other clients
            for (int i = 0; i < 4; i++)
            {
                playerScores[i] = (int)stream.ReceiveNext();
                finishPositions[i] = (int)stream.ReceiveNext();
            }
            nextFinishPosition = (int)stream.ReceiveNext();
            UpdateScoreDisplay();
        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        UpdateScoreDisplay();
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        UpdateScoreDisplay();
    }
}