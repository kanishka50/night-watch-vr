using System.Collections;
using UnityEngine;

// The neighbour's house on the village side, and the neighbour.
// Real practice: elephants raid houses for stored rice, and many people are hurt when they step
// outside at night not knowing an elephant is there. Warning neighbours early is what keeps them safe.
//
//   Asleep  --phone call--> Warned: the light comes on, they stay inside and bang pots when an
//                           elephant comes near (a scare on every elephant near the house)
//   Asleep  --the village-side bull comes--> it goes for the rice store at the house.
//           When it is near, the neighbour steps outside with a lamp to see what the noise is.
//   Outside --the player drives the bull away--> GoingIn > Safe (they walk back inside)
//   Outside --the bull reaches them--> Hurt (they fall; the night goes on; the ending says so)
public class NeighbourHouse : MonoBehaviour
{
    public enum Mood { Asleep, Warned, Outside, GoingIn, Safe, Hurt }

    public static NeighbourHouse Instance { get; private set; }

    [Header("Parts")]
    [Tooltip("The door's hinge. It turns to open.")]
    public Transform door;
    public Renderer window;
    public Light windowLight;
    [Tooltip("The neighbour, feet at the pivot. Hidden while inside.")]
    public Transform figure;
    public Transform lampPivot;
    public Light lamp;

    [Header("Places")]
    public Transform doorPoint;
    public Transform yardPoint;
    [Tooltip("The rice store by the house, where an elephant goes when no one is warned.")]
    public Transform storePoint;

    [Header("Numbers")]
    [Tooltip("The bull this close to the house wakes the unwarned neighbour, who comes outside.")]
    public float comeOutDistance = 18f;
    [Tooltip("Seconds the bull searches the store before it turns to the neighbour.")]
    public float noticeSeconds = 6f;
    [Tooltip("The bull this close to the neighbour knocks them down.")]
    public float hurtDistance = 3f;
    [Tooltip("The neighbour goes back inside when the bull is this far away for safeSeconds.")]
    public float safeDistance = 15f;
    public float safeSeconds = 4f;
    [Tooltip("Banging pots (after a warning): range, strength and seconds between bangs.")]
    public float potsRange = 14f;
    public float potsStrength = 6f;
    public float potsInterval = 3f;
    public float walkSpeed = 1f;
    [Tooltip("After being knocked down: seconds on the ground, then a slow limp back inside.")]
    public float hurtLieSeconds = 6f;
    public float limpSpeed = 0.45f;
    [Tooltip("How far the door turns to open (inward, into the house).")]
    public float doorOpenAngle = 100f;
    public Color windowOnColor = new Color(1f, 0.75f, 0.40f);

    public Mood State { get; private set; } = Mood.Asleep;

    ElephantAI bull;
    float atStoreTime = -1f;
    bool bullTurned;
    float farTime;
    float outsideSince;
    float potsTimer;
    bool walking;
    float lampIntensity;
    MaterialPropertyBlock windowBlock;

    Vector3 HousePoint => doorPoint != null ? doorPoint.position : transform.position;

    void Awake()
    {
        Instance = this;
        if (figure != null) figure.gameObject.SetActive(false);
        if (lamp != null) lampIntensity = lamp.intensity;
        SetWindow(false);
    }

    // ---------------- Called by other scripts ----------------

    // The phone in the hut.
    public void Call()
    {
        SoundBank.PlayAt(SoundBank.Ring, PlayerState.Head != null ? PlayerState.Head.position : transform.position, 0.5f, 1f);
        switch (State)
        {
            case Mood.Asleep:
                State = Mood.Warned;
                SetWindow(true);
                NoticeBoard.Message("Neighbour on the phone: \"Thank you! We will stay inside, and bang pots if an elephant comes near the house.\"");
                ActionLog.Record("Warned your neighbour", true,
                    "Neighbours who know an elephant is near stay inside, and their noise helps drive it away.");
                break;
            case Mood.Warned:
                NoticeBoard.Message("Your neighbour already knows. They are staying inside.");
                break;
            case Mood.Outside:
                NoticeBoard.Message("Your neighbour is already outside! Drive the bull away from the house.");
                break;
            default:
                NoticeBoard.Message("Your neighbour is inside now.");
                break;
        }
    }

    // The arrival manager sends the bull from the village side.
    public void VillageElephantCame(ElephantAI elephant)
    {
        bull = elephant;
        atStoreTime = -1f;
        bullTurned = false;
        if (State != Mood.Asleep) return;

        ActionLog.Record("Did not warn your neighbour", false,
            "Elephants raid houses for stored rice. A neighbour who does not know may step outside in the dark.");
        if (storePoint != null) bull.GoTo(storePoint.position);
    }

    // ---------------- Every frame ----------------

    void Update()
    {
        switch (State)
        {
            case Mood.Warned: UpdateWarned(); break;
            case Mood.Asleep: UpdateAsleep(); break;
            case Mood.Outside: UpdateOutside(); break;
        }
    }

    void UpdateWarned()
    {
        potsTimer -= Time.deltaTime;
        if (potsTimer > 0f) return;
        potsTimer = potsInterval;
        int reached = ElephantAI.ScareAllNear(HousePoint, potsRange, "pots and pans", potsStrength);
        if (reached > 0) SoundBank.PlayAt(SoundBank.Pots, HousePoint + Vector3.up, 1f, 6f);
    }

    void UpdateAsleep()
    {
        if (!BullIsHere()) return;
        if (Flat(bull.transform.position - HousePoint) < comeOutDistance) StartCoroutine(ComeOutside());
    }

    void UpdateOutside()
    {
        SwingLamp(walking ? 1f : 0.3f);
        // Turned back, or made to run by the player: the neighbour is safe and goes back in.
        if (!BullIsHere() || bull.LastFleeTime > outsideSince)
        {
            Switch(GoBackInside());
            return;
        }

        float toNeighbour = Flat(bull.transform.position - figure.position);
        if (toNeighbour < hurtDistance)
        {
            Switch(Fall());
            return;
        }

        // At the store it searches for rice, then it notices the person with the lamp.
        if (storePoint != null && atStoreTime < 0f && Flat(bull.transform.position - storePoint.position) < 3f)
            atStoreTime = Time.time;
        if (!bullTurned && atStoreTime >= 0f && Time.time - atStoreTime > noticeSeconds)
        {
            bullTurned = true;
            bull.GoTo(figure.position);
            NoticeBoard.Message("The bull has seen your neighbour! Drive it away now!");
        }

        // Driven far enough away for a while: the neighbour is safe and goes back in.
        farTime = toNeighbour > safeDistance ? farTime + Time.deltaTime : 0f;
        if (farTime > safeSeconds) Switch(GoBackInside());
    }

    // Stops what the neighbour was doing (for example walking out) and starts the next thing.
    void Switch(IEnumerator next)
    {
        StopAllCoroutines();
        walking = false;
        StartCoroutine(next);
    }

    bool BullIsHere() => bull != null && bull.gameObject.activeInHierarchy && bull.IsActive;

    // ---------------- Animations ----------------

    IEnumerator ComeOutside()
    {
        State = Mood.Outside;
        farTime = 0f;
        outsideSince = Time.time;
        SetWindow(true);
        yield return SwingDoor(true);
        figure.position = doorPoint.position;
        figure.gameObject.SetActive(true);
        if (lamp != null) lamp.enabled = true;
        NoticeBoard.Message("Your neighbour came outside with a lamp! A bull is near their house. Drive it away!");
        yield return Walk(yardPoint.position);
    }

    IEnumerator GoBackInside()
    {
        State = Mood.GoingIn;
        if (bull != null)
        {
            bull.ClearDetour();
            ActionLog.Record("Saved your neighbour", true,
                "You drove the bull away from the house from the safety of the hut.");
            NoticeBoard.Message("The bull has moved away. Your neighbour went back inside, safe.");
        }
        yield return Walk(doorPoint.position);
        figure.gameObject.SetActive(false);
        yield return SwingDoor(false);
        State = Mood.Safe;
    }

    IEnumerator Fall()
    {
        State = Mood.Hurt;
        if (bull != null) bull.ClearDetour();
        GameManager.Instance?.NeighbourHurt();
        ActionLog.Record("Your neighbour was hurt", false,
            "The bull reached your neighbour before you drove it away. Warn people early, and act fast when someone is in danger.");
        SoundBank.PlayAt(SoundBank.Trumpet, figure.position, 1f, 6f);

        // The neighbour falls backwards and the lamp drops to the ground.
        var start = figure.rotation;
        var down = start * Quaternion.Euler(-85f, 0f, 0f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            figure.rotation = Quaternion.Slerp(start, down, Mathf.SmoothStep(0f, 1f, t / 0.6f));
            yield return null;
        }
        figure.rotation = down;
        if (lampPivot != null)
        {
            lampPivot.SetParent(transform, true);
            var p = lampPivot.position;
            lampPivot.position = new Vector3(p.x, 0.1f, p.z);
            lampPivot.rotation = Quaternion.Euler(0f, 0f, 80f);
        }
        if (lamp != null) lamp.intensity = lampIntensity * 0.4f;

        // Hurt, not killed: after a while they get up slowly and limp back inside.
        yield return new WaitForSeconds(hurtLieSeconds);
        NoticeBoard.Message("Your neighbour is hurt, but alive. They are limping back inside.");
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            figure.rotation = Quaternion.Slerp(down, start, Mathf.SmoothStep(0f, 1f, t / 1.5f));
            yield return null;
        }
        figure.rotation = start;
        yield return Walk(doorPoint.position, limpSpeed, true);
        figure.gameObject.SetActive(false);
        yield return SwingDoor(false);
    }

    IEnumerator Walk(Vector3 target) => Walk(target, walkSpeed, false);

    IEnumerator Walk(Vector3 target, float speed, bool limp)
    {
        walking = true;
        target.y = figure.position.y;
        var dir = target - figure.position;
        if (dir.sqrMagnitude > 0.01f) figure.rotation = Quaternion.LookRotation(dir);
        float baseY = figure.position.y;
        float t = 0f;
        while (Flat(target - figure.position) > 0.05f)
        {
            t += Time.deltaTime;
            // A limp: slower, with a deep dip on every other step.
            float step = limp ? speed * (0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t * 3f))) : speed;
            var next = Vector3.MoveTowards(figure.position, target, step * Time.deltaTime);
            float bob = limp ? 0.1f * Mathf.Abs(Mathf.Sin(t * 3f)) : 0.04f * Mathf.Abs(Mathf.Sin(t * 7f));
            next.y = baseY - (limp ? bob : -bob);   // a small bob with each step
            figure.position = next;
            yield return null;
        }
        figure.position = new Vector3(target.x, baseY, target.z);
        walking = false;
        // Face the field, where the noise came from.
        if (State == Mood.Outside && bull != null)
        {
            var look = bull.transform.position - figure.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) figure.rotation = Quaternion.LookRotation(look);
        }
    }

    IEnumerator SwingDoor(bool open)
    {
        if (door == null) yield break;
        var from = door.localRotation;
        var to = Quaternion.Euler(0f, open ? doorOpenAngle : 0f, 0f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            door.localRotation = Quaternion.Slerp(from, to, t / 0.6f);
            yield return null;
        }
        door.localRotation = to;
    }

    void SwingLamp(float amount)
    {
        if (lampPivot == null || lampPivot.parent != figure) return;
        lampPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 5f) * 12f * amount);
        if (lamp != null) lamp.intensity = lampIntensity * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, 3f));
    }

    void SetWindow(bool on)
    {
        if (windowLight != null) windowLight.enabled = on;
        if (window == null) return;
        if (windowBlock == null) windowBlock = new MaterialPropertyBlock();
        windowBlock.SetColor("_BaseColor", on ? windowOnColor : new Color(0.08f, 0.07f, 0.06f));
        windowBlock.SetColor("_EmissionColor", on ? windowOnColor * 2f : Color.black);
        window.SetPropertyBlock(windowBlock);
    }

    static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }

    // ---------------- Test buttons ----------------

    [ContextMenu("Test: call (warn) the neighbour")]
    void TestCall() => Call();

    [ContextMenu("Test: send the bull from the village side")]
    void TestBull()
    {
        var arrivals = FindFirstObjectByType<ArrivalManager>();
        if (arrivals != null) arrivals.SpawnVillageBull();
    }

    [ContextMenu("Test: the neighbour comes outside now")]
    void TestComeOut()
    {
        if (State == Mood.Asleep || State == Mood.Warned) StartCoroutine(ComeOutside());
    }
}
