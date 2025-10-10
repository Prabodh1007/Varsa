using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private Dictionary<string, int> playerScores = new Dictionary<string, int>();

    void Awake() { Instance = this; }

    public void AddScore(string playerId, int points)
    {
        if (!playerScores.ContainsKey(playerId))
            playerScores[playerId] = 0;

        playerScores[playerId] += points;
        Debug.Log($"Player {playerId} scored! Total: {playerScores[playerId]}");
    }
}
