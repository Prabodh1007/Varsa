using Photon.Pun;
using UnityEngine;

public class PlayerSpawner : MonoBehaviourPunCallbacks
{
    [Tooltip("Prefab name inside Assets/Resources (case-sensitive)")]
    public string playerPrefabName = "Kavdi"; // make sure this exact prefab exists in Assets/Resources/Kavdi.prefab

    [Header("Per-player Ghar spawn positions (4 positions per player)")]
    public Transform[] player1Ghar; // element 0..3
    public Transform[] player2Ghar;
    public Transform[] player3Ghar;
    public Transform[] player4Ghar;

    bool hasSpawned = false;

    public override void OnJoinedRoom()
    {
        // Photon callback when joined -> spawn local player's pieces
        TrySpawn();
    }

    void Start()
    {
        // In case we are already in room at Start (editor), try spawn
        TrySpawn();
    }

    void TrySpawn()
    {
        if (hasSpawned) return;
        if (!PhotonNetwork.IsConnectedAndReady) return;
        if (!PhotonNetwork.InRoom) return;

        int actorIndex = Mathf.Clamp(PhotonNetwork.LocalPlayer.ActorNumber - 1, 0, 3);
        Transform[] myGhar = GetGharArray(actorIndex);
        if (myGhar == null || myGhar.Length < 4)
        {
            Debug.LogError($"PlayerSpawner: ghar array not assigned or too small for player {actorIndex}");
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            Vector3 spawnPos = myGhar[i].position;
            object[] instData = new object[] { actorIndex, i }; // pass playerIndex, pieceIndex
            GameObject spawned = PhotonNetwork.Instantiate(playerPrefabName, spawnPos, Quaternion.identity, 0, instData);
            if (spawned == null)
                Debug.LogError("PlayerSpawner: PhotonNetwork.Instantiate returned null (check Resources and prefab name).");
            else
            {
                var pv = spawned.GetComponent<Photon.Pun.PhotonView>();
                if (pv != null)
                {
                    Debug.Log($"PlayerSpawner: Spawned prefab with PhotonView id={pv.ViewID}, ownerActorNr={(pv.Owner!=null?pv.Owner.ActorNumber:-1)}, isMine={pv.IsMine}");
                }
                else
                {
                    Debug.LogWarning("PlayerSpawner: Spawned prefab has no PhotonView component.");
                }
            }
        }

        hasSpawned = true;
        Debug.Log($"PlayerSpawner: Spawned 4 pieces for player index {actorIndex}");
    }

    Transform[] GetGharArray(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return player1Ghar;
            case 1: return player2Ghar;
            case 2: return player3Ghar;
            case 3: return player4Ghar;
            default: return null;
        }
    }
}
