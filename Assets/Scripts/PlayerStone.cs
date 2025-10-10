using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerStone : MonoBehaviourPun, IPointerClickHandler, IPunObservable
{
    // assigned via instantiation data (player spawned them)
    public int playerIndex = 0;   // 0..3
    public int pieceIndex = 0;    // 0..3 (which kavdi in ghar)

    // resolved at runtime
    Tile[] path;                 // this player's path (BoardManager.GetPlayerPath(playerIndex))
    Tile[] moveQueue;            // tiles we are animating to (constructed from global tile indices)
    int moveQueueIndex = 0;
    bool isAnimating = false;

    // movement
    Vector3 targetPosition;
    Vector3 velocity = Vector3.zero;
    public float smoothTime = 0.22f;
    const float REACH_EPS = 0.02f;

    BoardManager boardManager;
    DiceRoller diceRoller;

    // current logical index corresponds to index inside the player's path array.
    // -1 = still in ghar (not entered path). 0..(path.Length-1): path index.
    int currentPathIndex = -1;

    void Awake()
    {
        // nothing heavy here
    }

    void Start()
    {
        boardManager = UnityEngine.Object.FindAnyObjectByType<BoardManager>();
        diceRoller = UnityEngine.Object.FindAnyObjectByType<DiceRoller>();

        // read instantiation data (passed from PlayerSpawner)
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
            Debug.LogError("PlayerStone: BoardManager not found in scene.");
        }

        // place where the spawner put us; targetPosition initialized to current transform
        targetPosition = transform.position;
    }

    void Update()
    {
        // Animate if anyone started an animation (owner initiates RPC, all clients animate)
        if (isAnimating && moveQueue != null && moveQueue.Length > 0 && moveQueueIndex < moveQueue.Length)
        {
            Tile tileTarget = moveQueue[moveQueueIndex];
            if (tileTarget == null)
            {
                // null means scoring/exit — stop animation
                isAnimating = false;
                moveQueue = null;
                moveQueueIndex = 0;
                return;
            }

            Vector3 top = GetTopPositionOnTile(tileTarget);
            transform.position = Vector3.SmoothDamp(transform.position, top, ref velocity, smoothTime);

            if (Vector3.Distance(transform.position, top) <= REACH_EPS)
            {
                // arrived at this tile; advance
                moveQueueIndex++;
                if (moveQueueIndex >= moveQueue.Length)
                {
                    // finished all moves
                    isAnimating = false;
                    moveQueue = null;
                    moveQueueIndex = 0;

                    // update currentPathIndex: final tile -> find its index in the player's path
                    // (only if path is assigned)
                    Tile finalTile = tileTarget;
                    if (path != null && finalTile != null)
                    {
                        currentPathIndex = System.Array.IndexOf(path, finalTile);
                    }
                }
            }
        }
    }

    // compute position to sit on top of tile (so piece doesn't sink)
    Vector3 GetTopPositionOnTile(Tile t)
    {
        if (t == null) return transform.position;
        Collider col = t.GetComponent<Collider>();
        float tileTopY = (col != null) ? col.bounds.max.y : t.transform.position.y;
        float pieceHalf = 0.5f;
        Collider myCol = GetComponent<Collider>();
        if (myCol != null) pieceHalf = myCol.bounds.extents.y;
        return new Vector3(t.transform.position.x, tileTopY + pieceHalf + 0.01f, t.transform.position.z);
    }

    // Owner click -> compute global tile indices to move along, send RPC to all clients
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!photonView.IsMine)
        {
            Debug.Log("PlayerStone: Not your piece.");
            return;
        }

        if (diceRoller == null)
            diceRoller = UnityEngine.Object.FindAnyObjectByType<DiceRoller>();

        if (diceRoller == null)
        {
            Debug.LogWarning("PlayerStone: DiceRoller not found, cannot move.");
            return;
        }

        int spaces = diceRoller.DiceTotal;
        if (spaces <= 0) return;

        if (path == null || path.Length == 0)
        {
            Debug.LogWarning("PlayerStone: No path assigned for player " + playerIndex);
            return;
        }

        List<int> globalIndices = new List<int>(spaces);
        int simulatedIndex = currentPathIndex; // -1 = ghar

        for (int i = 0; i < spaces; i++)
        {
            int nextPathIndex = simulatedIndex + 1;
            if (nextPathIndex >= path.Length)
            {
                // overshoot / scoring - use -1 to indicate null (scoring)
                globalIndices.Add(-1);
                simulatedIndex = nextPathIndex; // keep advancing but final will be -1
            }
            else
            {
                Tile nextTile = path[nextPathIndex];
                int global = (boardManager != null) ? boardManager.GetIndexOfTile(nextTile) : -1;
                globalIndices.Add(global);
                simulatedIndex = nextPathIndex;
            }
        }

        // Broadcast to everyone to start same animation sequence
        photonView.RPC(nameof(RPC_StartMove), RpcTarget.All, globalIndices.ToArray());
    }

    // RPC: reconstruct queue from global indices and begin animation on every client
    [PunRPC]
    void RPC_StartMove(int[] globalIndices, PhotonMessageInfo info)
    {
        if (boardManager == null)
            boardManager = UnityEngine.Object.FindAnyObjectByType<BoardManager>();

        if (globalIndices == null || globalIndices.Length == 0)
        {
            Debug.LogWarning("RPC_StartMove received empty indices.");
            return;
        }

        List<Tile> list = new List<Tile>(globalIndices.Length);
        foreach (int gi in globalIndices)
        {
            if (gi < 0)
            {
                list.Add(null); // scoring / off-board marker
            }
            else
            {
                Tile t = (boardManager != null) ? boardManager.GetTileByIndex(gi) : null;
                list.Add(t);
            }
        }

        moveQueue = list.ToArray();
        moveQueueIndex = 0;
        isAnimating = (moveQueue != null && moveQueue.Length > 0);

        // Immediately set target to the first tile if present so animation begins smoothly
        if (isAnimating && moveQueue[0] != null)
        {
            targetPosition = GetTopPositionOnTile(moveQueue[0]);
            // place transform a bit closer so smoothing works consistently
            // (we rely on Update to move it)
        }
    }

    // Photon sync so remote clients keep position stable when join / network hiccups
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
            // Accept networked position only for non-owned copies (owner already animates locally)
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