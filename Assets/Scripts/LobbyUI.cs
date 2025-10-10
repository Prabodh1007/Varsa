using UnityEngine;
using TMPro; // ✅ Import TextMeshPro namespace

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField roomNameInput; 

    public void OnCreateJoinButton()
    {
        string rn = roomNameInput != null ? roomNameInput.text : null;
        PhotonLauncher.Instance.CreateOrJoinRoom(rn);
    }

    public void OnQuickJoinButton()
    {
        PhotonLauncher.Instance.JoinRandomRoom();
    }
}
