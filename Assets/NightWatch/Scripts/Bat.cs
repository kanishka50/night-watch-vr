using UnityEngine;

// A small bat flitting over the field at dusk and early night, catching insects: quick, jerky
// loops at 3 to 9 m up, wings beating fast. Seen against the evening sky; hidden by day.
public class Bat : MonoBehaviour
{
    public Transform leftWing, rightWing;
    public Vector3 centre;
    public float radius = 12f;
    [Range(0f, 1f)] public float from = 0.22f;
    [Range(0f, 1f)] public float to = 0.42f;

    Vector3 a, b;     // loop speeds and phases, different for each bat
    Renderer[] parts;
    bool shown = true;

    void Start()
    {
        a = new Vector3(Random.Range(0.35f, 0.6f), Random.Range(0.8f, 1.4f), Random.Range(0.4f, 0.7f));
        b = new Vector3(Random.value * 6f, Random.value * 6f, Random.value * 6f);
        parts = GetComponentsInChildren<Renderer>();
        Show(false);
    }

    void Update()
    {
        float p = DayNightCycle.ShownProgress;
        bool on = p > from && p < to;
        if (on != shown) Show(on);
        if (!on) return;

        float t = Time.time;
        // Two loops of different speeds make a wandering figure-of-eight; a fast wobble makes it jerky.
        var at = centre + new Vector3(
            Mathf.Sin(t * a.x + b.x) * radius + Mathf.Sin(t * 3.1f + b.y) * 0.8f,
            6f + Mathf.Sin(t * a.y + b.y) * 2.5f + Mathf.Sin(t * 4.3f) * 0.4f,
            Mathf.Sin(t * a.z * 2f + b.z) * radius * 0.6f + Mathf.Cos(t * 2.7f + b.x) * 0.8f);
        var move = at - transform.position;
        transform.position = at;
        if (move.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(move);
        float beat = Mathf.Sin(t * 2f * Mathf.PI * 9f) * 50f;
        if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 0f, beat);
        if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, 0f, -beat);
    }

    void Show(bool on)
    {
        shown = on;
        foreach (var r in parts) r.enabled = on;
    }
}
