using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DiceRoller : MonoBehaviour
{
    // ✅ Add a Singleton Instance
  
    public static DiceRoller Instance { get; private set; }

    public int[] DiceValues;
    public int DiceTotal;
    public bool doneRolling = false;

    public Sprite[] DiceImageOne;
    public Sprite[] DiceImageZero;

    void Awake()
    {
        // ✅ Setup singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        DiceValues = new int[4];
    }

    public void RollTheDice()
    {
        DiceTotal = 0;
        for (int i = 0; i < DiceValues.Length; i++)
        {
            DiceValues[i] = Random.Range(0, 2);
            DiceTotal += DiceValues[i];

            if (DiceValues[i] == 0)
            {
                transform.GetChild(i).GetComponent<Image>().sprite =
                    DiceImageZero[Random.Range(0, DiceImageZero.Length)];
            }
            else
            {
                transform.GetChild(i).GetComponent<Image>().sprite =
                    DiceImageOne[Random.Range(0, DiceImageOne.Length)];
            }
        }

        if (DiceTotal == 0)
        {
            DiceTotal = 8;
        }

        doneRolling = true;
        Debug.Log("Rolled: " + DiceTotal);
    }
   
}
