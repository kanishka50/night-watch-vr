using UnityEngine;

// Turns a floating label round its upright axis to face the player, so it never reads mirrored
// from behind (text is seen from both sides; "Climb up" read backwards from under the hut).
public class FaceViewer : MonoBehaviour
{
    void LateUpdate() => Face(transform);

    // Text reads correctly when its forward points away from the viewer.
    public static void Face(Transform label)
    {
        var head = PlayerState.Head;
        if (head == null) return;
        var away = label.position - head.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.0001f) label.rotation = Quaternion.LookRotation(away);
    }
}
