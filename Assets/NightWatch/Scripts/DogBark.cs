using UnityEngine;

// A village dog at the neighbour's house. When a wild elephant comes near the village it barks in
// bursts, as village dogs do (often the first warning a farmer gets). Quiet otherwise.
[RequireComponent(typeof(AudioSource))]
public class DogBark : MonoBehaviour
{
    public float hearElephant = 40f;

    [Header("Test (read only)")]
    public string status;

    AudioSource voice;
    float nextBurst;
    int barksLeft;
    float nextBark;

    void Start()
    {
        voice = GetComponent<AudioSource>();
    }

    void Update()
    {
        var elephant = ElephantAI.NearestActive(transform.position);
        float d = elephant != null ? Vector3.Distance(elephant.transform.position, transform.position) : float.MaxValue;
        status = elephant != null ? $"elephant {d:F0} m" : "no elephant";

        if (barksLeft == 0 && d < hearElephant && Time.time > nextBurst)
        {
            // Closer elephant: longer bursts, shorter pauses.
            float near = 1f - d / hearElephant;
            barksLeft = Random.Range(2, 4) + Mathf.RoundToInt(near * 3f);
            nextBurst = Time.time + Mathf.Lerp(7f, 2.5f, near) + Random.Range(0f, 2f);
        }
        if (barksLeft > 0 && Time.time > nextBark)
        {
            Bark();
            barksLeft--;
            nextBark = Time.time + Random.Range(0.35f, 0.6f);
        }
    }

    void Bark()
    {
        voice.pitch = Random.Range(0.92f, 1.08f);
        voice.PlayOneShot(SoundBank.Bark, Random.Range(0.75f, 1f));
    }

    [ContextMenu("Test: bark")]
    void TestBark() => barksLeft = 3;
}
