using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Keeps the player's head inside a box (the hut floor) by sliding the XR Origin back.
// The XR Origin's body jumps to the head position every frame instead of moving into it,
// so walls cannot stop someone walking off the hut in the simulator. This script can.
// It also moves the player between the hut and the ground (ladder, start, debrief):
// in the hut the box is on and only the hut floor is a teleport target; on the ground the box
// is off and the whole ground is a teleport target.
public class PlayAreaLimit : MonoBehaviour
{
    public static PlayAreaLimit Instance { get; private set; }

    [Tooltip("The VR camera (the player's head).")]
    public Transform head;

    [Tooltip("Middle of the allowed area, in world space. Only X and Z limit the player; Y only places the yellow box.")]
    public Vector3 areaCenter = Vector3.zero;

    [Tooltip("Width (X) and depth (Z) of the allowed area in metres.")]
    public Vector2 areaSize = new Vector2(2.8f, 2.8f);

    [Header("Teleport targets")]
    public TeleportationArea hutFloor;
    public TeleportationArea groundArea;

    // Awake runs even while this component is switched off (player on the ground).
    void Awake() => Instance = this;

    // LateUpdate runs after the head and body have moved this frame.
    void LateUpdate()
    {
        if (head == null) return;

        Vector3 offset = head.position - areaCenter;
        float halfX = areaSize.x / 2f;
        float halfZ = areaSize.y / 2f;
        float pushX = Mathf.Clamp(offset.x, -halfX, halfX) - offset.x;
        float pushZ = Mathf.Clamp(offset.z, -halfZ, halfZ) - offset.z;

        if (pushX != 0f || pushZ != 0f)
        {
            transform.position += new Vector3(pushX, 0f, pushZ);
            // The Character Controller on the rig must see the new position before it moves again.
            Physics.SyncTransforms();
        }
    }

    // True while a move is fading out; a second move then is ignored (a double press on the ladder).
    public bool IsMoving { get; private set; }

    // Moves the rig so the head (not the rig's centre) ends up over the point, feet at its height,
    // hidden by a short fade to black and back (ScreenFeedback.Blink). onGround, if given, switches
    // between the hut and the ground at the moment of the jump. fadeFirst = false jumps at once
    // under an instant black (the very first placement).
    public void MoveHeadTo(Vector3 point, Quaternion? rotation = null, bool? onGround = null, bool fadeFirst = true)
    {
        if (IsMoving) return;
        if (!fadeFirst || ScreenFeedback.Instance == null)
        {
            Jump(point, rotation, onGround);
            ScreenFeedback.BlinkNow();
            return;
        }
        StartCoroutine(FadeAndJump(point, rotation, onGround));
    }

    System.Collections.IEnumerator FadeAndJump(Vector3 point, Quaternion? rotation, bool? onGround)
    {
        IsMoving = true;
        ScreenFeedback.BlinkOut();
        yield return new WaitForSecondsRealtime(ScreenFeedback.BlinkOutSeconds);
        Jump(point, rotation, onGround);
        IsMoving = false;
        ScreenFeedback.BlinkIn();
    }

    void Jump(Vector3 point, Quaternion? rotation, bool? onGround)
    {
        if (rotation.HasValue) transform.rotation = rotation.Value;
        var offset = head != null ? head.position - transform.position : Vector3.zero;
        offset.y = 0f;
        transform.position = point - offset;
        Physics.SyncTransforms();
        if (onGround.HasValue) SetOnGround(onGround.Value);
    }

    public void SetOnGround(bool onGround)
    {
        PlayerState.OnGround = onGround;
        enabled = !onGround;
        if (hutFloor != null) hutFloor.enabled = !onGround;
        if (groundArea != null) groundArea.enabled = onGround;
    }

    // Draws the allowed area as a yellow box in the Scene view when the rig is selected.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(areaCenter, new Vector3(areaSize.x, 2f, areaSize.y));
    }
}
