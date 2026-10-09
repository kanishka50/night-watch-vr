using UnityEngine;

// An egret (kokka) in the flooded paddy by day, as in every Sri Lankan field: it stands, looks
// round, stabs at the water for a frog or fish, and wades a few slow steps. If the player or an
// elephant comes close it flies off early. At dusk the birds leave one after another, flapping up
// and away west over the tank to their roost, and are gone for the night.
// The bird is made of simple shapes (VillageLife); this moves its parts.
public class Egret : MonoBehaviour
{
    public Transform neck, leftLeg, rightLeg, leftWing, rightWing;
    [Tooltip("Leaves for the roost at this DayProgress (dusk begins at about 0.22).")]
    public float leaveAt = 0.2f;
    [Tooltip("Where the roost is (it flies toward it and disappears).")]
    public Vector3 roost = new Vector3(-90f, 25f, 110f);
    public float homeRadius = 2.5f;
    public float fleeDistance = 6f;

    [Header("Test (read only)")]
    public string status;

    enum State { Stand, Wade, Strike, Fly, Gone }
    State state;
    float stateEnd, flightTime;
    Vector3 home, target, velocity;
    Quaternion neckRest;

    void Start()
    {
        home = transform.position;
        if (neck != null) neckRest = neck.localRotation;
        Begin(State.Stand);
    }

    void Update()
    {
        var game = GameManager.Instance;
        float p = game != null ? game.DayProgress : 0f;
        if (state == State.Gone)
        {
            // Back in the morning (a new game starts in the afternoon too).
            if (p < leaveAt - 0.01f || p > 0.985f) Return();
            return;
        }
        if (state != State.Fly)
        {
            if (p >= leaveAt || Threatened()) TakeOff();
        }

        switch (state)
        {
            case State.Stand:
            case State.Strike:
                if (Time.time > stateEnd) Begin(Random.value < 0.35f ? State.Wade : Random.value < 0.5f ? State.Strike : State.Stand);
                break;
            case State.Wade:
                Wade();
                break;
            case State.Fly:
                Fly();
                break;
        }
        Pose();
        status = state.ToString();
    }

    bool Threatened()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            var d = cam.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude < fleeDistance) return true;
        }
        var elephant = ElephantAI.NearestActive(transform.position);
        return elephant != null && Vector3.Distance(elephant.transform.position, transform.position) < 20f;
    }

    void Begin(State next)
    {
        state = next;
        switch (next)
        {
            case State.Stand: stateEnd = Time.time + Random.Range(3f, 9f); break;
            case State.Strike: stateEnd = Time.time + 0.9f; break;
            case State.Wade:
                var r = Random.insideUnitCircle * homeRadius;
                target = home + new Vector3(r.x, 0f, r.y);
                stateEnd = Time.time + 8f;
                break;
        }
    }

    void Wade()
    {
        var to = target - transform.position;
        to.y = 0f;
        if (to.magnitude < 0.05f || Time.time > stateEnd) { Begin(State.Stand); return; }
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 90f * Time.deltaTime);
        transform.position += Vector3.ClampMagnitude(to, 0.18f * Time.deltaTime);
    }

    void TakeOff()
    {
        state = State.Fly;
        flightTime = 0f;
        var away = roost - transform.position;
        away.y = 0f;
        velocity = away.normalized * 1.5f + Vector3.up * 2f;
    }

    void Fly()
    {
        flightTime += Time.deltaTime;
        var to = (roost - transform.position).normalized;
        // Up steeply first, then a steady glide-and-flap toward the roost.
        var want = Vector3.Lerp(Vector3.up * 2f + to * 2f, to * 7f, Mathf.Clamp01(flightTime / 4f));
        velocity = Vector3.MoveTowards(velocity, want, 3f * Time.deltaTime);
        transform.position += velocity * Time.deltaTime;
        var flat = new Vector3(velocity.x, velocity.y * 0.3f, velocity.z);
        if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
        if (flightTime > 30f || (roost - transform.position).sqrMagnitude < 25f) Hide();
    }

    void Hide()
    {
        state = State.Gone;
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    void Return()
    {
        transform.SetPositionAndRotation(home, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
        Begin(State.Stand);
    }

    // Neck, legs and wings for each state.
    void Pose()
    {
        float t = Time.time;
        bool flying = state == State.Fly;
        if (neck != null)
        {
            float tilt = state == State.Strike ? Mathf.Sin(Mathf.Clamp01(1f - (stateEnd - t) / 0.9f) * Mathf.PI) * 55f
                       : flying ? -25f
                       : Mathf.Sin(t * 0.7f + home.x) * 6f;
            float look = flying ? 0f : Mathf.Sin(t * 0.31f + home.z) * 25f;
            neck.localRotation = neckRest * Quaternion.Euler(tilt, look, 0f);
        }
        float step = state == State.Wade ? Mathf.Sin(t * 4f) * 25f : 0f;
        if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(flying ? 70f : step, 0f, 0f);
        if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(flying ? 70f : -step, 0f, 0f);
        // Wings: folded along the body when standing, beating slowly in flight (egrets flap deep and slow).
        float open = flying ? 1f : 0.08f;
        float beat = flying ? Mathf.Sin(t * 2f * Mathf.PI * 2.6f) * 38f + 5f : 0f;
        if (leftWing != null)
        {
            leftWing.localScale = new Vector3(open, 1f, 1f);
            leftWing.localRotation = Quaternion.Euler(0f, 0f, beat);
        }
        if (rightWing != null)
        {
            rightWing.localScale = new Vector3(open, 1f, 1f);
            rightWing.localRotation = Quaternion.Euler(0f, 0f, -beat);
        }
    }

    [ContextMenu("Test: fly off now")]
    void TestFly() => TakeOff();
}
