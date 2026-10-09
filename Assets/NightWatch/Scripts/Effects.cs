using UnityEngine;

// Shared one-off effects: a dust or smoke cloud from a hidden particle template.
public static class Effects
{
    // Set by HutDamage (which is given the template by the setup tool).
    public static ParticleSystem DustTemplate;

    public static void Dust(Vector3 at, Color color, float scale = 1f)
    {
        if (DustTemplate == null) return;
        var cloud = Object.Instantiate(DustTemplate, at, Quaternion.identity);
        cloud.transform.localScale = Vector3.one * scale;
        var main = cloud.main;
        // The dust material is unlit, so dim it with the light of the moment (the sky part of the
        // ambient light): full colour by day, about a third at night, so it does not glow.
        float light = Mathf.Clamp01(RenderSettings.ambientSkyColor.maxColorComponent / 0.6f);
        main.startColor = new Color(color.r * light, color.g * light, color.b * light, color.a);
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        cloud.gameObject.SetActive(true);
        cloud.Play();
        Object.Destroy(cloud.gameObject, 5f);
    }
}
