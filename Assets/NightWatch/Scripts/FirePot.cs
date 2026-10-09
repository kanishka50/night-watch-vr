using UnityEngine;

// Anything that can be set alight at the fire pot (fire torch, firecracker).
public interface IIgnitable
{
    void Ignite();
}

// The small fire kept burning in the watch hut. Touch a torch or a firecracker to it to light it.
// Needs a trigger collider (the flame zone) on this object.
public class FirePot : MonoBehaviour
{
    public static FirePot Instance { get; private set; }

    public Light fireLight;
    public float baseIntensity = 2.5f;
    public AudioSource crackle;
    [Tooltip("Pressing use on a torch or cracker this close to the pot (along the floor) lights it. " +
             "3.5 m covers the whole hut floor, because reaching the pot is fiddly with keyboard and mouse.")]
    public float reach = 3.5f;

    void Awake() => Instance = this;

    // True if a point is close enough to the fire to light something there.
    // Measured along the floor, and only from up in the hut (the pot is not reachable from the ground).
    public static bool IsNear(Vector3 point)
    {
        if (Instance == null || PlayerState.OnGround) return false;
        var a = point;
        var b = Instance.transform.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b) <= Instance.reach;
    }

    // Said when the player presses use on an unlit torch or cracker that is too far away.
    public static void TooFarMessage()
    {
        NoticeBoard.Message(PlayerState.OnGround
            ? "The fire pot is up in the hut. Climb up to light it."
            : "Bring it closer to the fire pot on the table, then press Use (trigger / T) again.");
    }

    void Start()
    {
        if (crackle == null) return;
        crackle.clip = SoundBank.FireCrackle;
        crackle.loop = true;
        crackle.Play();
    }

    void Update()
    {
        // A soft flicker.
        if (fireLight != null)
            fireLight.intensity = baseIntensity * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, 0f));
    }

    void OnTriggerEnter(Collider other) => TryLight(other);
    void OnTriggerStay(Collider other) => TryLight(other);

    static void TryLight(Collider other)
    {
        var target = other.GetComponentInParent<IIgnitable>();
        if (target != null) target.Ignite();
    }
}
