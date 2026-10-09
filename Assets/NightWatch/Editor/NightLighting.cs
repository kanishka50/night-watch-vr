using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Turns the open scene into night: dim blue moonlight, a dark sky, dark ambient light,
// fog that hides the map edge, and a warm light in the neighbour's window.
// Run it from the menu: Night Watch > Apply Night Lighting. Safe to run again.
// Lights it adds go under a "Lighting" object, so rebuilding the blockout does not remove them.
public static class NightLighting
{
    const string RootName = "Lighting";
    const string MaterialFolder = "Assets/NightWatch/Materials";

    static readonly Color MoonColor = new Color(0.60f, 0.70f, 1.00f);
    static readonly Color AmbientColor = new Color(0.06f, 0.08f, 0.13f);
    static readonly Color FogColor = new Color(0.02f, 0.03f, 0.06f);
    static readonly Color WindowColor = new Color(1.00f, 0.75f, 0.40f);

    [MenuItem("Night Watch/Apply Night Lighting")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Night Watch", "Stop Play mode first, then apply the lighting.", "OK");
            return;
        }

        SetUpMoon();
        SetUpSkyAndFog();
        SetUpWindowLight();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Night Watch: night lighting applied. Save the scene with Ctrl+S.");
    }

    // The template's Directional Light becomes the moon: low, blue and weak.
    static void SetUpMoon()
    {
        var moon = RenderSettings.sun;
        if (moon == null)
        {
            var go = GameObject.Find("Directional Light");
            if (go != null) moon = go.GetComponent<Light>();
        }
        if (moon == null)
        {
            Debug.LogWarning("Night Watch: no Directional Light found, moon not set.");
            return;
        }

        Undo.RecordObject(moon, "Night moon");
        Undo.RecordObject(moon.transform, "Night moon");
        moon.gameObject.name = "Moon";
        moon.color = MoonColor;
        moon.intensity = 0.35f;
        moon.shadows = LightShadows.Soft;
        // Shines from behind the player toward the forest, so the trees facing the hut are lit.
        moon.transform.rotation = Quaternion.Euler(35f, 20f, 0f);
        RenderSettings.sun = moon;
    }

    static void SetUpSkyAndFog()
    {
        RenderSettings.skybox = NightSkyMaterial();

        // A sky / horizon / ground gradient (cheap) keeps shadows from going pure black.
        // DayNightCycle changes these colours with the time of day.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.08f, 0.10f, 0.17f);
        RenderSettings.ambientEquatorColor = AmbientColor;
        RenderSettings.ambientGroundColor = new Color(0.03f, 0.035f, 0.05f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogDensity = 0.035f;
    }

    // Our own sky (NightWatch/Sky: gradient, sun glow, moon, stars), saved with its night look.
    // DayNightCycle drives a copy of it at run time. Unity's procedural sky is the fallback.
    static Material NightSkyMaterial()
    {
        string path = $"{MaterialFolder}/NightSky.mat";
        var shader = Shader.Find("NightWatch/Sky");
        if (shader == null)
        {
            Debug.LogWarning("Night Watch: shader NightWatch/Sky not found (Assets/NightWatch/Shaders), using Unity's procedural sky.");
            shader = Shader.Find("Skybox/Procedural");
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        if (mat.HasProperty("_ZenithColor"))
        {
            mat.SetColor("_ZenithColor", new Color(0.015f, 0.025f, 0.07f));
            mat.SetColor("_HorizonColor", new Color(0.05f, 0.07f, 0.13f));
            mat.SetColor("_GroundColor", FogColor);
            mat.SetFloat("_SunGlow", 0f);
            mat.SetFloat("_MoonShow", 1f);
            mat.SetFloat("_Stars", 1f);
            // Where the moon light comes from (SetUpMoon): the moon is opposite its light.
            mat.SetVector("_MoonDir", -(Quaternion.Euler(35f, 20f, 0f) * Vector3.forward));
        }
        else
        {
            mat.SetFloat("_SunDisk", 0f);
            mat.EnableKeyword("_SUNDISK_NONE");
            mat.SetColor("_SkyTint", new Color(0.20f, 0.25f, 0.45f));
            mat.SetColor("_GroundColor", FogColor);
            mat.SetFloat("_Exposure", 0.12f);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // The neighbour's window glows softly (emission only, no light on the wall). NeighbourHouse
    // switches the glow on when the neighbour is safe inside.
    static void SetUpWindowLight()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Night lighting");

        var window = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/HouseWindow.mat");
        if (window != null)
        {
            window.EnableKeyword("_EMISSION");
            window.SetColor("_EmissionColor", WindowColor * 2f);
            window.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(window);
        }
        AssetDatabase.SaveAssets();
    }
}
