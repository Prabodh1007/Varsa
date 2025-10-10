using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;   // <<< added

public class DiceTotalDisplay : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {
        theDiceRoller = GameObject.FindAnyObjectByType<DiceRoller>();
        //theStateManager = GameObject.FindObjectOfType<StateManager>();
    }

    DiceRoller theDiceRoller;
    //StateManager theStateManager;

    // Update is called once per frame
    void Update()
    {
        GetComponent<TextMeshProUGUI>().text = "= " + theDiceRoller.DiceTotal;
        //if (theDiceRoller.doneRolling == true)
        //{
        //    // switched to TextMeshProUGUI
        //    GetComponent<TextMeshProUGUI>().text = "= ?";
        //}
        //else
        //{
        //    GetComponent<TextMeshProUGUI>().text = "= " + theDiceRoller.DiceTotal;
        //}
    }
}
