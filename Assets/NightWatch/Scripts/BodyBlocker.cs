using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Stops the player walking through solid things: houses, hut posts and rails, the table, the
// ladder, bonfires, haystacks, trees, fences, the tank and elephants.
// Walking (WASD in the simulator, real steps in a headset) moves the head, not the rig, so the
// rig's Character Controller never stops it. Each frame this script sweeps the player's body (an
// upright capsule from knee height to the head, 0.2 m round) from where the head was to where it
// is now. If it hits something, the rig is slid back so the head stops at the wall and moves on
// along it, the same way PlayAreaLimit keeps the head over the hut floor.
// If the head still ends up inside something (an elephant walks into the player, or a teleport
// lands against a wall), the view goes black until the player steps out (ScreenFeedback.SetInside).
public class BodyBlocker : MonoBehaviour
{
    [Tooltip("The VR camera (the player's head).")]
    public Transform head;
    [Tooltip("Half the body's width, in metres.")]
    public float radius = 0.2f;
    [Tooltip("Things lower than this above the feet (steps, logs, the hut floor edge) do not block.")]
    public float kneeHeight = 0.45f;
    [Tooltip("The view goes black when this ball round the eyes touches a solid thing.")]
    public float eyeRadius = 0.12f;

    const float Skin = 0.02f;
    // A move bigger than this in one frame is a jump (teleport, ladder, story), not a step.
    const float JumpDistance = 0.5f;

    readonly RaycastHit[] hits = new RaycastHit[16];
    readonly Collider[] overlaps = new Collider[16];
    Vector3 lastHead, lastRig;
    bool hasLast;
    int mask;

    void Awake()
    {
        // Every layer, with Ignore Raycast (2): the tank wall is on it, so pointers pass over it
        // but the body does not. The learn spots there are triggers, which never block.
        mask = Physics.DefaultRaycastLayers | (1 << 2);
        if (head == null && Camera.main != null) head = Camera.main.transform;
    }

    void Start()
    {
        // Held tools must not bump into the player's own body and shake in the hand.
        var body = GetComponent<CharacterController>();
        if (body == null) return;
        foreach (var grab in FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var c in grab.GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(body, c);
    }

    void OnDisable()
    {
        hasLast = false;
        ScreenFeedback.SetInside(false);
    }

    void LateUpdate()
    {
        if (head == null) return;

        var limit = PlayAreaLimit.Instance;
        bool busy = (limit != null && limit.IsMoving) || ScreenFeedback.IsFalling;
        // Small rig moves (PlayAreaLimit, a snap turn round the head) are swept like steps.
        bool jumped = !hasLast
            || Flat(transform.position - lastRig).sqrMagnitude > JumpDistance * JumpDistance
            || Flat(head.position - lastHead).sqrMagnitude > JumpDistance * JumpDistance;

        if (!busy && !jumped) Block();

        ScreenFeedback.SetInside(!busy && HeadInside());
        lastHead = head.position;
        lastRig = transform.position;
        hasLast = true;
    }

    // Moves the rig back so the body stops at the first solid thing on the way, then slides along it.
    void Block()
    {
        var move = Flat(head.position - lastHead);
        if (move.sqrMagnitude < 1e-8f) return;

        var allowed = Sweep(lastHead, move, out var normal);
        if (normal.HasValue)
        {
            // Slide: the rest of the step, along the wall.
            var rest = Flat(Vector3.ProjectOnPlane(move - allowed, normal.Value));
            if (rest.sqrMagnitude > 1e-8f) allowed += Sweep(lastHead + allowed, rest, out _);
        }

        var push = Flat(lastHead + allowed - head.position);
        if (push.sqrMagnitude < 1e-8f) return;
        transform.position += push;
        // The Character Controller on the rig must see the new position before it moves again.
        Physics.SyncTransforms();
    }

    // How far the body can go from 'from' along 'move' (flat). normal = the wall it hit, if any.
    Vector3 Sweep(Vector3 from, Vector3 move, out Vector3? normal)
    {
        normal = null;
        float distance = move.magnitude;
        var direction = move / distance;
        float feet = transform.position.y;
        var low = new Vector3(from.x, feet + kneeHeight + radius, from.z);
        var high = new Vector3(from.x, Mathf.Max(low.y, head.position.y), from.z);

        int count = Physics.CapsuleCastNonAlloc(low, high, radius, direction, hits, distance + Skin, mask, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            // distance 0: the body already overlaps it (it can always walk out again).
            if (hit.distance <= 0f || !Blocks(hit.collider)) continue;
            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                normal = hit.normal;
            }
        }
        if (!normal.HasValue) return move;
        return direction * Mathf.Max(0f, nearest - Skin);
    }

    bool HeadInside()
    {
        int count = Physics.OverlapSphereNonAlloc(head.position, eyeRadius, overlaps, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (Blocks(overlaps[i])) return true;
        return false;
    }

    // Solid things only: not the ground (the Character Controller stands on it), not the player's
    // own rig, not tools (they are picked up and carried).
    bool Blocks(Collider c)
    {
        if (c is TerrainCollider) return false;
        if (c.transform.IsChildOf(transform)) return false;
        var body = c.attachedRigidbody;
        if (body != null && body.GetComponent<XRGrabInteractable>() != null) return false;
        return true;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
