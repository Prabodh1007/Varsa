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

    void Start()
    {
        boardManager = FindAnyObjectByType<BoardManager>();
        diceRoller = FindAnyObjectByType<DiceRoller>();

        if (photonView.InstantiationData != null && photonView.InstantiationData.Length >= 2)
        {
            playerIndex = (int)photonView.InstantiationData[0];
            pieceIndex = (int)photonView.InstantiationData[1];
        }

        if (boardManager != null)
            path = boardManager.GetPlayerPath(playerIndex);

        targetPosition = transform.position;

        // Position slightly above ghar
        Vector3 pos = transform.position;
        pos.y += 0.2f;
        transform.position = pos;
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