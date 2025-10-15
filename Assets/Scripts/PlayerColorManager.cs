using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime; // Added for RaiseEventOptions, ReceiverGroup, etc.
using ExitGames.Client.Photon; // For Hashtable, EventData

[RequireComponent(typeof(PhotonView))]
public class PlayerColorManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // Six predefined palette colors (customize as needed)
    public Color[] palette = new Color[] {
        new Color(1f, 0.2f, 0.2f), // red
        new Color(0.2f, 0.4f, 1f), // blue
        new Color(0.2f, 1f, 0.3f), // green
        new Color(1f, 0.9f, 0.2f), // yellow
        new Color(1f, 0.3f, 1f),   // magenta
        new Color(0.2f, 1f, 1f)    // cyan
    };

    // playerIndex -> palette index (-1 = unassigned)
    int[] playerColorIndexes = new int[4] { -1, -1, -1, -1 };
    List<int> available = new List<int>();

    public event Action<int, int> OnColorAssigned; // (playerIndex, paletteIndex)

    void Awake()
    {
        // init available
        available.Clear();
        for (int i = 0; i < palette.Length; i++) available.Add(i);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        // MasterClient initializes room props if missing
        if (PhotonNetwork.IsMasterClient)
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room == null) return;
            var props = room.CustomProperties;
            if (props == null || !props.ContainsKey("playerColorIndexes"))
            {
                Hashtable ht = new Hashtable();
                ht["playerColorIndexes"] = new int[] { -1, -1, -1, -1 };
                ht["availableColorIndexes"] = available.ToArray();
                room.SetCustomProperties(ht);
            }
            else
            {
                // sync local from existing props
                SetupFromRoomProperties();
            }
        }
        else
        {
            // non-master just sync
            SetupFromRoomProperties();
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);
        SetupFromRoomProperties();
    }

    void SetupFromRoomProperties()
    {
        if (!PhotonNetwork.InRoom) return;
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;
        var props = room.CustomProperties;
        if (props == null) return;
        int[] old = (int[])playerColorIndexes.Clone();

        if (props.ContainsKey("playerColorIndexes"))
        {
            playerColorIndexes = ObjToIntArray(props["playerColorIndexes"], playerColorIndexes.Length);
        }

        if (props.ContainsKey("availableColorIndexes"))
        {
            int[] av = ObjToIntArray(props["availableColorIndexes"], 0);
            available = new List<int>(av);
        }

        // Notify listeners of any assignments (helps late-joiners / race conditions)
        for (int i = 0; i < playerColorIndexes.Length; i++)
        {
            int newIdx = playerColorIndexes[i];
            int oldIdx = (old != null && i < old.Length) ? old[i] : -1;
            if (newIdx >= 0 && newIdx != oldIdx)
            {
                Debug.Log($"PlayerColorManager: SetupFromRoomProperties - player {i} assigned color {newIdx}");
                OnColorAssigned?.Invoke(i, newIdx);
            }
        }
    }

    int[] ObjToIntArray(object o, int fallbackLength)
    {
        if (o == null) return new int[fallbackLength];
        if (o is int[] ia) return ia;
        if (o is object[] oa)
        {
            int[] res = new int[oa.Length];
            for (int i = 0; i < oa.Length; i++) res[i] = Convert.ToInt32(oa[i]);
            return res;
        }
        try
        {
            var list = o as System.Collections.IList;
            if (list != null)
            {
                int[] res = new int[list.Count];
                for (int i = 0; i < list.Count; i++) res[i] = Convert.ToInt32(list[i]);
                return res;
            }
        }
        catch { }
        return new int[fallbackLength];
    }

    // Public API
    public int GetAssignedColorIndex(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerColorIndexes.Length) return -1;
        return playerColorIndexes[playerIndex];
    }

    public Color GetColorByIndex(int idx)
    {
        if (idx < 0 || idx >= palette.Length) return Color.white;
        return palette[idx];
    }

    // Called by PlayerStone (local) to ensure assignment exists. Master assigns immediately.
    public void EnsureColorAssignedByMaster(int playerIndex)
    {
        if (GetAssignedColorIndex(playerIndex) >= 0)
        {
            // already assigned locally
            OnColorAssigned?.Invoke(playerIndex, playerColorIndexes[playerIndex]);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            AssignColorToPlayer(playerIndex);
        }
        else
        {
            // ask master via RaiseEvent (avoids needing a valid PhotonView on this scene object)
            byte evCode = 200; // custom event code for color request
            object content = playerIndex;
            var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
            PhotonNetwork.RaiseEvent(evCode, content, options, SendOptions.SendReliable);
        }
    }

    void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // IOnEventCallback
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent == null) return;
        byte evCode = photonEvent.Code;
        if (evCode == 200)
        {
            // Assign request from a client
            if (!PhotonNetwork.IsMasterClient) return;
            object content = photonEvent.CustomData;
            int pIndex = Convert.ToInt32(content);
            AssignColorToPlayer(pIndex);
        }
    }

    void AssignColorToPlayer(int pIndex)
    {
        // refresh lists from room props
        SetupFromRoomProperties();

        int chosen = -1;
        if (available != null && available.Count > 0)
        {
            chosen = available[0];
            available.RemoveAt(0);
        }

        if (pIndex >= 0 && pIndex < playerColorIndexes.Length)
            playerColorIndexes[pIndex] = chosen;

        // update room props so late joiners get state
        Hashtable ht = new Hashtable();
        ht["playerColorIndexes"] = playerColorIndexes;
        ht["availableColorIndexes"] = available.ToArray();
        PhotonNetwork.CurrentRoom.SetCustomProperties(ht);

        // Notify local listeners immediately and rely on room properties for other clients.
        Debug.Log($"PlayerColorManager: Assigned color index {chosen} to player {pIndex} and updated room props.");
        OnColorAssigned?.Invoke(pIndex, chosen);
    }

    // Debug helper - prints current mapping
    public void DumpAssignments()
    {
        string s = "PlayerColorManager assignments:";
        for (int i = 0; i < playerColorIndexes.Length; i++)
        {
            int idx = playerColorIndexes[i];
            string col = (idx >= 0 && idx < palette.Length) ? palette[idx].ToString() : "unassigned";
            s += $"\n player {i}: {idx} -> {col}";
        }
        Debug.Log(s);
    }

    // More detailed dump including Photon players (if available)
    public void DumpAssignmentsWithPlayers()
    {
        string s = "PlayerColorManager assignments with Photon players:";
        var players = PhotonNetwork.PlayerList;
        for (int i = 0; i < playerColorIndexes.Length; i++)
        {
            int idx = playerColorIndexes[i];
            string col = (idx >= 0 && idx < palette.Length) ? palette[idx].ToString() : "unassigned";
            string name = (i < players.Length) ? players[i].NickName : $"playerSlot{i}";
            s += $"\n slot {i} ({name}): {idx} -> {col}";
        }
        Debug.Log(s);
    }

    void LogRoomProps()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        object playerColorIndexesObj = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("playerColorIndexes") ? PhotonNetwork.CurrentRoom.CustomProperties["playerColorIndexes"] : null;
        object availableColorIndexesObj = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("availableColorIndexes") ? PhotonNetwork.CurrentRoom.CustomProperties["availableColorIndexes"] : null;
        Debug.Log($"PlayerColorManager: RoomProps playerColorIndexes={playerColorIndexesObj} available={availableColorIndexesObj}");
    }

    // Dump PhotonViews in scene for diagnostics
    public void DumpPhotonViews()
    {
        var pvs = FindObjectsOfType<PhotonView>();
        Debug.Log($"PlayerColorManager: Found {pvs.Length} PhotonViews in scene");
        foreach (var pv in pvs)
        {
            Debug.Log($" PV id={pv.ViewID}, owner={(pv.Owner != null ? pv.Owner.ActorNumber : -1)}, isMine={pv.IsMine}, GO={pv.gameObject.name}");
        }
    }
}