using System.Collections;
using UnityEngine;

// The neighbour's house on the village side, and the neighbour.
// Real practice: elephants raid houses for stored rice, and many people are hurt when they are
// outside after dark not knowing an elephant is near. Warning neighbours early keeps them safe,
// and villagers keep a fire burning outside the house at night to keep elephants away.
//
//   Inside   (afternoon) the house is dark
//   Outside  from dusk: the neighbour lights a small evening fire in the yard and walks about
//            with a lamp. The player can see them from the hut.
//   Phone call: they stop, lift the lamp toward the hut, build the yard fire up into a big fire
//            (it scares elephants near the house, like the player's bonfires), hang a bottle lamp
//            by the door and go inside; the window glows softly (Warned). The bull from the village
//            side then goes to the field.
//   No call: the bull from the village side goes for the rice at the house (Danger):
//            it sees the neighbour, rears and trumpets, then runs at them. The player can still
//            drive it away (Safe). If it reaches them, they are thrown down (Hurt), lie still for
//            a while, then get up and limp inside.
public class NeighbourHouse : MonoBehaviour
{
    public enum Mood { Inside, Outside, GoingIn, Warned, Danger, Safe, Hurt }

    public static NeighbourHouse Instance { get; private set; }

    [Header("Parts")]
    [Tooltip("The door's hinge. It turns to open.")]
    public Transform door;
    public Renderer window;
    [Tooltip("The small oil lamp (kuppi lampuwa) hung by the door when they are safe inside.")]
    public GameObject bottleLamp;
    [Tooltip("The small evening fire in the yard (flame, light and crackle).")]
    public GameObject yardFire;
    [Tooltip("The neighbour, feet at the pivot. Hidden while inside.")]
    public Transform figure;
    public Transform lampPivot;
    public Light lamp;
    [Tooltip("The farmer's animations (Idle, Walk, Run, Working, Death). Empty = the old block figure.")]
    public Animator body;

    [Header("Places")]
    public Transform doorPoint;
    public Transform firePoint;
    [Tooltip("The neighbour walks between these two points in the evening.")]
    public Transform yardPoint;
    public Transform patrolPoint;
    [Tooltip("The rice store by the house, where the bull goes when no one is warned.")]
    public Transform storePoint;

    [Header("Numbers")]
    [Tooltip("Seconds the bull searches the rice before it notices the neighbour.")]
    public float noticeSeconds = 6f;
    [Tooltip("The bull notices the neighbour at once when this close.")]
    public float noticeDistance = 6f;
    [Tooltip("Seconds the bull rears and trumpets before it runs at the neighbour.")]
    public float warnDisplaySeconds = 2f;
    public float chargeSpeed = 3.5f;
    [Tooltip("The bull's centre this close to the neighbour during its run knocks them down (the trunk is 2.4 m in front of it).")]
    public float hitDistance = 3.2f;
    public float walkSpeed = 1f;
    [Tooltip("After being knocked down: seconds on the ground, then a slow limp back inside.")]
    public float hurtLieSeconds = 6f;
    public float limpSpeed = 0.45f;
    [Tooltip("How far the door turns to open (inward, into the house).")]
    public float doorOpenAngle = 100f;
    public Color windowOnColor = new Color(1f, 0.75f, 0.40f);

    [Header("The big fire after a warning")]
    public float bigFireScale = 1.8f;
    public float bigFireRange = 10f;
    public float bigFireIntensity = 4f;
    [Tooltip("Like the player's bonfires: every few seconds, elephants this close stop and hesitate.")]
    public float fireScareRange = 8f;
    public float fireScareStrength = 5f;
    public float fireScareInterval = 2f;

    public Mood State { get; private set; } = Mood.Inside;

    ElephantAI bull;
    float bullCameTime = float.PositiveInfinity;
    float atStoreTime = -1f;
    bool walking;
    // How the lamp hangs, relative to the way the neighbour faces (a swing, a wave).
    Quaternion lampTilt = Quaternion.identity;
    float lampIntensity;
    MaterialPropertyBlock windowBlock;
    Light fireLight;
    bool bigFire;
    float fireScareTimer;

    void Awake()
    {
        Instance = this;
        if (figure != null) figure.gameObject.SetActive(false);
        if (yardFire != null)
        {
            // The crackle starts when the fire is lit (the sound is made in code, so set it here).
            var crackle = yardFire.GetComponent<AudioSource>();
            if (crackle != null) crackle.clip = SoundBank.FireCrackle;
            fireLight = yardFire.GetComponentInChildren<Light>(true);
            yardFire.SetActive(false);
        }
        if (lamp != null) lampIntensity = lamp.intensity;
        SetHomeLights(false);
    }

    // ---------------- Called by other scripts ----------------

    // The phone in the hut.
    public void Call()
    {
        SoundBank.PlayAt(SoundBank.Ring, PlayerState.Head != null ? PlayerState.Head.position : transform.position, 0.5f, 1f);
        switch (State)
        {
            case Mood.Inside:
                // Warned before they came out: they build the fire up from the doorway at dusk.
                State = Mood.Warned;
                SetHomeLights(true);
                Warned("Neighbour on the phone: \"Thank you! We will keep a big fire going and stay inside tonight.\"");
                break;
            case Mood.Outside:
                Switch(AnswerAndGoInside(false));
                break;
            case Mood.Danger:
                // A late call: they hurry inside.
                Switch(AnswerAndGoInside(true));
                break;
            case Mood.Hurt:
                NoticeBoard.Message("Your neighbour is hurt. They are going back inside.");
                break;
            default:
                NoticeBoard.Message("Your neighbour is already safe inside.");
                break;
        }
    }

    // The arrival manager sends the bull from the village side.
    public void VillageElephantCame(ElephantAI elephant)
    {
        bull = elephant;
        bullCameTime = Time.time;
        atStoreTime = -1f;
        if (State == Mood.Warned || State == Mood.Safe) return;   // the house is lit and shut

        ActionLog.Record("Did not warn your neighbour", false,
            "Call your neighbour when elephants are out.");
        if (storePoint != null) bull.GoTo(storePoint.position);
    }

    // ---------------- Every frame ----------------

    void Update()
    {
        var game = GameManager.Instance;
        UpdateBigFire(game);
        switch (State)
        {
            case Mood.Inside:
                if (game != null && game.CurrentPhase >= GameManager.Phase.Dusk && game.CurrentPhase <= GameManager.Phase.Night)
                    Switch(ComeOutForTheEvening());
                break;
            case Mood.Outside:
                SwingLamp(walking ? 1f : 0.3f);
                if (BullNoticesNeighbour()) Switch(Danger());
                break;
            case Mood.Danger:
                // Driven away (turned back, or ran from a cracker): the neighbour is safe.
                if (!BullIsHere() || bull.LastFleeTime > bullCameTime) Switch(Rescued());
                break;
        }
    }

    bool BullIsHere() => bull != null && bull.gameObject.activeInHierarchy && bull.IsActive;

    bool BullNoticesNeighbour()
    {
        if (!BullIsHere()) return false;
        if (Flat(bull.transform.position - figure.position) < noticeDistance) return true;
        if (storePoint != null && atStoreTime < 0f && Flat(bull.transform.position - storePoint.position) < 3f)
            atStoreTime = Time.time;
        return atStoreTime >= 0f && Time.time - atStoreTime > noticeSeconds;
    }

    // Stops what the neighbour was doing and starts the next thing.
    void Switch(IEnumerator next)
    {
        StopAllCoroutines();
        walking = false;
        StartCoroutine(next);
    }

    // ---------------- The story, step by step ----------------

    IEnumerator ComeOutForTheEvening()
    {
        State = Mood.Outside;
        yield return SwingDoor(true);
        figure.position = doorPoint.position;
        figure.rotation = Quaternion.identity;
        figure.gameObject.SetActive(true);
        if (lamp != null) lamp.enabled = true;

        // Light the evening fire, then walk about the yard.
        yield return Walk(firePoint.position, walkSpeed, false);
        Play("Working");
        yield return new WaitForSeconds(1.5f);
        if (yardFire != null) yardFire.SetActive(true);
        while (true)
        {
            yield return Walk(yardPoint.position, walkSpeed, false);
            yield return new WaitForSeconds(Random.Range(2f, 4f));
            yield return Walk(patrolPoint.position, walkSpeed, false);
            yield return new WaitForSeconds(Random.Range(2f, 4f));
        }
    }

    IEnumerator AnswerAndGoInside(bool hurry)
    {
        State = Mood.GoingIn;
        if (hurry)
        {
            NoticeBoard.Message("Neighbour on the phone: \"An elephant? We are going in now!\"");
            ActionLog.Record("Called your neighbour inside just in time", true,
                "A late warning still helps.");
        }
        else Warned("Neighbour on the phone: \"Thank you! We will build up the fire and go inside.\"");
        if (bull != null) bull.ClearDetour();

        // They stop, turn to the hut and lift the lamp: the call got through. Then they throw more
        // wood on the fire before going in.
        if (!hurry)
        {
            FaceTowards(ElephantAI.HutCenter);
            Play("Idle");
            yield return LiftLamp(1.5f);
            yield return Walk(firePoint.position, walkSpeed, false);
            Play("Working");
            yield return new WaitForSeconds(0.8f);
            yield return GrowFire();
        }
        yield return Walk(doorPoint.position, hurry ? walkSpeed * 2f : walkSpeed, false);
        yield return GoInAndLightUp();
        State = Mood.Warned;
    }

    IEnumerator Danger()
    {
        State = Mood.Danger;
        NoticeBoard.Message("The bull has seen your neighbour in the yard! Scare it away now!");
        FaceTowards(bull.transform.position);
        Play("Idle");
        // Frozen with fear; the old block figure holds its lamp up.
        if (LampInHand) lampTilt = body != null ? Quaternion.identity : Quaternion.Euler(-60f, 0f, 0f);

        // The bull rears, flaps its ears and trumpets: a warning before it runs.
        bull.Alarm(warnDisplaySeconds);
        yield return new WaitForSeconds(warnDisplaySeconds);
        if (!BullIsHere()) yield break;

        NoticeBoard.Message("The bull is charging your neighbour!");
        float retarget = 0f;
        while (BullIsHere())
        {
            // The neighbour backs away slowly, frozen with fear, facing the bull.
            var away = figure.position - bull.transform.position;
            away.y = 0f;
            FaceTowards(bull.transform.position);
            figure.position += away.normalized * 0.5f * Time.deltaTime;

            retarget -= Time.deltaTime;
            if (retarget <= 0f)
            {
                retarget = 0.3f;
                bull.GoTo(figure.position, chargeSpeed);
            }
            if (away.magnitude < hitDistance)
            {
                yield return Hit(away.normalized);
                yield break;
            }
            yield return null;
        }
    }

    IEnumerator Rescued()
    {
        State = Mood.GoingIn;
        if (bull != null) bull.ClearDetour();
        ActionLog.Record("Saved your neighbour", true,
            "You drove the bull away from the house.");
        NoticeBoard.Message("The bull has gone. Your neighbour is hurrying back inside, safe.");
        lampTilt = Quaternion.identity;
        yield return Walk(doorPoint.position, walkSpeed * 1.8f, false);
        yield return GoInAndLightUp();
        State = Mood.Safe;
    }

    // The bull reaches the neighbour: they are thrown back and fall, the lamp flies off.
    IEnumerator Hit(Vector3 pushDirection)
    {
        State = Mood.Hurt;
        GameManager.Instance?.NeighbourHurt();
        ActionLog.Record("Your neighbour was hurt", false,
            "Scare the bull away from the house sooner.");
        SoundBank.PlayAt(SoundBank.Bang, figure.position, 0.6f, 6f);
        if (bull != null)
        {
            bull.ClearDetour();
            bull.Alarm(2f);   // it stands over them, trumpeting, then goes on to the field
        }

        if (lampPivot != null)
        {
            lampPivot.SetParent(transform, true);
            var landing = figure.position + pushDirection * 2.5f + Vector3.Cross(Vector3.up, pushDirection) * 0.6f;
            lampPivot.position = new Vector3(landing.x, 0.1f, landing.z);
            lampPivot.rotation = Quaternion.Euler(0f, 0f, 80f);
        }
        if (lamp != null) lamp.intensity = lampIntensity * 0.4f;

        // Thrown back about 1.8 m with a small arc, falling onto their back.
        var startPos = figure.position;
        var upright = figure.rotation;
        var down = upright * Quaternion.Euler(-85f, 0f, 0f);
        bool animated = body != null;
        if (animated) Play("Death", 1.3f, 0.1f);
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            float k = t / 0.45f;
            figure.position = startPos + pushDirection * 1.8f * k + Vector3.up * 0.4f * Mathf.Sin(k * Mathf.PI);
            if (!animated) figure.rotation = Quaternion.Slerp(upright, down, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        figure.position = startPos + pushDirection * 1.8f;
        if (!animated) figure.rotation = down;

        // Hurt, not killed: after a while they get up slowly and limp back inside.
        yield return new WaitForSeconds(hurtLieSeconds);
        NoticeBoard.Message("Your neighbour is hurt, but alive. They are limping back inside.");
        if (animated)
        {
            // A slow blend from lying to standing reads as getting up.
            Play("Idle", 0.6f, 1.2f);
            yield return new WaitForSeconds(1.5f);
        }
        else
        {
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                figure.rotation = Quaternion.Slerp(down, upright, Mathf.SmoothStep(0f, 1f, t / 1.5f));
                yield return null;
            }
        }
        figure.rotation = upright;
        yield return Walk(doorPoint.position, limpSpeed, true);
        yield return GoInAndLightUp();
    }

    // ---------------- Small animations ----------------

    void Warned(string reply)
    {
        NoticeBoard.Message(reply);
        ActionLog.Record("Warned your neighbour", true,
            "They stayed inside, safe.");
    }

    IEnumerator GoInAndLightUp()
    {
        figure.gameObject.SetActive(false);
        yield return SwingDoor(false);
        SetHomeLights(true);
    }

    // More wood on the yard fire: it grows into a big fire over a second and a half.
    IEnumerator GrowFire()
    {
        if (yardFire == null || bigFire) yield break;
        bigFire = true;
        yardFire.SetActive(true);
        var scale = yardFire.transform.localScale;
        float range = fireLight != null ? fireLight.range : 0f;
        float intensity = fireLight != null ? fireLight.intensity : 0f;
        SoundBank.PlayAt(SoundBank.FireCrackle, yardFire.transform.position, 1f, 3f);
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.5f);
            yardFire.transform.localScale = scale * Mathf.Lerp(1f, bigFireScale, k);
            if (fireLight != null)
            {
                fireLight.range = Mathf.Lerp(range, bigFireRange, k);
                fireLight.intensity = Mathf.Lerp(intensity, bigFireIntensity, k);
            }
            yield return null;
        }
    }

    // A warned house keeps its big fire going: it flickers, and elephants near it hesitate.
    void UpdateBigFire(GameManager game)
    {
        // Warned before dusk: the fire is built up as soon as evening comes.
        if (State == Mood.Warned && !bigFire && game != null && game.CurrentPhase >= GameManager.Phase.Dusk)
            StartCoroutine(GrowFire());
        if (!bigFire || yardFire == null) return;

        if (fireLight != null)
            fireLight.intensity = bigFireIntensity * (0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 5f, 7f));
        fireScareTimer -= Time.deltaTime;
        if (fireScareTimer > 0f) return;
        fireScareTimer = fireScareInterval;
        ElephantAI.ScareAllNear(yardFire.transform.position, fireScareRange, "house fire", fireScareStrength);
    }

    IEnumerator LiftLamp(float seconds)
    {
        if (!LampInHand) yield break;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            // Waved toward the hut (the old block figure raises it high).
            lampTilt = body != null
                ? Quaternion.Euler(0f, 0f, Mathf.Sin(t * 8f) * 35f)
                : Quaternion.Euler(-100f, 0f, Mathf.Sin(t * 8f) * 20f);
            yield return null;
        }
        lampTilt = Quaternion.identity;
    }

    IEnumerator Walk(Vector3 target, float speed, bool limp)
    {
        walking = true;
        Play(limp || speed <= walkSpeed * 1.5f ? "Walk" : "Run", limp ? 0.6f : 1f);
        target.y = figure.position.y;
        FaceTowards(target);
        float baseY = figure.position.y;
        float t = 0f;
        while (Flat(target - figure.position) > 0.05f)
        {
            t += Time.deltaTime;
            // A limp: slower, with a deep dip on every other step.
            float step = limp ? speed * (0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t * 3f))) : speed;
            var next = Vector3.MoveTowards(figure.position, target, step * Time.deltaTime);
            // The animated farmer bobs by itself; the limp still dips a little.
            float bob = limp ? -(body != null ? 0.04f : 0.1f) * Mathf.Abs(Mathf.Sin(t * 3f))
                : body != null ? 0f : 0.04f * Mathf.Abs(Mathf.Sin(t * 7f));
            next.y = baseY + bob;
            figure.position = next;
            if (!limp) SwingLamp(1f);
            yield return null;
        }
        figure.position = new Vector3(target.x, baseY, target.z);
        Play("Idle");
        walking = false;
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

    void FaceTowards(Vector3 point)
    {
        var look = point - figure.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) figure.rotation = Quaternion.LookRotation(look);
    }

    // The lamp is in the hand while it sits anywhere inside the figure (it flies off in a fall).
    bool LampInHand => lampPivot != null && figure != null && lampPivot.IsChildOf(figure);

    void Play(string state, float speed = 1f, float blend = 0.2f)
    {
        if (body == null || !body.isActiveAndEnabled) return;
        body.speed = speed;
        body.CrossFadeInFixedTime(state, blend);
    }

    // The lamp hangs upright from the hand, after the animation has moved the hand.
    void LateUpdate()
    {
        if (LampInHand && figure.gameObject.activeInHierarchy) lampPivot.rotation = figure.rotation * lampTilt;
    }

    void SwingLamp(float amount)
    {
        if (!LampInHand) return;
        lampTilt = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 5f) * 12f * amount);
        if (lamp != null) lamp.intensity = lampIntensity * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, 3f));
    }

    // Someone is home and safe: the window glows softly and the bottle lamp burns by the door.
    void SetHomeLights(bool on)
    {
        if (bottleLamp != null) bottleLamp.SetActive(on);
        if (window == null) return;
        if (windowBlock == null) windowBlock = new MaterialPropertyBlock();
        windowBlock.SetColor("_BaseColor", on ? windowOnColor * 0.8f : new Color(0.08f, 0.07f, 0.06f));
        windowBlock.SetColor("_EmissionColor", on ? windowOnColor * 0.9f : Color.black);
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

    [ContextMenu("Test: the neighbour comes out for the evening")]
    void TestComeOut()
    {
        if (State == Mood.Inside) Switch(ComeOutForTheEvening());
    }
}
