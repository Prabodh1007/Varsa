using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Photon.Pun;

/// <summary>
/// PlayerStone: movement + Photon replication + kill handling + non-overlap offsets.
/// Built to be robust against missing child colliders and missing board objects.
/// Drop-in replacement for your existing PlayerStone.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class PlayerStone : MonoBehaviourPun, IPointerClickHandler, IPunObservable
{
    public int playerIndex = 0;
    public int pieceIndex = 0;

    // runtime resolved
    Tile[] path;
    Tile[] moveQueue;
    int moveQueueIndex = 0;
    bool isAnimating = false;

    // movement smoothing
    Vector3 velocity = Vector3.zero;
    public float smoothTime = 0.22f;
    const float REACH_EPS = 0.03f;

    BoardManager boardManager;
    DiceRoller diceRoller;

    // logical index on player's path (-1 = still in ghar)
    int currentPathIndex = -1;

    void Start()
    {
        // try to find board manager and dice roller; tolerate nulls
        boardManager = UnityEngine.Object.FindAnyObjectByType<BoardManager>();
        diceRoller = UnityEngine.Object.FindAnyObjectByType<DiceRoller>();

        // receive instantiation data
        if (photonView.InstantiationData != null && photonView.InstantiationData.Length >= 2)
        {
            playerIndex = (int)photonView.InstantiationData[0];
            pieceIndex = (int)photonView.InstantiationData[1];
        }

        if (boardManager != null)
        {
            path = boardManager.GetPlayerPath(playerIndex);
        }
        else
        {
            Debug.LogWarning("PlayerStone: BoardManager not found in scene (Start). Some features may have reduced functionality.");
        }

        // small safety lift at spawn so initially we don't sink into floor
        Vector3 pos = transform.position;
        pos.y += 0.12f;
        transform.position = pos;
    }

    void Update()
    {
        // If animating, step toward the current target tile top pos
        if (isAnimating && moveQueue != null && moveQueue.Length > 0 && moveQueueIndex < moveQueue.Length)
        {
            Tile tileTarget = moveQueue[moveQueueIndex];
            if (tileTarget == null)
            {
                // scoring / off-board case: stop animation
                isAnimating = false;
                moveQueue = null;
                moveQueueIndex = 0;
                return;
            }

            // compute top position with occupancy offset
            Vector3 top = GetTopPositionOnTile(tileTarget);

            // Smooth horizontally/vertically but force Y to tile top to avoid sinking
            Vector3 nextPos = Vector3.SmoothDamp(transform.position, top, ref velocity, smoothTime);
            nextPos.y = top.y;
            transform.position = nextPos;

            // arrival check
            if (Vector3.Distance(transform.position, top) <= REACH_EPS)
            {
                // advance
                moveQueueIndex++;

                // when we finish the full queue, finalize and update occupancy & path index
                if (moveQueueIndex >= moveQueue.Length)
                {
                    isAnimating = false;

                    // final tile
                    Tile finalTile = tileTarget;

                    // update currentPathIndex to index in player path (if available)
                    if (path != null && finalTile != null)
                    {
                        currentPathIndex = System.Array.IndexOf(path, finalTile);
                    }

                    // Update OccupyingPieces lists (remove this from all tiles and add to final)
                    foreach (var t in UnityEngine.Object.FindObjectsOfType<Tile>())
                    {
                        if (t.OccupyingPieces.Contains(this))
                            t.OccupyingPieces.Remove(this);
                    }
                    if (finalTile != null && !finalTile.OccupyingPieces.Contains(this))
                    {
                        finalTile.OccupyingPieces.Add(this);
                    }

                    // KILL LOGIC: only trigger kill when this piece lands (owner initiated RPC already makes everyone animate)
                    if (photonView.IsMine && finalTile != null && finalTile.OccupyingPieces != null)
                    {
                        bool moverSafe = finalTile.isGhar && finalTile.OccupyingPieces.Exists(p => p.playerIndex == this.playerIndex);
                        if (!moverSafe && finalTile.OccupyingPieces.Count > 1)
                        {
                            // copy to avoid modifying collection while iterating
                            var others = new List<PlayerStone>(finalTile.OccupyingPieces);
                            foreach (var other in others)
                            {
                                if (other != null && other != this && other.playerIndex != this.playerIndex)
                                {
                                    bool victimSafe = finalTile.isGhar && finalTile.OccupyingPieces.Exists(p => p.playerIndex == other.playerIndex);
                                    if (!victimSafe)
                                    {
                                        // send victim home on all clients
                                        other.photonView.RPC(nameof(RPC_SendHome), RpcTarget.All);
                                    }
                                }
                            }
                        }
                    }

                    // clear moveQueue after finishing
                    moveQueue = null;
                    moveQueueIndex = 0;
                }
            }
        }
    }

    // compute position on top of tile; includes circular offset when multiple pieces occupy the tile
    Vector3 GetTopPositionOnTile(Tile t)
    {
        if (t == null) return transform.position;

        // tile top Y: tile collider max Y if present, else tile transform.y
        Collider tileCol = t.GetComponent<Collider>();
        float tileTopY = (tileCol != null) ? tileCol.bounds.max.y : t.transform.position.y;

        // piece half-height: check children colliders as well
        float pieceHalf = 0.5f;
        Collider myCol = GetComponent<Collider>();
        if (myCol == null)
            myCol = GetComponentInChildren<Collider>(); // handle prefab where collider is on child
        if (myCol != null)
            pieceHalf = myCol.bounds.extents.y;

        // base position (center of tile, lifted)
        float liftOffset = 0.5f; // tune this for your models (0.12..0.5)
        Vector3 basePos = new Vector3(t.transform.position.x, tileTopY + pieceHalf + liftOffset, t.transform.position.z);

        // occupancy-based offset: prefer using Tile.OccupyingPieces if present
        List<PlayerStone> occupants = null;
        try
        {
            occupants = t.OccupyingPieces;
        }
        catch
        {
            occupants = null;
        }

        // If the tile's OccupyingPieces is null or empty, fallback to scanning scene for stones at nearly same tile (defensive)
        if (occupants == null)
            occupants = new List<PlayerStone>();

        // compute index and count
        int index = occupants.IndexOf(this);
        int count = Mathf.Max(occupants.Count, 1);

        // If this stone is not yet in the occupying list (e.g. just starting animation),
        // include it logically at the end so the moving piece gets an offset too.
        if (index == -1 && occupants.Count >= 1)
        {
            index = occupants.Count; // place it after existing occupants for offset calc
            count = occupants.Count + 1;
        }

        // If multiple pieces, arrange them in a small circle around tile center
        if (count > 1)
        {
            float radius = 0.4f; // adjust according to tile size and models
            float angle = (360f / count) * index * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            basePos += offset;
        }

        return basePos;
    }

    // RPC to send a piece home (called on all clients)
    [PunRPC]
    void RPC_SendHome()
    {
        // remove from any tiles
        foreach (var t in UnityEngine.Object.FindObjectsOfType<Tile>())
            t.OccupyingPieces.Remove(this);

        // Try PlayerSpawner first to find ghar positions (preferred)
        PlayerSpawner spawner = UnityEngine.Object.FindAnyObjectByType<PlayerSpawner>();
        Vector3 gharPosition = Vector3.zero;
        bool set = false;

        if (spawner != null)
        {
            Transform[] arr = null;
            switch (playerIndex)
            {
                case 0: arr = spawner.player1Ghar; break;
                case 1: arr = spawner.player2Ghar; break;
                case 2: arr = spawner.player3Ghar; break;
                case 3: arr = spawner.player4Ghar; break;
            }

            if (arr != null && pieceIndex >= 0 && pieceIndex < arr.Length && arr[pieceIndex] != null)
            {
                gharPosition = arr[pieceIndex].position;
                gharPosition.y += 0.12f; // small lift
                transform.position = gharPosition;
                set = true;
            }
        }

        // fallback: BoardManager.GetGharTile if available
        if (!set && boardManager != null)
        {
            try
            {
                Tile gharTile = boardManager.GetGharTile(playerIndex, pieceIndex);
                if (gharTile != null)
                {
                    Vector3 top = GetTopPositionOnTile(gharTile);
                    transform.position = top;
                    set = true;

                    // register in tile occupants
                    if (!gharTile.OccupyingPieces.Contains(this))
                        gharTile.OccupyingPieces.Add(this);
                }
            }
            catch
            {
                // method may not exist in your BoardManager - ignore
            }
        }

        if (!set)
        {
            // last fallback: just nudge upward to avoid sinking
            Vector3 p = transform.position;
            p.y += 0.2f;
            transform.position = p;
        }

        // reset move state
        isAnimating = false;
        moveQueue = null;
        moveQueueIndex = 0;
        currentPathIndex = -1;
    }

    // handle click by owner
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!photonView.IsMine)
        {
            // Not this client's piece
            Debug.Log("PlayerStone: Not your piece.");
            return;
        }

        if (diceRoller == null)
            diceRoller = UnityEngine.Object.FindAnyObjectByType<DiceRoller>();

        if (diceRoller == null)
        {
            Debug.LogWarning("PlayerStone: DiceRoller not found.");
            return;
        }

        int spaces = diceRoller.DiceTotal;
        if (spaces <= 0) return;

        if (path == null || path.Length == 0)
        {
            Debug.LogWarning($"PlayerStone: No path for player {playerIndex}");
            return;
        }

        // block moves that would overshoot goal (if that's your rule)
        int finalPathIndex = currentPathIndex + spaces;
        if (finalPathIndex > (path.Length - 1))
        {
            Debug.Log($"PlayerStone: Move blocked (would overshoot). current {currentPathIndex} + {spaces} > max {path.Length - 1}");
            return;
        }

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

        // broadcast to all clients to start the same animation
        photonView.RPC(nameof(RPC_StartMove), RpcTarget.All, globalIndices.ToArray());
    }

    // RPC reconstructs moveQueue from global tile indices and starts animation
    [PunRPC]
    void RPC_StartMove(int[] globalIndices, PhotonMessageInfo info)
    {
        if (boardManager == null)
            boardManager = UnityEngine.Object.FindAnyObjectByType<BoardManager>();

        if (globalIndices == null || globalIndices.Length == 0) return;

        List<Tile> list = new List<Tile>(globalIndices.Length);
        foreach (int gi in globalIndices)
        {
            if (gi < 0) list.Add(null);
            else
            {
                Tile t = (boardManager != null) ? boardManager.GetTileByIndex(gi) : null;
                list.Add(t);
            }
        }

        moveQueue = list.ToArray();
        moveQueueIndex = 0;
        isAnimating = (moveQueue != null && moveQueue.Length > 0);

        // Immediately snap Y to the top of first tile (prevents initial dip)
        if (isAnimating && moveQueue[0] != null)
        {
            Vector3 top = GetTopPositionOnTile(moveQueue[0]);
            Vector3 p = transform.position;
            p.y = top.y;
            transform.position = p;
        }
    }

    // Photon sync (position + logical index)
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










//{
//    // Use this for initialization
//    void Start()
//    {
//        theStateManager = GameObject.FindObjectOfType<StateManager>();
//        targetPosition = this.transform.position;
//    }

//    public Tile StartingTile;
//    public Tile CurrentTile { get; protected set; }

//    public int PlayerId;
//    //public StoneStorage MyStoneStorage;

//    bool scoreMe = false;

//    StateManager theStateManager;

//    Tile[] moveQueue;
//    int moveQueueIndex;

//    bool isAnimating = false;

//    Vector3 targetPosition;
//    Vector3 velocity;
//    float smoothTime = 0.25f;
//    float smoothTimeVertical = 0.1f;
//    float smoothDistance = 0.01f;
//    float smoothHeight = 0.5f;

//    PlayerStone stoneToBop;


//    // Update is called once per frame
//    void Update()
//    {
//        if (isAnimating == false)
//        {
//            // Nothing for us to do.
//            return;
//        }

//        if (Vector3.Distance(
//               new Vector3(this.transform.position.x, targetPosition.y, this.transform.position.z),
//               targetPosition) < smoothDistance)
//        {
//            // We've reached the target position -- do we still have moves in the queue?

//            if (
//                (moveQueue == null || moveQueueIndex == (moveQueue.Length))
//                &&
//                ((this.transform.position.y - smoothDistance) > targetPosition.y)
//            )
//            {
//                // We are totally out of moves (and too high up), the only thing left to do is drop down.
//                this.transform.position = Vector3.SmoothDamp(
//                    this.transform.position,
//                    new Vector3(this.transform.position.x, targetPosition.y, this.transform.position.z),
//                    ref velocity,
//                    smoothTimeVertical);

//                // Check for bops
//                if (stoneToBop != null)
//                {
//                    stoneToBop.ReturnToStorage();
//                    stoneToBop = null;
//                }
//            }
//            else
//            {
//                // Right position, right height -- let's advance the queue
//                AdvanceMoveQueue();
//            }
//        }
//        else if (this.transform.position.y < (smoothHeight - smoothDistance))
//        {
//            // We want to rise up before we move sideways.
//            this.transform.position = Vector3.SmoothDamp(
//                this.transform.position,
//                new Vector3(this.transform.position.x, smoothHeight, this.transform.position.z),
//                ref velocity,
//                smoothTimeVertical);
//        }
//        else
//        {
//            // Normal movement (sideways)
//            this.transform.position = Vector3.SmoothDamp(
//                this.transform.position,
//                new Vector3(targetPosition.x, smoothHeight, targetPosition.z),
//                ref velocity,
//                smoothTime);
//        }

//    }

//    void AdvanceMoveQueue()
//    {
//        if (moveQueue != null && moveQueueIndex < moveQueue.Length)
//        {
//            Tile nextTile = moveQueue[moveQueueIndex];
//            if (nextTile == null)
//            {
//                // We are probably being scored
//                // TODO: Move us to the scored pile
//                Debug.Log("SCORING TILE!");
//                SetNewTargetPosition(this.transform.position + Vector3.right * 10f);
//            }
//            else
//            {
//                SetNewTargetPosition(nextTile.transform.position);
//                moveQueueIndex++;
//            }
//        }
//        else
//        {
//            // The movement queue is empty, so we are done animating!
//            //Debug.Log("Done animating!");
//            this.isAnimating = false;
//            theStateManager.AnimationsPlaying--;

//            // Are we on a roll again space?
//            if (CurrentTile != null && CurrentTile.IsRollAgain)
//            {
//                theStateManager.RollAgain();
//            }
//        }

//    }

//    void SetNewTargetPosition(Vector3 pos)
//    {
//        targetPosition = pos;
//        velocity = Vector3.zero;
//        isAnimating = true;
//    }

//    void OnMouseUp()
//    {
//        // TODO:  Is the mouse over a UI element? In which case, ignore this click.
//        MoveMe();
//    }

//    public void MoveMe()
//    {
//        // Is this the correct player?
//        if (theStateManager.CurrentPlayerId != PlayerId)
//        {
//            return;
//        }

//        // Have we rolled the dice?
//        if (theStateManager.IsDoneRolling == false)
//        {
//            // We can't move yet.
//            return;
//        }
//        if (theStateManager.IsDoneClicking == true)
//        {
//            // We've already done a move!
//            return;
//        }


//        // Where should we end up?
//        moveQueue = GetTilesAhead(spacesToMove);
//        Tile finalTile = moveQueue[moveQueue.Length - 1];

//        // TODO: Check to see if the destination is legal!

//        if (finalTile == null)
//        {
//            // Hey, we're scoring this stone!
//            scoreMe = true;
//        }
//        else
//        {
//            if (CanLegallyMoveTo(finalTile) == false)
//            {
//                // Not allowed!
//                finalTile = CurrentTile;
//                moveQueue = null;
//                return;
//            }

//            // If there is an enemy tile in our legal space, the we kick it out.
//            if (finalTile.PlayerStone != null)
//            {
//                //finalTile.PlayerStone.ReturnToStorage();
//                stoneToBop = finalTile.PlayerStone;
//                stoneToBop.CurrentTile.PlayerStone = null;
//                stoneToBop.CurrentTile = null;
//            }
//        }

//        this.transform.SetParent(null); // Become Batman

//        // Remove ourselves from our old tile
//        if (CurrentTile != null)
//        {
//            CurrentTile.PlayerStone = null;
//        }

//        // Even before the animation is done, set our current tile to the new tile
//        CurrentTile = finalTile;
//        if (finalTile.IsScoringSpace == false)   // "Scoring" tiles are always "empty"
//        {
//            finalTile.PlayerStone = this;
//        }

//        moveQueueIndex = 0;

//        theStateManager.IsDoneClicking = true;
//        this.isAnimating = true;
//        theStateManager.AnimationsPlaying++;
//    }

//    // Return the list of tiles __ moves ahead of us
//    public Tile[] GetTilesAhead(int spacesToMove)
//    {
//        if (spacesToMove == 0)
//        {
//            return null;
//        }

//        // Where should we end up?

//        Tile[] listOfTiles = new Tile[spacesToMove];
//        Tile finalTile = CurrentTile;

//        for (int i = 0; i < spacesToMove; i++)
//        {
//            if (finalTile == null)
//            {
//                finalTile = StartingTile;
//            }
//            else
//            {
//                if (finalTile.NextTiles == null || finalTile.NextTiles.Length == 0)
//                {
//                    // We are overshooting the victory -- so just return some nulls in the array
//                    // Just break and we'll return the array, which is going to have nulls
//                    // at the end.
//                    break;
//                }
//                else if (finalTile.NextTiles.Length > 1)
//                {
//                    // Branch based on player id
//                    finalTile = finalTile.NextTiles[PlayerId];
//                }
//                else
//                {
//                    finalTile = finalTile.NextTiles[0];
//                }
//            }

//            listOfTiles[i] = finalTile;
//        }

//        return listOfTiles;
//    }

//    public Tile GetTileAhead()
//    {
//        return GetTileAhead(theStateManager.DiceTotal);
//    }


//    // Return the final tile we'd land on if we moved __ spaces
//    public Tile GetTileAhead(int spacesToMove)
//    {
//        //Debug.Log(spacesToMove);
//        Tile[] tiles = GetTilesAhead(spacesToMove);

//        if (tiles == null)
//        {
//            // We aren't moving at all, so just return our current tile?
//            return CurrentTile;
//        }

//        return tiles[tiles.Length - 1];
//    }

//    public bool CanLegallyMoveAhead(int spacesToMove)
//    {
//        if (CurrentTile != null && CurrentTile.IsScoringSpace)
//        {
//            // This stone is already on a scoring tile, so we can't move.
//            return false;
//        }

//        Tile theTile = GetTileAhead(spacesToMove);

//        return CanLegallyMoveTo(theTile);
//    }

//    bool CanLegallyMoveTo(Tile destinationTile)
//    {
//        //Debug.Log("CanLegallyMoveTo: " + destinationTile);

//        if (destinationTile == null)
//        {
//            // NOTE!  A null tile means we are overshooting the victory roll
//            // and this is NOT legal (apparently) in the Royal Game of Ur
//            return false;


//            // We're trying to move off the board and score, which is legal
//            //Debug.Log("We're trying to move off the board and score, which is legal");
//            //return true;
//        }

//        // Is the tile empty?
//        if (destinationTile.PlayerStone == null)
//        {
//            return true;
//        }

//        // Is it one of our stones?
//        if (destinationTile.PlayerStone.PlayerId == this.PlayerId)
//        {
//            // We can't land on our own stone.
//            return false;
//        }

//        // If it's an enemy stone, is it in a safe square?
//        if (destinationTile.IsRollAgain == true)
//        {
//            // Can't bop someone on a safe tile!
//            return false;
//        }

//        // If we've gotten here, it means we can legally land on the enemy stone and
//        // kick it off the board.
//        return true;
//    }

//    public void ReturnToStorage()
//    {
//        Debug.Log("ReturnToStorage");
//        //currentTile.PlayerStone = null;
//        //currentTile = null;

//        this.isAnimating = true;
//        theStateManager.AnimationsPlaying++;

//        moveQueue = null;

//        // Save our current position
//        Vector3 savePosition = this.transform.position;

//        //MyStoneStorage.AddStoneToStorage(this.gameObject);

//        // Set our new position to the animation target
//        SetNewTargetPosition(this.transform.position);

//        // Restore our saved position
//        this.transform.position = savePosition;
//    }

//}









//{
//    // Use this for initialization
//    void Start()
//    {
//        theStateManager = GameObject.FindObjectOfType<StateManager>();
//        targetPosition = this.transform.position;
//    }

//    public Tile StartingTile;
//    public Tile CurrentTile { get; protected set; }

//    public int PlayerId;
//    //public StoneStorage MyStoneStorage;

//    bool scoreMe = false;

//    StateManager theStateManager;

//    Tile[] moveQueue;
//    int moveQueueIndex;

//    bool isAnimating = false;

//    Vector3 targetPosition;
//    Vector3 velocity;
//    float smoothTime = 0.25f;
//    float smoothTimeVertical = 0.1f;
//    float smoothDistance = 0.01f;
//    float smoothHeight = 0.5f;

//    PlayerStone stoneToBop;


//    // Update is called once per frame
//    void Update()
//    {
//        if (isAnimating == false)
//        {
//            // Nothing for us to do.
//            return;
//        }

//        if (Vector3.Distance(
//               new Vector3(this.transform.position.x, targetPosition.y, this.transform.position.z),
//               targetPosition) < smoothDistance)
//        {
//            // We've reached the target position -- do we still have moves in the queue?

//            if (
//                (moveQueue == null || moveQueueIndex == (moveQueue.Length))
//                &&
//                ((this.transform.position.y - smoothDistance) > targetPosition.y)
//            )
//            {
//                // We are totally out of moves (and too high up), the only thing left to do is drop down.
//                this.transform.position = Vector3.SmoothDamp(
//                    this.transform.position,
//                    new Vector3(this.transform.position.x, targetPosition.y, this.transform.position.z),
//                    ref velocity,
//                    smoothTimeVertical);

//                // Check for bops
//                if (stoneToBop != null)
//                {
//                    stoneToBop.ReturnToStorage();
//                    stoneToBop = null;
//                }
//            }
//            else
//            {
//                // Right position, right height -- let's advance the queue
//                AdvanceMoveQueue();
//            }
//        }
//        else if (this.transform.position.y < (smoothHeight - smoothDistance))
//        {
//            // We want to rise up before we move sideways.
//            this.transform.position = Vector3.SmoothDamp(
//                this.transform.position,
//                new Vector3(this.transform.position.x, smoothHeight, this.transform.position.z),
//                ref velocity,
//                smoothTimeVertical);
//        }
//        else
//        {
//            // Normal movement (sideways)
//            this.transform.position = Vector3.SmoothDamp(
//                this.transform.position,
//                new Vector3(targetPosition.x, smoothHeight, targetPosition.z),
//                ref velocity,
//                smoothTime);
//        }

//    }

//    void AdvanceMoveQueue()
//    {
//        if (moveQueue != null && moveQueueIndex < moveQueue.Length)
//        {
//            Tile nextTile = moveQueue[moveQueueIndex];
//            if (nextTile == null)
//            {
//                // We are probably being scored
//                // TODO: Move us to the scored pile
//                Debug.Log("SCORING TILE!");
//                SetNewTargetPosition(this.transform.position + Vector3.right * 10f);
//            }
//            else
//            {
//                SetNewTargetPosition(nextTile.transform.position);
//                moveQueueIndex++;
//            }
//        }
//        else
//        {
//            // The movement queue is empty, so we are done animating!
//            //Debug.Log("Done animating!");
//            this.isAnimating = false;
//            theStateManager.AnimationsPlaying--;

//            // Are we on a roll again space?
//            if (CurrentTile != null && CurrentTile.IsRollAgain)
//            {
//                theStateManager.RollAgain();
//            }
//        }

//    }

//    void SetNewTargetPosition(Vector3 pos)
//    {
//        targetPosition = pos;
//        velocity = Vector3.zero;
//        isAnimating = true;
//    }

//    void OnMouseUp()
//    {
//        // TODO:  Is the mouse over a UI element? In which case, ignore this click.
//        MoveMe();
//    }

//    public void MoveMe()
//    {
//        // Is this the correct player?
//        if (theStateManager.CurrentPlayerId != PlayerId)
//        {
//            return;
//        }

//        // Have we rolled the dice?
//        if (theStateManager.IsDoneRolling == false)
//        {
//            // We can't move yet.
//            return;
//        }
//        if (theStateManager.IsDoneClicking == true)
//        {
//            // We've already done a move!
//            return;
//        }


//        // Where should we end up?
//        moveQueue = GetTilesAhead(spacesToMove);
//        Tile finalTile = moveQueue[moveQueue.Length - 1];

//        // TODO: Check to see if the destination is legal!

//        if (finalTile == null)
//        {
//            // Hey, we're scoring this stone!
//            scoreMe = true;
//        }
//        else
//        {
//            if (CanLegallyMoveTo(finalTile) == false)
//            {
//                // Not allowed!
//                finalTile = CurrentTile;
//                moveQueue = null;
//                return;
//            }

//            // If there is an enemy tile in our legal space, the we kick it out.
//            if (finalTile.PlayerStone != null)
//            {
//                //finalTile.PlayerStone.ReturnToStorage();
//                stoneToBop = finalTile.PlayerStone;
//                stoneToBop.CurrentTile.PlayerStone = null;
//                stoneToBop.CurrentTile = null;
//            }
//        }

//        this.transform.SetParent(null); // Become Batman

//        // Remove ourselves from our old tile
//        if (CurrentTile != null)
//        {
//            CurrentTile.PlayerStone = null;
//        }

//        // Even before the animation is done, set our current tile to the new tile
//        CurrentTile = finalTile;
//        if (finalTile.IsScoringSpace == false)   // "Scoring" tiles are always "empty"
//        {
//            finalTile.PlayerStone = this;
//        }

//        moveQueueIndex = 0;

//        theStateManager.IsDoneClicking = true;
//        this.isAnimating = true;
//        theStateManager.AnimationsPlaying++;
//    }

//    // Return the list of tiles __ moves ahead of us
//    public Tile[] GetTilesAhead(int spacesToMove)
//    {
//        if (spacesToMove == 0)
//        {
//            return null;
//        }

//        // Where should we end up?

//        Tile[] listOfTiles = new Tile[spacesToMove];
//        Tile finalTile = CurrentTile;

//        for (int i = 0; i < spacesToMove; i++)
//        {
//            if (finalTile == null)
//            {
//                finalTile = StartingTile;
//            }
//            else
//            {
//                if (finalTile.NextTiles == null || finalTile.NextTiles.Length == 0)
//                {
//                    // We are overshooting the victory -- so just return some nulls in the array
//                    // Just break and we'll return the array, which is going to have nulls
//                    // at the end.
//                    break;
//                }
//                else if (finalTile.NextTiles.Length > 1)
//                {
//                    // Branch based on player id
//                    finalTile = finalTile.NextTiles[PlayerId];
//                }
//                else
//                {
//                    finalTile = finalTile.NextTiles[0];
//                }
//            }

//            listOfTiles[i] = finalTile;
//        }

//        return listOfTiles;
//    }

//    public Tile GetTileAhead()
//    {
//        return GetTileAhead(theStateManager.DiceTotal);
//    }


//    // Return the final tile we'd land on if we moved __ spaces
//    public Tile GetTileAhead(int spacesToMove)
//    {
//        //Debug.Log(spacesToMove);
//        Tile[] tiles = GetTilesAhead(spacesToMove);

//        if (tiles == null)
//        {
//            // We aren't moving at all, so just return our current tile?
//            return CurrentTile;
//        }

//        return tiles[tiles.Length - 1];
//    }

//    public bool CanLegallyMoveAhead(int spacesToMove)
//    {
//        if (CurrentTile != null && CurrentTile.IsScoringSpace)
//        {
//            // This stone is already on a scoring tile, so we can't move.
//            return false;
//        }

//        Tile theTile = GetTileAhead(spacesToMove);

//        return CanLegallyMoveTo(theTile);
//    }

//    bool CanLegallyMoveTo(Tile destinationTile)
//    {
//        //Debug.Log("CanLegallyMoveTo: " + destinationTile);

//        if (destinationTile == null)
//        {
//            // NOTE!  A null tile means we are overshooting the victory roll
//            // and this is NOT legal (apparently) in the Royal Game of Ur
//            return false;


//            // We're trying to move off the board and score, which is legal
//            //Debug.Log("We're trying to move off the board and score, which is legal");
//            //return true;
//        }

//        // Is the tile empty?
//        if (destinationTile.PlayerStone == null)
//        {
//            return true;
//        }

//        // Is it one of our stones?
//        if (destinationTile.PlayerStone.PlayerId == this.PlayerId)
//        {
//            // We can't land on our own stone.
//            return false;
//        }

//        // If it's an enemy stone, is it in a safe square?
//        if (destinationTile.IsRollAgain == true)
//        {
//            // Can't bop someone on a safe tile!
//            return false;
//        }

//        // If we've gotten here, it means we can legally land on the enemy stone and
//        // kick it off the board.
//        return true;
//    }

//    public void ReturnToStorage()
//    {
//        Debug.Log("ReturnToStorage");
//        //currentTile.PlayerStone = null;
//        //currentTile = null;

//        this.isAnimating = true;
//        theStateManager.AnimationsPlaying++;

//        moveQueue = null;

//        // Save our current position
//        Vector3 savePosition = this.transform.position;

//        //MyStoneStorage.AddStoneToStorage(this.gameObject);

//        // Set our new position to the animation target
//        SetNewTargetPosition(this.transform.position);

//        // Restore our saved position
//        this.transform.position = savePosition;
//    }

//}