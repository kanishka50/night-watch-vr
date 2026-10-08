using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Base for things in the world that the player points at and presses G on, but does not carry
// (the ladder handles, a bonfire, the fence energiser). It handles:
//  - input through an XR Simple Interactable: grab (G) counts as one press
//  - letting go straight away (the hands use toggle grab, so it would stay "held")
//  - a highlight while a pointer is on it, like the tools in the hut
// Each child class overrides OnPressed.
[RequireComponent(typeof(XRSimpleInteractable))]
public abstract class PressTarget : MonoBehaviour
{
    [Tooltip("Colour the object turns while a pointer is on it.")]
    public Color highlightColor = new Color(1f, 0.9f, 0.5f);
    [Tooltip("The shapes that light up. Empty = the renderer on this object.")]
    public Renderer[] highlightRenderers;

    protected XRSimpleInteractable interactable;
    MaterialPropertyBlock highlight;
    bool releaseNextFrame;

    protected virtual void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (highlightRenderers == null || highlightRenderers.Length == 0)
        {
            var own = GetComponent<Renderer>();
            highlightRenderers = own != null ? new[] { own } : new Renderer[0];
        }
    }

    protected virtual void OnEnable()
    {
        interactable.selectEntered.AddListener(HandleSelect);
        interactable.hoverEntered.AddListener(HandleHoverStart);
        interactable.hoverExited.AddListener(HandleHoverEnd);
    }

    protected virtual void OnDisable()
    {
        interactable.selectEntered.RemoveListener(HandleSelect);
        interactable.hoverEntered.RemoveListener(HandleHoverStart);
        interactable.hoverExited.RemoveListener(HandleHoverEnd);
        SetHighlight(false);
    }

    protected virtual void Update()
    {
        if (!releaseNextFrame) return;
        releaseNextFrame = false;
        if (interactable.isSelected && interactable.interactionManager != null)
            interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)interactable);
    }

    protected abstract void OnPressed();

    void HandleSelect(SelectEnterEventArgs args)
    {
        releaseNextFrame = true;
        OnPressed();
    }

    void HandleHoverStart(HoverEnterEventArgs args) => SetHighlight(true);

    void HandleHoverEnd(HoverExitEventArgs args)
    {
        // Several pointers can hover at once; only clear when none are left.
        if (!interactable.isHovered) SetHighlight(false);
    }

    void SetHighlight(bool on)
    {
        if (highlightRenderers == null) return;
        if (on && highlight == null)
        {
            highlight = new MaterialPropertyBlock();
            highlight.SetColor("_BaseColor", highlightColor);
        }
        foreach (var r in highlightRenderers)
            if (r != null) r.SetPropertyBlock(on ? highlight : null);
    }
}
