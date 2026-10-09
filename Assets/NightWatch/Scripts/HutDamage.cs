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
    float shakeScale = 1f;
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
            float k = shakeAmount * shakeScale * (shakeTimer / shakeSeconds);
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
        shakeScale = 1f;
        var away = Away(from);
        Dust(home - away * 1.8f + Vector3.up * 1f);

        if (hits == 1) BreakSide(from);
        else if (hits >= 2) Lean(away);
    }

    // A shake with no damage (the story film's bull). scale = how much more than a game hit.
    public void Shake(float scale)
    {
        shakeTimer = shakeSeconds;
        shakeScale = scale;
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
        // The climb targets are solid boxes round the ladder (so the pointer finds them): the
        // falling ladder must not be caught inside them.
        if (partName == "Ladder")
            foreach (var targetName in new[] { "LadderTop", "LadderBottom" })
            {
                var target = GameObject.Find(targetName);
                if (target == null) continue;
                foreach (var a in target.GetComponents<Collider>())
                    foreach (var b in part.GetComponentsInChildren<Collider>())
                        Physics.IgnoreCollision(a, b);
            }
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

        // The roof falls in one piece: one body for the frame and the thatch. The thatch had no
        // collider, so it stayed in the air while the poles fell.
        var roof = transform.Find("Roof");
        if (roof != null && roof.GetComponent<Rigidbody>() == null)
        {
            var thatch = roof.Find("Thatch");
            var mesh = thatch != null ? thatch.GetComponent<MeshFilter>() : null;
            if (mesh != null && mesh.sharedMesh != null && thatch.GetComponent<Collider>() == null)
            {
                var box = thatch.gameObject.AddComponent<BoxCollider>();
                box.center = mesh.sharedMesh.bounds.center;
                box.size = mesh.sharedMesh.bounds.size;
            }
            var roofBody = roof.gameObject.AddComponent<Rigidbody>();
            roofBody.mass = 60f;
            roofBody.AddExplosionForce(collapseForce * 2f, from + Vector3.up * 1.5f, 10f, 0.5f, ForceMode.Impulse);
        }

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
