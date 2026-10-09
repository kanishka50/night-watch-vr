using UnityEngine;

// Small oil-lamp flames (the pahan round the dagaba) that flicker and lean with the breeze, each
// on its own beat, and one shared light that flickers with them. A TimeWindow lights and puts
// them out.
public class FlameFlicker : MonoBehaviour
{
    public Transform[] flames;
    public Light glow;

    Vector3[] sizes;
    float glowRange;

    void Start()
    {
        sizes = new Vector3[flames.Length];
        for (int i = 0; i < flames.Length; i++) sizes[i] = flames[i] != null ? flames[i].localScale : Vector3.one;
        glowRange = glow != null ? glow.range : 0f;
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 120f * 120f) return;
        float t = Time.time;
        var wind = WindDriver.Direction * (0.15f * WindDriver.Strength);
        for (int i = 0; i < flames.Length; i++)
        {
            if (flames[i] == null) continue;
            float k = 0.8f + 0.2f * Mathf.Sin(t * 13f + i * 1.7f) + 0.12f * Mathf.Sin(t * 23f + i * 0.9f);
            flames[i].localScale = new Vector3(sizes[i].x, sizes[i].y * k, sizes[i].z);
            flames[i].localRotation = Quaternion.Euler(wind.y * 60f * k, 0f, -wind.x * 60f * k);
        }
        // The shared light only flickers gently (a ring of many small flames averages out). Its
        // reach wobbles; TimeWindow owns its brightness.
        if (glow != null) glow.range = glowRange * (1f + 0.05f * Mathf.Sin(t * 9f) + 0.03f * Mathf.Sin(t * 17f));
    }
}
