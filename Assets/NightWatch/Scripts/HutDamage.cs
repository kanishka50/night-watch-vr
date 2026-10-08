using UnityEngine;

// The watch hut reacting to elephant charges, so the danger is visible and grows:
//   hit 1 : the hut shakes, the rail (or ladder) on the elephant's side breaks off, dust
//   hit 2 : the hut shakes and tilts away from the elephant, the roof slips, dust
//   hit 3 : the hut falls apart (every part gets physics and is pushed away from the elephant)
public class HutDamage : MonoBehaviour
{
    public static HutDamage Instance { get; private set; }

    public float shakeSeconds = 0.7f;
    public float shakeAmount = 0.06f;
    public float tiltDegrees = 5f;
    public float collapseForce = 350f;
    [Tooltip("A hidden dust cloud, copied to where the elephant strikes.")]
    public ParticleSystem dustTemplate;
    [Tooltip("Things in the hut (table, tools, notice board) hidden once it has fallen.")]
    public GameObject[] hideOnCollapse;

    Vector3 home;
    Quaternion targetRotation;
    float shakeTimer;
    bool collapsed;

    void Awake()
    {
        Instance = this;
        if (dustTemplate != null) Effects.DustTemplate = dustTemplate;
        home = transform.position;
        targetRotation = transform.rotation;
    }

    void Update()
    {
        if (collapsed) return;

        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 10f * Time.deltaTime);
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float k = shakeAmount * (shakeTimer / shakeSeconds);
            transform.position = home + new Vector3(Mathf.Sin(Time.time * 60f) * k, 0f, Mathf.Cos(Time.time * 47f) * k);
        }
        else
        {
            transform.position = home;
        }
    }

    // A charge that did not bring the hut down. hits = how many hits so far.
    public void TakeHit(int hits, Vector3 from)
    {
        shakeTimer = shakeSeconds;
        var away = Away(from);
        Dust(home - away * 1.8f + Vector3.up * 1f);

        if (hits == 1) BreakSide(from);
        else if (hits >= 2) Lean(away);
    }

    // The part of the hut facing the elephant breaks off and falls.
    void BreakSide(Vector3 from)
    {
        var d = from - home;
        string partName = Mathf.Abs(d.z) >= Mathf.Abs(d.x)
            ? (d.z > 0f ? "Rail_North" : "Ladder")
            : (d.x > 0f ? "Rail_East" : "Rail_West");
        var part = transform.Find(partName);
        if (part == null) return;

        part.SetParent(null, true);
        var body = part.gameObject.AddComponent<Rigidbody>();
        body.mass = 10f;
        // Falls outward, toward the elephant's side, and down to the ground.
        body.AddForce(-Away(from) * 2f + Vector3.up * 1f, ForceMode.VelocityChange);
        body.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);
    }

    // The hut leans away from the elephant and the roof slips.
    void Lean(Vector3 away)
    {
        var axis = Vector3.Cross(Vector3.up, away);
        targetRotation = Quaternion.AngleAxis(tiltDegrees, axis) * targetRotation;
        var roof = transform.Find("Roof");
        if (roof != null)
        {
            roof.position += away * 0.35f;
            roof.rotation = Quaternion.AngleAxis(12f, axis) * roof.rotation;
        }
    }

    // The hut breaks: posts, floor, rails, roof and ladder all fall.
    public void Collapse(Vector3 from)
    {
        if (collapsed) return;
        collapsed = true;
        transform.position = home;
        SoundBank.PlayAt(SoundBank.BranchCrack, home + Vector3.up * 2f, 1f, 8f);
        SoundBank.PlayAt(SoundBank.Bang, home + Vector3.up * 2f, 1f, 8f);
        Dust(home + Vector3.up * 1f);
        Dust(home + Vector3.up * 2.5f);

        foreach (var part in GetComponentsInChildren<Collider>())
        {
            if (part.isTrigger || part.attachedRigidbody != null) continue;
            var body = part.gameObject.AddComponent<Rigidbody>();
            body.mass = 15f;
            body.AddExplosionForce(collapseForce, from + Vector3.up * 1.5f, 10f, 0.5f, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * 30f, ForceMode.Impulse);
        }
        // By then the player's view is going dark.
        Invoke(nameof(HideContents), 1.2f);
    }

    void HideContents()
    {
        if (hideOnCollapse == null) return;
        foreach (var go in hideOnCollapse)
            if (go != null) go.SetActive(false);
    }

    static void Dust(Vector3 at) => Effects.Dust(at, new Color(0.45f, 0.36f, 0.25f, 0.6f));

    // Flat direction from the elephant toward the hut.
    Vector3 Away(Vector3 from)
    {
        var d = home - from;
        d.y = 0f;
        return d.sqrMagnitude < 0.01f ? Vector3.forward : d.normalized;
    }
}
