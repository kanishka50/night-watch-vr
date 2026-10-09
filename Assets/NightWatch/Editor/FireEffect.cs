using System.IO;
using UnityEditor;
using UnityEngine;

// Real moving fire made with Unity's particle system (no downloaded assets): flickering flames
// that rise and fade from yellow to orange to dark red, with a few glowing embers drifting up.
// Used for the bonfires, the fire pot, the fire torches and the neighbour's yard fire.
// The fire follows the scale of its parent (a bonfire that burns down, a yard fire built up).
public static class FireEffect
{
    const string TexturePath = "Assets/NightWatch/Materials/SoftDot.png";
    const string MaterialPath = "Assets/NightWatch/Materials/FireParticle.mat";

    // size 1 = a bonfire flame about 1 m tall. The fire starts at localPosition and rises.
    public static ParticleSystem Build(Transform parent, Vector3 localPosition, float size, bool embers = true)
    {
        var go = new GameObject("FireParticles");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        var flames = go.AddComponent<ParticleSystem>();
        var main = flames.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f * size, 1.3f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f * size, 0.6f * size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // a moving torch leaves a trail
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        // Big fires need more flames to stay full (a bonfire is size 2.5).
        float fullness = Mathf.Max(1f, size * 0.6f);
        main.maxParticles = Mathf.RoundToInt(60 * fullness);

        var emission = flames.emission;
        emission.rateOverTime = 35f * fullness;

        var shape = flames.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.18f * size;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // point the cone up

        var colour = flames.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.4f),
                new GradientColorKey(new Color(0.6f, 0.12f, 0.03f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.12f),
                new GradientAlphaKey(0.6f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            });
        colour.color = gradient;

        var sizeOverLife = flames.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.3f)));

        // A little sideways flicker, like flames in a breeze.
        var noise = flames.noise;
        noise.enabled = true;
        noise.strength = 0.3f * size;
        noise.frequency = 1.5f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Material();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (embers) BuildEmbers(go.transform, size);
        return flames;
    }

    // Small glowing sparks that drift up and out of the fire.
    static void BuildEmbers(Transform parent, float size)
    {
        var go = new GameObject("Embers");
        go.transform.SetParent(parent, false);
        var embers = go.AddComponent<ParticleSystem>();
        var main = embers.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * size, 1.6f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f * size, 0.06f * size);
        main.startColor = new Color(1f, 0.6f, 0.2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 20;
        var emission = embers.emission;
        emission.rateOverTime = 5f;
        var shape = embers.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.15f * size;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var colour = embers.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;
        var noise = embers.noise;
        noise.enabled = true;
        noise.strength = 0.5f * size;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Material();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // Additive glow (brighter where flames overlap), using a soft round dot.
    static Material Material()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", SoftDot());
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f);   // transparent
        mat.SetFloat("_Blend", 2f);     // additive
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }

    // A soft white dot, see-through at the edges, saved as a PNG so the build keeps it.
    public static Texture2D SoftDot()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (tex != null) return tex;
        if (!AssetDatabase.IsValidFolder(EditorKit.MaterialFolder)) EditorKit.Mat("Fire", Color.white, true);
        const int size = 64;
        var pixels = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            pixels.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        pixels.Apply();
        File.WriteAllBytes(TexturePath, pixels.EncodeToPNG());
        Object.DestroyImmediate(pixels);
        AssetDatabase.ImportAsset(TexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }
}
