using UnityEditor;
using UnityEngine;

// The water materials (shader NightWatch/Water) and the ripple picture they share.
//  - Tank (wewa): deep green-brown, bigger ripples drifting with the wind, a clear sky mirror.
//  - Plots: flooded paddy, shallow and muddy, almost still (small, slow ripples).
//  - Canal (ela): ripples running along the channel toward the field.
// The ripple picture is made here, no download: many small waves added together, each with a
// whole number of waves across the picture, so it repeats without a seam.
public static class WaterSetup
{
    const string RipplePath = "Assets/NightWatch/Textures/WaterRipples.png";
    const int RippleSize = 256;

    public static Material Tank() => Make("WaterTank", new Color(0.055f, 0.13f, 0.12f), 6f, 0.4f, new Vector2(1f, 0.35f), 0.12f, 1f, 0.9f, 1.6f, 0.35f);
    public static Material Plots() => Make("WaterPlots", new Color(0.11f, 0.10f, 0.07f), 2.5f, 0.2f, new Vector2(0.6f, 1f), 0.04f, 1f, 0.8f, 1.1f, 0.6f);
    // The canal's mesh v runs along the channel from the sluice, so (0, 1) is downstream.
    public static Material Canal() => Make("WaterCanal", new Color(0.08f, 0.11f, 0.09f), 1.4f, 0.6f, new Vector2(0f, 1f), 0.45f, 0.15f, 0.75f, 1.2f, 0.5f);

    static Material Make(string name, Color colour, float rippleSize, float strength, Vector2 drift, float speed,
        float cross, float reflection, float glint, float murk)
    {
        var shader = Shader.Find("NightWatch/Water");
        if (shader == null)
        {
            Debug.LogWarning("Night Watch: shader NightWatch/Water not found; the water uses plain Lit.");
            return EditorKit.Mat(name, colour);
        }
        string path = $"{EditorKit.MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_DeepColor", colour);
        mat.SetTexture("_RippleMap", Ripples());
        mat.SetFloat("_RippleSize", rippleSize);
        mat.SetFloat("_RippleStrength", strength);
        mat.SetVector("_FlowDir", new Vector4(drift.x, drift.y, 0f, 0f));
        mat.SetFloat("_FlowSpeed", speed);
        mat.SetFloat("_CrossDrift", cross);
        mat.SetFloat("_Reflection", reflection);
        mat.SetFloat("_Glint", glint);
        mat.SetFloat("_Murk", murk);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // The ripple normal map (made once, then reused).
    static Texture2D Ripples()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(RipplePath);
        if (existing != null) return existing;

        // 48 waves. Most run roughly across the wind (x), a few in any direction; small waves are
        // weaker than big ones, as on real water.
        var random = new System.Random(5);
        const int count = 48;
        var kx = new float[count];
        var ky = new float[count];
        var amp = new float[count];
        var phase = new float[count];
        for (int i = 0; i < count; i++)
        {
            float angle = i < count * 2 / 3
                ? (float)(random.NextDouble() - 0.5) * 2.1f
                : (float)(random.NextDouble() * Mathf.PI * 2.0);
            float waves = 2f + Mathf.Pow((float)random.NextDouble(), 1.5f) * 14f;
            kx[i] = Mathf.Round(Mathf.Cos(angle) * waves);
            ky[i] = Mathf.Round(Mathf.Sin(angle) * waves);
            if (kx[i] == 0f && ky[i] == 0f) kx[i] = 1f;
            amp[i] = Mathf.Pow(new Vector2(kx[i], ky[i]).magnitude, -1.6f);
            phase[i] = (float)(random.NextDouble() * Mathf.PI * 2.0);
        }

        // Slopes of the summed waves at every pixel.
        var dx = new float[RippleSize * RippleSize];
        var dy = new float[RippleSize * RippleSize];
        float largest = 0f;
        for (int y = 0; y < RippleSize; y++)
        for (int x = 0; x < RippleSize; x++)
        {
            float u = x / (float)RippleSize, v = y / (float)RippleSize;
            float sx = 0f, sy = 0f;
            for (int i = 0; i < count; i++)
            {
                float c = Mathf.Cos(2f * Mathf.PI * (kx[i] * u + ky[i] * v) + phase[i]) * amp[i] * 2f * Mathf.PI;
                sx += c * kx[i];
                sy += c * ky[i];
            }
            dx[y * RippleSize + x] = sx;
            dy[y * RippleSize + x] = sy;
            largest = Mathf.Max(largest, Mathf.Abs(sx), Mathf.Abs(sy));
        }

        // Steepest slope about 1 (45 degrees); the material's strength scales it down.
        var tex = new Texture2D(RippleSize, RippleSize, TextureFormat.RGB24, false, true);
        var pixels = new Color[RippleSize * RippleSize];
        for (int p = 0; p < pixels.Length; p++)
        {
            var n = new Vector3(-dx[p] / largest, -dy[p] / largest, 1f).normalized;
            pixels[p] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
        }
        tex.SetPixels(pixels);
        System.IO.File.WriteAllBytes(RipplePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(RipplePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(RipplePath);
        importer.textureType = TextureImporterType.NormalMap;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 4;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(RipplePath);
    }
}
