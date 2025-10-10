//using Photon.Pun;
//using UnityEngine;
//using TMPro;

//public class TurnManager : MonoBehaviourPunCallbacks
//{
//    public static TurnManager Instance;

//    public TMP_Text turnText;
//    public GameObject rollButton;

//    private int currentPlayerIndex = 0;

//    private void Awake()
//    {
//        Instance = this;
//    }

//    private void Start()
//    {
//        UpdateUI();
//    }

//    public void NextTurn()
//    {
//        if (PhotonNetwork.IsMasterClient)
//        {
//            currentPlayerIndex = (currentPlayerIndex + 1) % PhotonNetwork.CurrentRoom.PlayerCount;
//            photonView.RPC("RPC_SetTurn", RpcTarget.All, currentPlayerIndex);
//        }
//    }

//    [PunRPC]
//    private void RPC_SetTurn(int newIndex)
//    {
//        currentPlayerIndex = newIndex;
//        UpdateUI();
//    }

//    private void UpdateUI()
//    {
//        int myIndex = PhotonNetwork.LocalPlayer.ActorNumber - 1;

//        bool isMyTurn = (myIndex == currentPlayerIndex);

//        // Show/hide the roll button based on whose turn it is
//        rollButton.SetActive(isMyTurn);

//        // Update the UI text for everyone
//        turnText.text = isMyTurn
//            ? "Your Turn!"
//            : $"Waiting for Player {currentPlayerIndex + 1}...";
//    }

//    // Called by the Roll button
//    public void OnRollButtonPressed()
//    {
//        if (PhotonNetwork.LocalPlayer.ActorNumber - 1 != currentPlayerIndex) return;

//        // Call your dice roll here, for example:
//        DiceRoller.Instance.RollTheDice();

//        // After the dice roll finishes, call NextTurn()
//        // (You’ll hook this to your dice animation finish event)
//    }
//}
