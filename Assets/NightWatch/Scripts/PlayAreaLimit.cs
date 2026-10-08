using UnityEngine;

// Keeps the player's head inside a box (the hut floor) by sliding the XR Origin back.
// The XR Origin's body jumps to the head position every frame instead of moving into it,
// so walls cannot stop someone walking off the hut in the simulator. This script can.
// Turn it off when the player is allowed to leave the hut (for example, climbing down).
public class PlayAreaLimit : MonoBehaviour
{
    [Tooltip("The VR camera (the player's head).")]
    public Transform head;

    [Tooltip("Middle of the allowed area, in world space. Only X and Z limit the player; Y only places the yellow box.")]
    public Vector3 areaCenter = Vector3.zero;

    [Tooltip("Width (X) and depth (Z) of the allowed area in metres.")]
    public Vector2 areaSize = new Vector2(2.8f, 2.8f);

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

    // Draws the allowed area as a yellow box in the Scene view when the rig is selected.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(areaCenter, new Vector3(areaSize.x, 2f, areaSize.y));
    }
}
