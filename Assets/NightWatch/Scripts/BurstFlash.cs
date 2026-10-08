using UnityEngine;

// A short flash of light that fades out and removes itself (cracker bursts, fence sparks).
public class BurstFlash : MonoBehaviour
{
    Light flash;
    float startIntensity;
    float life;
    float age;

    public static void At(Vector3 position, Color color, float intensity, float range, float seconds)
    {
        var go = new GameObject("Flash");
        go.transform.position = position;
        var f = go.AddComponent<BurstFlash>();
        f.flash = go.AddComponent<Light>();
        f.flash.type = LightType.Point;
        f.flash.color = color;
        f.flash.range = range;
        f.flash.intensity = intensity;
        f.startIntensity = intensity;
        f.life = seconds;
    }

    void Update()
    {
        age += Time.deltaTime;
        flash.intensity = Mathf.Lerp(startIntensity, 0f, age / life);
        if (age >= life) Destroy(gameObject);
    }
}
