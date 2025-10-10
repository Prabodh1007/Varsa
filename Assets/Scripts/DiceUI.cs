using UnityEngine;
using UnityEngine.UIElements;

public class DiceUI : MonoBehaviour
{
    public VisualTreeAsset uiAsset; // assign your UXML here
    public Texture2D diceTexture;   // assign your dice texture in inspector

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        uiAsset.CloneTree(root);

        var diceImage = root.Q<Image>("diceImage");
        if (diceImage != null)
        {
            diceImage.image = diceTexture; // assign Texture2D
        }
    }
}
