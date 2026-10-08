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
    [Tooltip("Pressing use on a torch or cracker this close to the pot lights it.")]
    public float lightingRange = 2f;

    void Awake() => Instance = this;

    // True if a point is close enough to the fire to light something there.
    public static bool IsNear(Vector3 point)
    {
        return Instance != null && Vector3.Distance(point, Instance.transform.position) <= Instance.lightingRange;
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
