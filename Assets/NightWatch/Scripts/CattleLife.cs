using UnityEngine;

// A village cow or bull tied to a post beside the kamatha, going about its day:
//  - grazes most of the time (head down, chewing), stands and looks round, sniffs the ground,
//    and now and then walks slowly to a new spot, never further than its rope allows;
//  - the rope is drawn from the post to its neck and sags when slack;
//  - moos now and then by day (rarely at night; 3D sound, mid tones for laptop speakers);
//  - at night it mostly stands still and dozes (the model has no lying-down clip);
//  - when a wild elephant comes nearer than alarmDistance, it lifts its head, faces it, moos and
//    steps away to the far side of its rope circle, and stays restless until the elephant leaves.
// States are the Animator's: Eating, Idle, Idle_2, Idle_Headlow, Walk, Flinch (CattleSetup).
public class CattleLife : MonoBehaviour
{
    [Tooltip("The tethering post (world point at its foot).")]
    public Vector3 post;
    [Tooltip("How far the body's centre may be from the post (rope length minus the neck).")]
    public float reach = 2f;
    [Tooltip("Places the animal will not walk into (kamatha floor, haystacks): x, z, radius.")]
    public Vector3[] keepOut;
    public float walkSpeed = 0.55f;
    public float turnSpeed = 70f;
    public float alarmDistance = 15f;
    [Tooltip("Logic and rope stop past this distance from the camera (the Animator culls itself).")]
    public float activeDistance = 70f;

    public Animator animator;
    public AudioSource voice;
    public Transform neck;
    public LineRenderer rope;
    public Vector3 ropeTop;

    [Header("Test (read only)")]
    public string status;

    enum State { Graze, Stand, Sniff, Walk, Alarm }
    State state;
    float stateEnd, nextMoo;
    Vector3 target;
    bool fleeing;          // walking away from an elephant
    float walkFrom;        // the walk starts at this time (after a startled flinch)
    bool walking;
    Terrain ground;

    void Start()
    {
        ground = Terrain.activeTerrain;
        // Two animals never move in step.
        if (animator != null) animator.speed = Random.Range(0.9f, 1.08f);
        nextMoo = Time.time + Random.Range(15f, 50f);
        Begin(Random.value < 0.6f ? State.Graze : State.Stand);
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > activeDistance * activeDistance) return;

        var elephant = ElephantAI.NearestActive(transform.position);
        float elephantDistance = elephant != null ? Vector3.Distance(elephant.transform.position, transform.position) : float.MaxValue;
        if (elephantDistance < alarmDistance && state != State.Alarm && !fleeing)
            Alarm(elephant.transform.position);

        switch (state)
        {
            case State.Walk:
                WalkStep();
                break;
            case State.Alarm:
                // Keep facing the elephant while it is near.
                if (elephant != null) TurnToward(elephant.transform.position);
                if (Time.time > stateEnd)
                {
                    if (elephantDistance < alarmDistance) Alarm(elephant.transform.position);
                    else Begin(State.Stand);
                }
                break;
            default:
                if (Time.time > stateEnd) Begin(Next());
                break;
        }

        if (Time.time > nextMoo)
        {
            if (state != State.Alarm && !(Night && Random.value < 0.75f)) Moo();
            nextMoo = Time.time + (Night ? Random.Range(90f, 180f) : Random.Range(35f, 80f));
        }
        status = $"{state}, {(elephant != null ? $"elephant {elephantDistance:F0} m" : "no elephant")}, {(Night ? "night" : "day")}";
    }

    void LateUpdate()
    {
        if (rope == null || neck == null) return;
        // A rope with a little sag: more when the animal is close to the post.
        var a = ropeTop;
        var b = neck.position;
        float slack = Mathf.Clamp01(1f - Vector3.Distance(a, b) / (reach + 0.6f));
        for (int i = 0; i < rope.positionCount; i++)
        {
            float t = i / (float)(rope.positionCount - 1);
            rope.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * (0.08f + 0.5f * slack)));
        }
    }

    static bool Night
    {
        get
        {
            // The time of day on screen: late dusk (0.27) until dawn (0.96).
            float p = DayNightCycle.ShownProgress;
            return p > 0.27f && p < 0.96f;
        }
    }

    // Day: mostly grazing. Night: mostly standing and dozing, little walking.
    State Next()
    {
        float r = Random.value;
        if (Night) return r < 0.7f ? State.Stand : r < 0.9f ? State.Sniff : State.Walk;
        return r < 0.5f ? State.Graze : r < 0.68f ? State.Stand : r < 0.8f ? State.Sniff : State.Walk;
    }

    void Begin(State next)
    {
        state = next;
        fleeing = false;
        switch (next)
        {
            case State.Graze:
                Play("Eating");
                stateEnd = Time.time + Random.Range(12f, 30f);
                break;
            case State.Stand:
                Play(Random.value < 0.5f ? "Idle" : "Idle_2");
                stateEnd = Time.time + (Night ? Random.Range(15f, 40f) : Random.Range(5f, 12f));
                break;
            case State.Sniff:
                Play("Idle_Headlow");
                stateEnd = Time.time + Random.Range(4f, 8f);
                break;
            case State.Walk:
                if (!PickSpot(out target, null)) { Begin(State.Stand); return; }
                walkFrom = Time.time;
                walking = false;
                stateEnd = Time.time + 20f;   // give up if stuck
                break;
        }
    }

    void Alarm(Vector3 elephant)
    {
        state = State.Alarm;
        Play("Flinch");
        Moo();
        // A startled flinch, then a few steps to the far side of the rope circle, away from the
        // elephant; then it stands watching.
        if (PickSpot(out var spot, elephant))
        {
            target = spot;
            state = State.Walk;
            fleeing = true;
            walkFrom = Time.time + 0.7f;
            walking = false;
            stateEnd = Time.time + 12f;
            return;
        }
        Play("Idle_2");
        stateEnd = Time.time + 6f;
    }

    void WalkStep()
    {
        if (Time.time < walkFrom) return;
        if (!walking)
        {
            Play("Walk");
            walking = true;
        }
        var to = target - transform.position;
        to.y = 0f;
        float turn = TurnToward(target);
        // Turn mostly on the spot first, then walk.
        if (to.magnitude > 0.15f && Time.time < stateEnd)
        {
            float speed = walkSpeed * Mathf.Clamp01(1f - Mathf.Abs(turn) / 60f);
            var step = Vector3.ClampMagnitude(to, speed * Time.deltaTime);
            var next = transform.position + step;
            next = Clamp(next);
            if (ground != null) next.y = ground.SampleHeight(next) + ground.transform.position.y;
            transform.position = next;
            return;
        }
        // Arrived (or stuck): after an alarm, stand and watch; otherwise graze or stand.
        if (fleeing)
        {
            fleeing = false;
            state = State.Alarm;
            Play("Idle_2");
            stateEnd = Time.time + 8f;
        }
        else Begin(Night ? State.Stand : State.Graze);
    }

    // Returns the turn still left (degrees).
    float TurnToward(Vector3 point)
    {
        var to = point - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return 0f;
        var want = Quaternion.LookRotation(to);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        return Quaternion.Angle(transform.rotation, want);
    }

    // A free spot inside the rope circle. away: if set, on the side away from that point.
    bool PickSpot(out Vector3 spot, Vector3? away)
    {
        for (int tries = 0; tries < 16; tries++)
        {
            Vector2 offset;
            if (away.HasValue)
            {
                var from = post - away.Value;
                from.y = 0f;
                var dir = new Vector2(from.x, from.z).normalized;
                offset = Quaternion.Euler(0f, 0f, Random.Range(-50f, 50f)) * dir * reach * Random.Range(0.75f, 1f);
            }
            else offset = Random.insideUnitCircle * reach;
            spot = post + new Vector3(offset.x, 0f, offset.y);
            if (Vector3.Distance(spot, transform.position) < (away.HasValue ? 0.4f : 0.8f)) continue;
            if (Blocked(spot)) continue;
            return true;
        }
        spot = transform.position;
        return false;
    }

    bool Blocked(Vector3 p)
    {
        if (keepOut == null) return false;
        foreach (var k in keepOut)
            if (new Vector2(p.x - k.x, p.z - k.y).sqrMagnitude < k.z * k.z) return true;
        return false;
    }

    // The rope holds the animal within reach of the post.
    Vector3 Clamp(Vector3 p)
    {
        var off = p - post;
        off.y = 0f;
        if (off.magnitude > reach) p = post + off.normalized * reach + Vector3.up * p.y;
        return p;
    }

    void Play(string clip)
    {
        if (animator != null && animator.runtimeAnimatorController != null) animator.CrossFadeInFixedTime(clip, 0.4f);
    }

    void Moo()
    {
        if (voice == null) return;
        voice.pitch = Random.Range(0.9f, 1.1f);
        voice.PlayOneShot(SoundBank.Moo, Random.Range(0.6f, 1f));
    }

    [ContextMenu("Test: moo")]
    void TestMoo() => Moo();

    [ContextMenu("Test: walk to a new spot")]
    void TestWalk() => Begin(State.Walk);

    [ContextMenu("Test: an elephant comes from the north")]
    void TestAlarm() => Alarm(transform.position + Vector3.forward * 12f);
}
