using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerStone : MonoBehaviourPun, IPointerClickHandler, IPunObservable
{
    public int playerIndex = 0;
    public int pieceIndex = 0;

    Tile[] path;
    Tile[] moveQueue;
    int moveQueueIndex = 0;
    bool isAnimating = false;

    Vector3 targetPosition;
    Vector3 velocity = Vector3.zero;
    public float smoothTime = 0.22f;
    const float REACH_EPS = 0.03f;

    BoardManager boardManager;
    DiceRoller diceRoller;

    int currentPathIndex = -1;

    // --- COLOR PICKING/APPLYING ---
    // Apply color to this piece's renderers. Public so PlayerColorManager can call it.
    public void ApplyColor(Color color)
    {
        Debug.Log($"PlayerStone[{playerIndex},{pieceIndex}]: ApplyColor called with {color}");
        // Apply to all Renderers on this GameObject and children
        var rends = GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r == null || r.sharedMaterial == null)
            {
                Debug.LogWarning($"PlayerStone.ApplyColor: renderer or sharedMaterial null on '{gameObject.name}'");
                continue;
            }
            try
            {
                // Use MaterialPropertyBlock to avoid instantiating new material instances where possible
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);

                bool applied = false;
                var mat = r.sharedMaterial;
                // Try common color property names
                string[] tryProps = new string[] { "_BaseColor", "_Color", "_TintColor", "_MainColor", "_EmissionColor" };
                foreach (var prop in tryProps)
                {
                    if (mat.HasProperty(prop))
                    {
                        if (prop == "_EmissionColor")
                            mpb.SetColor(prop, color * 0.6f);
                        else
                            mpb.SetColor(prop, color);
                        applied = true;
                    }
                }

                if (applied)
                {
                    r.SetPropertyBlock(mpb);
                    Debug.Log($"PlayerStone[{playerIndex},{pieceIndex}]: SetPropertyBlock on '{r.gameObject.name}'");
                }
                else
                {
                    // Fallback: instantiate material and set color where possible
                    Material newMat = new Material(r.material);
                    if (newMat.HasProperty("_Color")) newMat.color = color;
                    if (newMat.HasProperty("_BaseColor")) newMat.SetColor("_BaseColor", color);
                    if (newMat.HasProperty("_EmissionColor"))
                    {
                        newMat.SetColor("_EmissionColor", color * 0.6f);
                        newMat.EnableKeyword("_EMISSION");
                    }
                    r.material = newMat;
                    Debug.Log($"PlayerStone[{playerIndex},{pieceIndex}]: Replaced material on '{r.gameObject.name}'");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"PlayerStone.ApplyColor: failed to set material color: {ex.Message}");
            }
        }
    }

    void Start()
    {
        boardManager = FindAnyObjectByType<BoardManager>();
        diceRoller = FindAnyObjectByType<DiceRoller>();

        if (photonView.InstantiationData != null && photonView.InstantiationData.Length >= 2)
        {
            playerIndex = (int)photonView.InstantiationData[0];
            pieceIndex = (int)photonView.InstantiationData[1];
        }

        // Debug PhotonView info
        Debug.Log($"PlayerStone.Start: pv.ViewID={photonView.ViewID}, owner={(photonView.Owner!=null?photonView.Owner.NickName:"none")}, isMine={photonView.IsMine}");

        if (boardManager != null)
            path = boardManager.GetPlayerPath(playerIndex);

        targetPosition = transform.position;

        // Position slightly above ghar
        Vector3 pos = transform.position;
        pos.y += 0.2f;
        transform.position = pos;

        // Request/apply player color (if PlayerColorManager exists)
        var pcm = FindObjectOfType<PlayerColorManager>();

        if (pcm != null)
        {
            // Ensure master assigns a color if missing (master will broadcast)
            pcm.EnsureColorAssignedByMaster(playerIndex);
            int assigned = pcm.GetAssignedColorIndex(playerIndex);
            if (assigned >= 0)
            {
                ApplyColor(pcm.GetColorByIndex(assigned));
            }
            else
            {
                // Subscribe to assignment event
                pcm.OnColorAssigned += HandleColorAssigned;
            }
        }

    }


    void HandleColorAssigned(int pIdx, int colorIdx)
    {
        if (pIdx != playerIndex) return;
        var pcm = FindObjectOfType<PlayerColorManager>();
        if (pcm != null)
        {
            ApplyColor(pcm.GetColorByIndex(colorIdx));
            pcm.OnColorAssigned -= HandleColorAssigned;
        }
    }
    

    // ---------------- KILLING / RETURN LOGIC ----------------

    public void ReturnToStart()
    {
        Debug.Log($"PlayerStone: Player {playerIndex} (Piece {pieceIndex}) returning to ghar.");

        PlayerSpawner spawner = FindAnyObjectByType<PlayerSpawner>();
        Vector3 gharPosition = Vector3.zero;
        bool foundGharPosition = false;

        if (spawner != null)
        {
            Transform[] gharArray = null;
            switch (playerIndex)
            {
                case 0: gharArray = spawner.player1Ghar; break;
                case 1: gharArray = spawner.player2Ghar; break;
                case 2: gharArray = spawner.player3Ghar; break;
                case 3: gharArray = spawner.player4Ghar; break;
            }

            if (gharArray != null && pieceIndex >= 0 && pieceIndex < gharArray.Length && gharArray[pieceIndex] != null)
            {
                gharPosition = gharArray[pieceIndex].position;
                foundGharPosition = true;
            }
        }

        if (foundGharPosition)
        {
            transform.position = gharPosition;
            currentPathIndex = -1;

            foreach (var t in FindObjectsOfType<Tile>())
                t.OccupyingPieces.Remove(this);

            Debug.Log($"PlayerStone: Returned to ghar at {gharPosition}");
        }
        else
        {
            Debug.LogWarning($"PlayerStone: Could not find ghar tile for player {playerIndex}, piece {pieceIndex}");
        }
    }

    // --------------------------------------------------------

    void Update()
    {
        if (isAnimating && moveQueue != null && moveQueue.Length > 0 && moveQueueIndex < moveQueue.Length)
        {
            Tile tileTarget = moveQueue[moveQueueIndex];
            if (tileTarget == null)
            {
                isAnimating = false;
                moveQueue = null;
                moveQueueIndex = 0;
                return;
            }

            Vector3 top = GetTopPositionOnTile(tileTarget);
            Vector3 nextPos = Vector3.SmoothDamp(transform.position, top, ref velocity, smoothTime);
            nextPos.y = top.y;
            transform.position = nextPos;

            if (Vector3.Distance(transform.position, top) <= REACH_EPS)
            {
                moveQueueIndex++;
                if (moveQueueIndex >= moveQueue.Length)
                {
                    isAnimating = false;
                    moveQueue = null;
                    moveQueueIndex = 0;

                    Tile finalTile = tileTarget;
                    if (path != null && finalTile != null)
                        currentPathIndex = System.Array.IndexOf(path, finalTile);

                    // Update OccupyingPieces
                    foreach (var t in FindObjectsOfType<Tile>())
                        t.OccupyingPieces.Remove(this);
                    if (finalTile != null && !finalTile.OccupyingPieces.Contains(this))
                        finalTile.OccupyingPieces.Add(this);

                    // --------------- KILL LOGIC ----------------
                    if (finalTile != null && finalTile.OccupyingPieces.Count > 1)
                    {
                        bool moverSafe = finalTile.isGhar && finalTile.OccupyingPieces.Exists(p => p.playerIndex == playerIndex);

                        if (!moverSafe)
                        {
                            var others = new List<PlayerStone>(finalTile.OccupyingPieces);
                            foreach (var other in others)
                            {
                                if (other != null && other != this && other.playerIndex != playerIndex)
                                {
                                    bool victimSafe = finalTile.isGhar && finalTile.OccupyingPieces.Exists(p => p.playerIndex == other.playerIndex);
                                    if (!victimSafe)
                                    {
                                        Debug.Log($"Player {playerIndex} killed Player {other.playerIndex} (Piece {other.pieceIndex}) on {finalTile.name}");
                                        other.ReturnToStart();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    Vector3 GetTopPositionOnTile(Tile t)
    {
        if (t == null) return transform.position;

        Collider col = t.GetComponent<Collider>();
        float tileTopY = (col != null) ? col.bounds.max.y : t.transform.position.y;
        float pieceHalf = 0.5f;
        Collider myCol = GetComponent<Collider>();
        if (myCol != null) pieceHalf = myCol.bounds.extents.y;

        float liftOffset = 0.5f;
        return new Vector3(t.transform.position.x, tileTopY + pieceHalf + liftOffset, t.transform.position.z);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!photonView.IsMine)
        {
            Debug.Log("Not your piece.");
            return;
        }

        if (diceRoller == null)
            diceRoller = FindAnyObjectByType<DiceRoller>();

        int spaces = diceRoller.DiceTotal;
        if (spaces <= 0) return;
        if (path == null || path.Length == 0) return;

        List<int> globalIndices = new List<int>(spaces);
        int simulatedIndex = currentPathIndex;

        for (int i = 0; i < spaces; i++)
        {
            int nextPathIndex = simulatedIndex + 1;
            if (nextPathIndex >= path.Length)
            {
                globalIndices.Add(-1);
                simulatedIndex = nextPathIndex;
            }
            else
            {
                Tile nextTile = path[nextPathIndex];
                int global = (boardManager != null) ? boardManager.GetIndexOfTile(nextTile) : -1;
                globalIndices.Add(global);
                simulatedIndex = nextPathIndex;
            }
        }

        photonView.RPC(nameof(RPC_StartMove), RpcTarget.All, globalIndices.ToArray());
    }

    [PunRPC]
    void RPC_StartMove(int[] globalIndices, PhotonMessageInfo info)
    {
        if (boardManager == null)
            boardManager = FindAnyObjectByType<BoardManager>();

        if (globalIndices == null || globalIndices.Length == 0)
            return;

        List<Tile> list = new List<Tile>(globalIndices.Length);
        foreach (int gi in globalIndices)
        {
            if (gi < 0) list.Add(null);
            else list.Add(boardManager.GetTileByIndex(gi));
        }

        moveQueue = list.ToArray();
        moveQueueIndex = 0;
        isAnimating = moveQueue.Length > 0;

        if (isAnimating && moveQueue[0] != null)
        {
            Vector3 top = GetTopPositionOnTile(moveQueue[0]);
            transform.position = top;
            targetPosition = top;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(currentPathIndex);
        }
        else
        {
            Vector3 p = (Vector3)stream.ReceiveNext();
            int idx = (int)stream.ReceiveNext();
            if (!photonView.IsMine)
            {
                transform.position = p;
                currentPathIndex = idx;
            }
        }
    }
}









