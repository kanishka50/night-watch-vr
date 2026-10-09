using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

// Fades the view to black and back around a teleport or a snap turn, so the jump is never seen
// (course week 5: comfort). Each provider has a short delay before it moves (set by
// ComfortSetup): the view fades out during the delay, the jump happens in the dark, then it fades
// back in. A snap turn only dims the view part way, a teleport goes fully black.
public class ComfortFade : MonoBehaviour
{
    public TeleportationProvider teleport;
    public SnapTurnProvider snapTurn;
    [Range(0f, 1f)] public float snapTurnDarkness = 0.6f;

    void OnEnable()
    {
        if (teleport != null) teleport.locomotionStateChanged += OnTeleport;
        if (snapTurn != null) snapTurn.locomotionStateChanged += OnTurn;
    }

    void OnDisable()
    {
        if (teleport != null) teleport.locomotionStateChanged -= OnTeleport;
        if (snapTurn != null) snapTurn.locomotionStateChanged -= OnTurn;
    }

    void OnTeleport(LocomotionProvider provider, LocomotionState state)
    {
        if (state == LocomotionState.Preparing) ScreenFeedback.BlinkOut(Mathf.Max(0.05f, teleport.delayTime));
        else if (state == LocomotionState.Ended) ScreenFeedback.BlinkIn();
    }

    void OnTurn(LocomotionProvider provider, LocomotionState state)
    {
        if (state == LocomotionState.Preparing) ScreenFeedback.BlinkTo(snapTurnDarkness, Mathf.Max(0.05f, snapTurn.delayTime));
        else if (state == LocomotionState.Ended) ScreenFeedback.BlinkIn(0.2f);
    }
}
