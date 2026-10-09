using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Base for every tool the player can grab in the hut (fire torch, firecracker, tins,
// flashlight, hakka patas). It handles the parts all tools share:
//  - input through the XR Grab Interactable: grab (grip), use (trigger), let go
//  - a highlight while a pointer is on it, so the player knows it can be grabbed
//  - putting it back on the table if it is dropped over the side of the hut
// Each tool overrides OnGrabbed, OnUsed and OnReleased.
[RequireComponent(typeof(XRGrabInteractable))]
public class PlayerAction : MonoBehaviour
{
    public static readonly List<PlayerAction> All = new List<PlayerAction>();

    [Tooltip("Colour the object turns while a pointer is on it.")]
    public Color highlightColor = new Color(1f, 0.9f, 0.5f);
    [Tooltip("Put it back where it started if it falls off the hut.")]
    public bool returnIfDropped = true;
    [Tooltip("Below this height it counts as fallen off the hut.")]
    public float resetBelowHeight = 2f;

    protected XRGrabInteractable grab;
    protected Rigidbody body;
    MeshRenderer[] shapes;
    MaterialPropertyBlock highlight;
    Vector3 startPosition;
    Quaternion startRotation;

    public bool IsHeld => grab != null && grab.isSelected;

    public static bool AnyHeld => All.Exists(t => t.IsHeld);

    protected virtual void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
        // Every shape of the tool lights up (not the flame particles).
        shapes = GetComponentsInChildren<MeshRenderer>(true);
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    protected virtual void OnEnable()
    {
        All.Add(this);
        grab.selectEntered.AddListener(HandleGrab);
        grab.selectExited.AddListener(HandleRelease);
        grab.activated.AddListener(HandleUse);
        grab.hoverEntered.AddListener(HandleHoverStart);
        grab.hoverExited.AddListener(HandleHoverEnd);
    }

    protected virtual void OnDisable()
    {
        All.Remove(this);
        grab.selectEntered.RemoveListener(HandleGrab);
        grab.selectExited.RemoveListener(HandleRelease);
        grab.activated.RemoveListener(HandleUse);
        grab.hoverEntered.RemoveListener(HandleHoverStart);
        grab.hoverExited.RemoveListener(HandleHoverEnd);
    }

    protected virtual void Update()
    {
        if (returnIfDropped && !IsHeld && transform.position.y < resetBelowHeight) ReturnToStart();
    }

    protected virtual void OnGrabbed() { }

    // A one-line hint shown low in the view the first time this kind of tool is grabbed.
    protected virtual string Hint => null;
    static readonly HashSet<System.Type> hinted = new HashSet<System.Type>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetHints() => hinted.Clear();
    protected virtual void OnReleased() { }
    protected virtual void OnUsed() { }

    // Tools stay in the hut: when the player climbs down, a tool lying away from its place goes
    // back on the table. Not for things meant to leave the hut (a lit cracker, hakka patas).
    // Returns true if it was moved.
    public bool PutBack()
    {
        if (!returnIfDropped || IsHeld) return false;
        if (Vector3.Distance(transform.position, startPosition) < 0.3f) return false;
        ReturnToStart();
        return true;
    }

    protected void ReturnToStart()
    {
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(startPosition, startRotation);
    }

    void HandleGrab(SelectEnterEventArgs args)
    {
        if (Hint != null && hinted.Add(GetType())) HintDisplay.Show(Hint);
        OnGrabbed();
    }
    void HandleRelease(SelectExitEventArgs args) => OnReleased();
    void HandleUse(ActivateEventArgs args) => OnUsed();
    void HandleHoverStart(HoverEnterEventArgs args) => SetHighlight(true);

    void HandleHoverEnd(HoverExitEventArgs args)
    {
        // Several pointers can hover at once; only clear when none are left.
        if (!grab.isHovered) SetHighlight(false);
    }

    void SetHighlight(bool on)
    {
        if (shapes == null) return;
        if (on && highlight == null)
        {
            highlight = new MaterialPropertyBlock();
            highlight.SetColor("_BaseColor", highlightColor);
        }
        foreach (var shape in shapes)
            if (shape != null) shape.SetPropertyBlock(on ? highlight : null);
    }
}
