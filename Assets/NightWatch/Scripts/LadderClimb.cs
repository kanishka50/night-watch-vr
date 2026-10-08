using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// The hut ladder. Point at it and grab (G) to climb down, or at the bottom to climb back up.
// Climbing down is a wrong choice: on the ground the player cannot escape, and elephants near a
// person on the ground get agitated. A charge that reaches the player on the ground ends the night.
[RequireComponent(typeof(XRSimpleInteractable))]
public class LadderClimb : MonoBehaviour
{
    [Tooltip("True on the top of the ladder (climb down), false at the bottom (climb up).")]
    public bool goesDown = true;
    [Tooltip("Where the player's feet end up.")]
    public Transform destination;

    XRSimpleInteractable interactable;
    bool releaseNextFrame;
    static bool loggedClimb;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
    }

    void OnEnable() => interactable.selectEntered.AddListener(OnSelected);
    void OnDisable() => interactable.selectEntered.RemoveListener(OnSelected);

    // The hands use toggle grab, so the ladder would stay "held". Let go of it straight away.
    void Update()
    {
        if (!releaseNextFrame) return;
        releaseNextFrame = false;
        if (interactable.isSelected && interactable.interactionManager != null)
            interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)interactable);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => loggedClimb = false;

    void OnSelected(SelectEnterEventArgs args)
    {
        releaseNextFrame = true;
        if (destination == null) return;
        if (goesDown == PlayerState.OnGround) return;   // already there

        var limit = FindFirstObjectByType<PlayAreaLimit>();
        var head = PlayerState.Head;
        if (limit == null || head == null) return;
        var rig = limit.transform;

        // Move the rig so the head (not the rig's centre) ends up over the destination.
        var offset = head.position - rig.position;
        offset.y = 0f;
        rig.position = destination.position - offset;
        Physics.SyncTransforms();

        PlayerState.OnGround = goesDown;
        limit.enabled = !goesDown;
        // The hut floor stays a teleport target only while the player is in the hut.
        var floor = FindFirstObjectByType<TeleportationArea>();
        if (floor != null) floor.enabled = !goesDown;

        if (goesDown)
        {
            NoticeBoard.Message("You are on the ground! Elephants near you will become agitated. Climb back up.");
            if (!loggedClimb)
            {
                loggedClimb = true;
                ActionLog.Record("Climbed down from the hut", false, "On the ground you cannot escape a charge, and your presence agitates elephants. Stay in the hut.");
            }
        }
    }
}
