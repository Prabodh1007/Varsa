using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensures this UI element is anchored to the top-right corner and configures
/// the parent Canvas Scaler for responsive scaling across different screens.
/// Attach this to the GameObject that contains the TextMeshProUGUI used by ScoreManager.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ScoreUIAnchorer : MonoBehaviour
{
    [Tooltip("Padding from the top-right corner in pixels: (x = right padding, y = top padding)")]
    public Vector2 padding = new Vector2(12f, 12f);

    [Header("Canvas Scaler (will be added if missing)")]
    public CanvasScaler.ScaleMode scaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)]
    public float matchWidthOrHeight = 0.5f;

    void Reset()
    {
        ApplyAnchors();
    }

    void Awake()
    {
        ApplyAnchors();
    }

    void OnValidate()
    {
        // Keep the inspector feedback immediate in Edit mode
        ApplyAnchors();
    }

    void ApplyAnchors()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) return;

        // Anchor to top-right
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);

        // Set position based on padding (negative x, negative y because anchored to top-right)
        rt.anchoredPosition = new Vector2(-Mathf.Abs(padding.x), -Mathf.Abs(padding.y));

        // Ensure parent Canvas has a CanvasScaler configured for responsive UI
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            CanvasScaler cs = parentCanvas.GetComponent<CanvasScaler>();
            if (cs == null)
            {
                cs = parentCanvas.gameObject.AddComponent<CanvasScaler>();
            }
            cs.uiScaleMode = scaleMode;
            cs.referenceResolution = referenceResolution;
            cs.matchWidthOrHeight = matchWidthOrHeight;
        }
    }
}
