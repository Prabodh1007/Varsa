using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    public bool isGhar = false;
    public List<PlayerStone> OccupyingPieces = new List<PlayerStone>();

    void OnTriggerEnter(Collider other)
    {
        var piece = other.GetComponent<PlayerStone>();
        if (piece != null && !OccupyingPieces.Contains(piece))
            OccupyingPieces.Add(piece);
    }

    void OnTriggerExit(Collider other)
    {
        var piece = other.GetComponent<PlayerStone>();
        if (piece != null && OccupyingPieces.Contains(piece))
            OccupyingPieces.Remove(piece);
    }
}
