using UnityEngine;

// Small shared facts about the player that many scripts need.
public static class PlayerState
{
    // True after the player climbs down the ladder. Elephants near a player on the ground get agitated.
    public static bool OnGround;

    // The player's head (the VR camera).
    public static Transform Head => Camera.main != null ? Camera.main.transform : null;

    // Distance on the ground (ignoring height) from a point to the player.
    public static float FlatDistanceTo(Vector3 point)
    {
        var head = Head;
        if (head == null) return float.MaxValue;
        var a = head.position;
        a.y = 0f;
        point.y = 0f;
        return Vector3.Distance(a, point);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => OnGround = false;
}
