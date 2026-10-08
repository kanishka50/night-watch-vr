using System.Collections;
using UnityEngine;

// The neighbour's house on the village side, and the neighbour.
// Real practice: elephants raid houses for stored rice, and many people are hurt when they are
// outside after dark not knowing an elephant is near. Warning neighbours early keeps them safe,
// and villagers are told to keep outside lights on at night.
//
//   Inside   (afternoon) the house is dark
//   Outside  from dusk: the neighbour lights a small evening fire in the yard and walks about
//            with a lamp. The player can see them from the hut.
//   Phone call: they stop, lift the lamp toward the hut, walk inside, the door closes and the
//            porch light comes on (Warned). The bull from the village side then goes to the field.
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
    public Light windowLight;
    [Tooltip("The light over the door that lights the yard once they are safe inside.")]
    public Light porchLight;
    public Renderer porchBulb;
    [Tooltip("The small evening fire in the yard (flame, light and crackle).")]
    public GameObject yardFire;
    [Tooltip("The neighbour, feet at the pivot. Hidden while inside.")]
    public Transform figure;
    public Transform lampPivot;
    public Light lamp;

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

    public Mood State { get; private set; } = Mood.Inside;

    ElephantAI bull;
    float bullCameTime = float.PositiveInfinity;
    float atStoreTime = -1f;
    bool walking;
    float lampIntensity;
    MaterialPropertyBlock windowBlock;
    MaterialPropertyBlock bulbBlock;

    void Awake()
    {
        Instance = this;
        if (figure != null) figure.gameObject.SetActive(false);
        if (yardFire != null)
        {
            // The crackle starts when the fire is lit (the sound is made in code, so set it here).
            var crackle = yardFire.GetComponent<AudioSource>();
            if (crackle != null) crackle.clip = SoundBank.FireCrackle;
            yardFire.SetActive(false);
        }
        if (lamp != null) lampIntensity = lamp.intensity;
        SetLights(false);
    }

    // ---------------- Called by other scripts ----------------

    // The phone in the hut.
    public void Call()
    {
        SoundBank.PlayAt(SoundBank.Ring, PlayerState.Head != null ? PlayerState.Head.position : transform.position, 0.5f, 1f);
        switch (State)
        {
            case Mood.Inside:
                State = Mood.Warned;
                SetLights(true);
                Warned("Neighbour on the phone: \"Thank you! We will stay inside tonight and keep the lights on.\"");
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
            "Elephants raid houses for stored rice. A neighbour who does not know is outside in the dark.");
        if (storePoint != null) bull.GoTo(storePoint.position);
    }

    // ---------------- Every frame ----------------

    void Update()
    {
        var game = GameManager.Instance;
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
        yield return new WaitForSeconds(1f);
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
                "Even a late warning helps: they went inside before the bull reached them.");
        }
        else Warned("Neighbour on the phone: \"Thank you! We are going inside, and we will keep the lights on.\"");
        if (bull != null) bull.ClearDetour();

        // They stop, turn to the hut and lift the lamp: the call got through.
        if (!hurry)
        {
            FaceTowards(ElephantAI.HutCenter);
            yield return LiftLamp(1.5f);
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
        if (lampPivot != null && lampPivot.parent == figure) lampPivot.localRotation = Quaternion.Euler(-60f, 0f, 0f);

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
            "You drove the bull away from the house from the safety of the hut.");
        NoticeBoard.Message("The bull has gone. Your neighbour is hurrying back inside, safe.");
        if (lampPivot != null && lampPivot.parent == figure) lampPivot.localRotation = Quaternion.identity;
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
            "The bull reached your neighbour before you drove it away. Warn people early, and act fast when someone is in danger.");
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
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            float k = t / 0.45f;
            figure.position = startPos + pushDirection * 1.8f * k + Vector3.up * 0.4f * Mathf.Sin(k * Mathf.PI);
            figure.rotation = Quaternion.Slerp(upright, down, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        figure.position = startPos + pushDirection * 1.8f;
        figure.rotation = down;

        // Hurt, not killed: after a while they get up slowly and limp back inside.
        yield return new WaitForSeconds(hurtLieSeconds);
        NoticeBoard.Message("Your neighbour is hurt, but alive. They are limping back inside.");
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            figure.rotation = Quaternion.Slerp(down, upright, Mathf.SmoothStep(0f, 1f, t / 1.5f));
            yield return null;
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
            "A neighbour who knows an elephant is near stays inside with the lights on.");
    }

    IEnumerator GoInAndLightUp()
    {
        figure.gameObject.SetActive(false);
        yield return SwingDoor(false);
        SetLights(true);
    }

    IEnumerator LiftLamp(float seconds)
    {
        if (lampPivot == null || lampPivot.parent != figure) yield break;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            // Raised high and waved toward the hut.
            lampPivot.localRotation = Quaternion.Euler(-100f, 0f, Mathf.Sin(t * 8f) * 20f);
            yield return null;
        }
        lampPivot.localRotation = Quaternion.identity;
    }

    IEnumerator Walk(Vector3 target, float speed, bool limp)
    {
        walking = true;
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
            float bob = limp ? -0.1f * Mathf.Abs(Mathf.Sin(t * 3f)) : 0.04f * Mathf.Abs(Mathf.Sin(t * 7f));
            next.y = baseY + bob;
            figure.position = next;
            if (!limp) SwingLamp(1f);
            yield return null;
        }
        figure.position = new Vector3(target.x, baseY, target.z);
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

    void SwingLamp(float amount)
    {
        if (lampPivot == null || lampPivot.parent != figure) return;
        lampPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 5f) * 12f * amount);
        if (lamp != null) lamp.intensity = lampIntensity * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, 3f));
    }

    // The window glow and the porch light over the door.
    void SetLights(bool on)
    {
        if (windowLight != null) windowLight.enabled = on;
        if (porchLight != null) porchLight.enabled = on;
        if (window != null)
        {
            if (windowBlock == null) windowBlock = new MaterialPropertyBlock();
            windowBlock.SetColor("_BaseColor", on ? windowOnColor : new Color(0.08f, 0.07f, 0.06f));
            windowBlock.SetColor("_EmissionColor", on ? windowOnColor * 2f : Color.black);
            window.SetPropertyBlock(windowBlock);
        }
        if (porchBulb != null)
        {
            if (bulbBlock == null) bulbBlock = new MaterialPropertyBlock();
            bulbBlock.SetColor("_BaseColor", on ? new Color(1f, 0.9f, 0.6f) : new Color(0.15f, 0.14f, 0.12f));
            porchBulb.SetPropertyBlock(bulbBlock);
        }
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
