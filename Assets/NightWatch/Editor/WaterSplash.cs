using UnityEditor;
using UnityEngine;

// A small splash where water falls (the canal's outlet and its end in the field): pale drops that
// jump up and fall back, and a little white foam that spreads on the water. Lit particles, so they
// are dim at night like everything else. size 1 = the outlet's fall.
public static class WaterSplash
{
    const string MaterialPath = "Assets/NightWatch/Materials/SplashParticle.mat";

    public static ParticleSystem Build(Transform parent, string name, Vector3 at, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = at;

        var drops = go.AddComponent<ParticleSystem>();
        var main = drops.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * size, 1.1f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new Color(0.85f, 0.9f, 0.95f, 0.7f);
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        var emission = drops.emission;
        emission.rateOverTime = 30f * size;
        var shape = drops.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.12f * size;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        Fade(drops);
        Look(go);

        // Foam: a few soft white patches that spread and fade on the surface.
        var foamGo = new GameObject("Foam");
        foamGo.transform.SetParent(go.transform, false);
        foamGo.transform.localPosition = Vector3.up * 0.01f;
        var foam = foamGo.AddComponent<ParticleSystem>();
        var fm = foam.main;
        fm.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        fm.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        fm.startSize = new ParticleSystem.MinMaxCurve(0.12f * size, 0.25f * size);
        fm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        fm.startColor = new Color(0.9f, 0.92f, 0.95f, 0.35f);
        fm.simulationSpace = ParticleSystemSimulationSpace.World;
        fm.maxParticles = 20;
        var fe = foam.emission;
        fe.rateOverTime = 8f * size;
        var fs = foam.shape;
        fs.shapeType = ParticleSystemShapeType.Circle;
        fs.radius = 0.15f * size;
        fs.rotation = new Vector3(90f, 0f, 0f);   // flat on the water
        Fade(foam);
        var fr = Look(foamGo);
        fr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        return drops;
    }

    static void Fade(ParticleSystem ps)
    {
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;
    }

    static ParticleSystemRenderer Look(GameObject go)
    {
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Material();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    // Alpha-blended, lit by the sun or moon and the sky light, using FireEffect's soft dot.
    static Material Material()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
        mat.SetTexture("_BaseMap", FireEffect.SoftDot());
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f);   // transparent
        mat.SetFloat("_Blend", 0f);     // alpha
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }
}
