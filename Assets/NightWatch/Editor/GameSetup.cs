using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One menu item that builds the whole game in the open scene:
//   Night Watch > Build Full Game
// It rebuilds the blockout, the night lighting, the field (fence and crop plots), the NavMesh,
// the elephants and their arrivals, the tools in the hut, the screens and the game managers.
// Running it again replaces everything it made before.
public static class GameSetup
{
    [MenuItem("Night Watch/Build Full Game")]
    public static void BuildFullGame()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Night Watch", "Stop Play mode first, then build the game.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Night Watch",
                "This rebuilds the level, the elephants, the tools and the screens in the open scene. Continue?",
                "Build", "Cancel"))
            return;

        // Parts made by earlier versions of the tools, or by a previous run of this one.
        foreach (var name in new[] { "HutProps", "Elephant", "ElephantRoute", "Navigation", "Field", "Game" })
            EditorKit.Remove(name);

        BlockoutBuilder.Build(false);
        NightLighting.Apply();
        var sun = HutPropsSetup.CreateSun();

        ElephantSetup.BuildField();
        ElephantSetup.BakeNavMesh();

        var game = new GameObject("Game").transform;
        Undo.RegisterCreatedObjectUndo(game.gameObject, "Game");
        ElephantSetup.BuildElephants(game);
        HutPropsSetup.BuildTools();
        HutPropsSetup.BuildScreens(game);
        HutPropsSetup.BuildManagers(game, sun);

        AddSceneToBuild();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Night Watch: full game built. Save the scene with Ctrl+S.");
    }

    // The scene must be in the build list for Restart (scene reload) and for the Windows build.
    static void AddSceneToBuild()
    {
        var path = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(path)) return;
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

// Small helpers shared by the setup tools.
public static class EditorKit
{
    public const string MaterialFolder = "Assets/NightWatch/Materials";

    // A saved material (so the scene keeps it). Reuses the existing one if it is already there.
    public static Material Mat(string name, Color color, bool unlit = false)
    {
        if (!AssetDatabase.IsValidFolder("Assets/NightWatch"))
            AssetDatabase.CreateFolder("Assets", "NightWatch");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/NightWatch", "Materials");

        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    public static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        else Undo.RegisterCreatedObjectUndo(go, name);
        return go.transform;
    }

    // A primitive shape placed by local position under its parent.
    public static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 localPosition,
        Vector3 scale, Material mat, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // An empty object placed by world position.
    public static Transform Empty(string name, Transform parent, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        return go.transform;
    }

    // 3D text that shrinks to fit its box. Readable when looking along its forward direction.
    public static TextMeshPro Text(string name, Transform parent, Vector3 position, Quaternion rotation,
        Vector2 size, string text, float maxSize, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.05f;
        tmp.fontSizeMax = maxSize;
        tmp.alignment = align;
        tmp.color = new Color(1f, 0.96f, 0.88f);
        tmp.rectTransform.sizeDelta = size;
        return tmp;
    }

    // A flat coloured board behind text. Faces the same way as Text().
    public static GameObject Board(string name, Transform parent, Vector3 center, Quaternion rotation, Vector2 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(center + rotation * Vector3.forward * 0.02f, rotation);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.GetComponent<Renderer>().sharedMaterial = Mat("Board_" + ColorUtility.ToHtmlStringRGB(color), color, true);
        return go;
    }

    // A small label floating over an object, turned to face the middle of the hut.
    public static void Label(string text, Transform parent, Vector3 position)
    {
        var dir = position - new Vector3(0f, position.y, 0f);
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
        Text("Label_" + text, parent, position, Quaternion.LookRotation(dir), new Vector2(0.6f, 0.12f), text, 0.6f,
            TextAlignmentOptions.Center);
    }

    public static void Remove(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Undo.DestroyObjectImmediate(go);
    }
}
