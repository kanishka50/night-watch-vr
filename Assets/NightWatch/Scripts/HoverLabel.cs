using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A name label that shows only while a pointer is on its thing (a tool, a handle, the phone),
// so the hut is not full of floating words. It fades in and out quickly.
[RequireComponent(typeof(TextMeshPro))]
public class HoverLabel : MonoBehaviour
{
    [Tooltip("Pointing at any of these shows the label (not while one is held).")]
    public XRBaseInteractable[] targets;
    [Tooltip("Also show while the player holds a fire torch or a firecracker (for the fire pot: light it here).")]
    public bool showWhileFireToolHeld;
    public float fadeSpeed = 8f;

    TextMeshPro text;
    float shown;

    void Awake()
    {
        text = GetComponent<TextMeshPro>();
        Apply();
    }

    // After the other scripts' Update, which may set the label's text and colour.
    void LateUpdate()
    {
        shown = Mathf.MoveTowards(shown, Wanted() ? 1f : 0f, fadeSpeed * Time.deltaTime);
        Apply();
        // Turned to the player, so it never reads mirrored from behind.
        if (shown > 0.01f) FaceViewer.Face(transform);
    }

    bool Wanted()
    {
        if (targets != null)
            foreach (var t in targets)
                if (t != null && t.isActiveAndEnabled && t.isHovered && !t.isSelected)
                    return true;
        if (showWhileFireToolHeld)
            foreach (var tool in PlayerAction.All)
                if (tool.IsHeld && (tool is FireTorch || tool is Firecracker))
                    return true;
        return false;
    }

    void Apply()
    {
        text.alpha = shown;
        text.enabled = shown > 0.01f;
    }
}
