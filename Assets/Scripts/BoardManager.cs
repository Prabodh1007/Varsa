using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("All Board Tiles (in order of movement around the board)")]
    public Tile[] allTiles;

    [Header("Paths for Each Player (0 = Red, 1 = Blue, 2 = Green, 3 = Yellow)")]
    public Tile[] player1Path;
    public Tile[] player2Path;
    public Tile[] player3Path;
    public Tile[] player4Path;

    [Header("Starting Tiles (Spawn Points / Ghars) for Each Player")]
    public Tile[] player1StartTiles;
    public Tile[] player2StartTiles;
    public Tile[] player3StartTiles;
    public Tile[] player4StartTiles;

    // Maps each player index (0–3) to their path
    private Dictionary<int, Tile[]> playerPaths = new Dictionary<int, Tile[]>();

    void Awake()
    {
        // Fill the dictionary so GetPlayerPath works cleanly
        playerPaths[0] = player1Path;
        playerPaths[1] = player2Path;
        playerPaths[2] = player3Path;
        playerPaths[3] = player4Path;
    }

    /// <summary>
    /// Returns the movement path for a specific player.
    /// </summary>
    public Tile[] GetPlayerPath(int playerIndex)
    {
        if (playerPaths.ContainsKey(playerIndex))
            return playerPaths[playerIndex];
        else
        {
            Debug.LogWarning($"No path found for player index {playerIndex}");
            return null;
        }
    }

    /// <summary>
    /// Returns the global index of a tile (used for RPC sync).
    /// </summary>
    public int GetIndexOfTile(Tile tile)
    {
        if (tile == null || allTiles == null)
            return -1;

        for (int i = 0; i < allTiles.Length; i++)
        {
            if (allTiles[i] == tile)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Returns the Tile object corresponding to a global index.
    /// </summary>
    public Tile GetTileByIndex(int index)
    {
        if (index < 0 || index >= allTiles.Length)
            return null;
        return allTiles[index];
    }

    /// <summary>
    /// Returns the starting ghar/spawn tile for a given player's piece.
    /// </summary>
    public Tile GetGharTile(int playerIndex, int pieceIndex)
    {
        Tile[] startArray = null;

        switch (playerIndex)
        {
            case 0: startArray = player1StartTiles; break;
            case 1: startArray = player2StartTiles; break;
            case 2: startArray = player3StartTiles; break;
            case 3: startArray = player4StartTiles; break;
        }

        if (startArray != null && pieceIndex >= 0 && pieceIndex < startArray.Length)
            return startArray[pieceIndex];

        Debug.LogWarning($"No ghar tile found for player {playerIndex}, piece {pieceIndex}");
        return null;
    }
}
