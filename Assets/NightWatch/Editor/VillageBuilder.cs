using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Builds the teammate's Elephant Village (a three.js level, converted by
// tools/village_convert.py) around our game, with our hut at the Unity origin:
//  - the ground as a Unity Terrain (their height grid, their 4 ground textures mixed by their
//    splat map); the paddy field is levelled to height 0 (their terraces stepped down to the
//    south), so our game's objects stand on it. It is the ground teleport area;
//  - the tank (lake) and the flooded paddy plots as one flat water mesh (plots under our farm
//    are drained, see FarmLayout);
//  - houses, granaries, the shed, the temple, the well and haystacks as normal
//    scene objects (marked Static);
//  - every other placed thing (plants, trees, rice, rocks, garden fences, small props) as a
//    PropSet drawn by InstancedProps, with the browser version's distance rules. None are drawn
//    in the farm's open places and crop plots (FarmLayout).
// Village coordinates have +z to the south; Unity has +z to the north. See docs/village-port.md.
// Built by Night Watch > Build Full Game, or alone with Night Watch > Build Village.
// Running it again replaces the old one.
public static class VillageBuilder
{
    const string RootName = "Village";
    const string Folder = "Assets/NightWatch/Village";
    const string Generated = Folder + "/Generated";

    // Our hut stands on this point of the village map (a bund crossing in the paddy field).
    public const float HutX = -42.5f, HutZ = 13.5f;

    // The paddy field on the village map: 8 x 8 terraced plots.
    const float FieldX0 = -98f, FieldX1 = -24f, FieldZ0 = -4f, FieldZ1 = 66f;
    const int FieldRows = 8;

    // Placed as normal scene objects. Everything else is drawn by InstancedProps.
    static readonly HashSet<string> SceneObjects = new HashSet<string>
    {
        "hut_a", "hut_b", "hut_c", "hut_kitchen", "ambalama", "atuwa", "well", "stupa",
        "worn_shed_pbr_game_ready", "haystack_1", "haystack_2",
    };

    public const string FenceModel = "wire_fence";

    // The village's rice clump spots inside each crop plot (FarmLayout.CropPlots), filled by Build.
    // The crop plots are planted on them with this model (ElephantSetup); the village does not draw them.
    public const string CropModel = "rice_gold";
    // Ripe paddy stands about 1 m; the village's clumps are 0.75 to 1 m. Taller makes the eaten
    // gaps and flat patches easier to see from the hut. All rice, so the crop plots still match.
    const float RiceTaller = 1.2f;
    public static readonly Dictionary<string, List<float>> CropRice = new Dictionary<string, List<float>>();

    // Drawing rules, from the browser version (js/props.js) and tuned for the laptop.
    // cull: not drawn past this distance (m); lod: past this distance the model is swapped for the
    // proxy model at proxyScale; thin: past this distance only 1 copy in thinKeep is drawn, made
    // wider by thinScale, so a far field still looks full at a fraction of the cost.
    struct Rule
    {
        public float cull, lod, proxyScale, thin, thinScale;
        public int thinKeep;
        public string proxy;
        public bool shadow;
        public Rule(float cull, bool shadow, float lod = 0f, string proxy = null, float proxyScale = 1f)
        {
            this.cull = cull; this.shadow = shadow; this.lod = lod; this.proxy = proxy; this.proxyScale = proxyScale;
            thin = 0f; thinKeep = 1; thinScale = 1f;
        }
        public Rule Thin(float from, int keep, float scale)
        {
            var r = this;
            r.thin = from; r.thinKeep = keep; r.thinScale = scale;
            return r;
        }
    }

    const float Always = 400f;      // drawn at any distance (the fog still limits it)
    const float SmallCull = 45f;    // small props with no rule (pots, baskets, stools)

    static readonly Dictionary<string, Rule> Rules = new Dictionary<string, Rule>
    {
        { "island_tree_01", new Rule(Always, true, 30f, "jungle_tree_1", 0.55f) },
        { "island_tree_02", new Rule(Always, true, 30f, "jungle_tree_2", 0.5f) },
        { "island_tree_03", new Rule(Always, true, 30f, "jungle_tree_3", 0.38f) },
        { "tree_small_02", new Rule(Always, true, 30f, "jungle_tree_1", 0.5f) },
        { "jacaranda_tree", new Rule(Always, true) },
        { "jungle_tree_1", new Rule(Always, true) }, { "jungle_tree_2", new Rule(Always, true) }, { "jungle_tree_3", new Rule(Always, true) },
        { "palm_1", new Rule(Always, true) }, { "palm_2", new Rule(Always, true) }, { "palm_3", new Rule(Always, true) }, { "palm_4", new Rule(Always, true) },
        { "banana_1", new Rule(110f, true) }, { "banana_2", new Rule(110f, true) },
        { "shrub_01", new Rule(65f, false) }, { "shrub_02", new Rule(70f, false) }, { "shrub_03", new Rule(60f, false) },
        { "shrub_04", new Rule(60f, false) }, { "fern_02", new Rule(60f, false) }, { "nettle_plant", new Rule(55f, false) },
        { "periwinkle_plant", new Rule(45f, false) }, { "weed_plant_02", new Rule(45f, false) },
        { "grass_medium_01", new Rule(50f, false) }, { "grass_medium_02", new Rule(50f, false) }, { "grass_bermuda_01", new Rule(45f, false) },
        { "grass_tuft", new Rule(60f, false).Thin(20f, 2, 1.4f) }, { "grass_tuft_dry", new Rule(60f, false).Thin(20f, 2, 1.4f) }, { "reeds", new Rule(90f, false) },
        { "rice_green", new Rule(85f, false).Thin(25f, 3, 1.6f) }, { "rice_gold", new Rule(85f, false).Thin(25f, 3, 1.6f) },
        { "rock_moss_set_01", new Rule(Always, true) }, { "rock_moss_set_02", new Rule(Always, true) },
        { "rock_07", new Rule(Always, true) }, { "rock_09", new Rule(Always, true) },
        { "tree_stump_01", new Rule(90f, false) }, { "tree_stump_02", new Rule(90f, false) }, { "dead_tree_trunk", new Rule(90f, false) },
        { "fence_stick", new Rule(100f, true) }, { "fence_bamboo", new Rule(100f, true) }, { "gate_wood", new Rule(Always, true) },
    };

    // The four ground textures: file name, tile size (m) and tint, as in the browser's terrain shader
    // (it pushes the dry-season grass scans towards monsoon green; ours a little greener still).
    static readonly (string name, float tile, Color tint)[] GroundLayers =
    {
        ("grass", 3.5f, new Color(0.68f, 1.05f, 0.50f)),
        ("dirt", 2.6f, Color.white),
        ("mud", 3.0f, Color.white),
        ("forest", 3.2f, new Color(0.7f, 0.95f, 0.55f)),
    };

    // level.json (written by tools/village_convert.py), still in village coordinates.
    [Serializable] class Level
    {
        public float half, res, waterLevel;
        public int n;
        public GroupData[] groups;
        public Shape[] colliders, water, steps;
    }
    [Serializable] class GroupData { public string name; public float[] items; }
    [Serializable] class Shape
    {
        public string kind, t;
        public float x, z, y, r, a, b, rot, hx, hz, ry, ax, az, bx, bz, x0, x1, z0, z1;
    }

    // The village's height grid, and the same ground with the paddy field levelled.
    class Ground
    {
        public float[] h;
        public int n;
        public float res, half;

        public float Raw(float x, float z)
        {
            float fx = Mathf.Clamp((x + half) / res, 0f, n - 1.001f);
            float fz = Mathf.Clamp((z + half) / res, 0f, n - 1.001f);
            int i = (int)fx, j = (int)fz;
            float u = fx - i, v = fz - j;
            int o = j * n + i;
            return (h[o] * (1 - u) + h[o + 1] * u) * (1 - v) + (h[o + n] * (1 - u) + h[o + n + 1] * u) * v;
        }

        // Their terraces step down 0.36 m per row; taking the terrace level away leaves the field
        // flat at 0 with its bunds, blending into the land round it as before.
        public float Levelled(float x, float z) => Raw(x, z) - FieldWeight(x, z) * TerraceLevel(z);

        // How far a village-space point must move up or down to stay on the levelled ground.
        public float Shift(float x, float z) => Levelled(x, z) - Raw(x, z);
    }

    static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    static float FieldWeight(float x, float z)
    {
        float dx = Mathf.Max(FieldX0 - x, 0f, x - FieldX1);
        float dz = Mathf.Max(FieldZ0 - z, 0f, z - FieldZ1);
        float gap = Mathf.Sqrt(dx * dx + dz * dz);
        return gap <= 0f ? 1f : 1f - Smooth(0f, 6f, gap);
    }

    static float TerraceLevel(float z)
    {
        float d = (FieldZ1 - FieldZ0) / FieldRows;
        int row = Mathf.FloorToInt((Mathf.Clamp(z, FieldZ0 + 0.01f, FieldZ1 - 0.01f) - FieldZ0) / d);
        return 0.35f - 0.36f * row;
    }

    // Village map point > Unity point (our hut at the origin, +z to the north).
    public static Vector3 ToUnity(float x, float y, float z) => new Vector3(x - HutX, y, HutZ - z);

    [MenuItem("Night Watch/Build Village")]
    static void BuildMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Night Watch", "Stop Play mode first, then build the village.", "OK");
            return;
        }
        Build();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Night Watch: village built. Save the scene with Ctrl+S.");
    }

    public static void Build()
    {
        var level = JsonUtility.FromJson<Level>(File.ReadAllText(Folder + "/World/level.json"));
        var ground = LoadGround(level);
        CropRice.Clear();
        RemovedSpots.Clear();

        EditorKit.Remove(RootName);
        var root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Village");
        Folders();

        BuildTerrain(root, ground, level);
        BuildWater(root, ground, level);
        BuildPlacements(root, ground, level);
        BuildColliders(root, ground, level);
        AssetDatabase.SaveAssets();
    }

    static void Folders()
    {
        if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(Folder, "Generated");
        if (!AssetDatabase.IsValidFolder(Generated + "/Materials")) AssetDatabase.CreateFolder(Generated, "Materials");
    }

    static Ground LoadGround(Level level)
    {
        var bytes = File.ReadAllBytes(Folder + "/World/height.bytes");
        var h = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, h, 0, h.Length * 4);
        if (h.Length != level.n * level.n)
            throw new Exception($"Night Watch: height.bytes has {h.Length} values, expected {level.n * level.n}.");
        return new Ground { h = h, n = level.n, res = level.res, half = level.half };
    }

    // ------------------------------------------------------------------ terrain

    static void BuildTerrain(Transform root, Ground ground, Level level)
    {
        const int res = 1025;     // 0.27 m between height points, so the paddy bunds keep their shape
        const int splatRes = 512;
        float size = 2f * level.half;
        // The terrain's south-west corner in Unity (village x min, village z max).
        var corner = ToUnity(-level.half, 0f, level.half);

        var raw = new float[res, res];
        float lo = float.MaxValue, hi = float.MinValue;
        for (int j = 0; j < res; j++)
        for (int i = 0; i < res; i++)
        {
            float x = corner.x + i * size / (res - 1) + HutX;      // back to village x
            float z = HutZ - (corner.z + j * size / (res - 1));    // back to village z
            float y = ground.Levelled(x, z);
            raw[j, i] = y;
            lo = Mathf.Min(lo, y);
            hi = Mathf.Max(hi, y);
        }
        hi += 0.01f;
        var heights = new float[res, res];
        for (int j = 0; j < res; j++)
        for (int i = 0; i < res; i++)
            heights[j, i] = (raw[j, i] - lo) / (hi - lo);

        string dataPath = Generated + "/VillageTerrain.asset";
        AssetDatabase.DeleteAsset(dataPath);
        var data = new TerrainData { heightmapResolution = res };
        data.size = new Vector3(size, hi - lo, size);
        data.SetHeights(0, 0, heights);

        var layers = new TerrainLayer[GroundLayers.Length];
        for (int l = 0; l < GroundLayers.Length; l++)
        {
            var (name, tile, tint) = GroundLayers[l];
            string layerPath = $"{Generated}/Ground_{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = LoadTexture($"{Folder}/Ground/{name}_diff.jpg", false);
            layer.normalMapTexture = LoadTexture($"{Folder}/Ground/{name}_nor_gl.jpg", true);
            layer.tileSize = new Vector2(tile, tile);
            layer.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f);
            layer.smoothness = name == "mud" ? 0.35f : 0.05f;
            EditorUtility.SetDirty(layer);
            layers[l] = layer;
        }
        data.terrainLayers = layers;

        // Their splat map: R grass, G dirt, B mud, A forest floor; the top row is the north edge.
        var splat = new Texture2D(2, 2);
        splat.LoadImage(File.ReadAllBytes(Folder + "/World/splat.png"));
        data.alphamapResolution = splatRes;
        var mix = new float[splatRes, splatRes, layers.Length];
        for (int b = 0; b < splatRes; b++)
        for (int a = 0; a < splatRes; a++)
        {
            float x = corner.x + (a + 0.5f) * size / splatRes + HutX;
            float z = HutZ - (corner.z + (b + 0.5f) * size / splatRes);
            var c = splat.GetPixelBilinear((x + level.half) / size, 1f - (z + level.half) / size);
            // A little sharper than a straight blend, as in the browser's shader.
            var w = new Vector4(Mathf.Pow(c.r, 1.6f), Mathf.Pow(c.g, 1.6f), Mathf.Pow(c.b, 1.6f), Mathf.Pow(c.a, 1.6f));
            float sum = w.x + w.y + w.z + w.w;
            if (sum < 1e-4f) { w = new Vector4(1f, 0f, 0f, 0f); sum = 1f; }
            w /= sum;
            // Open grass is not one flat colour: big soft patches of drier grass (the forest floor
            // layer) and smaller worn ones (bare dirt). Only the grass share moves, so the paddy plots,
            // bunds and paths keep their own mix. Same 4 layers, so it costs nothing to draw.
            float dry = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.8f, Mathf.PerlinNoise(x * 0.045f + 11f, z * 0.045f + 7f))) * 0.45f;
            float bare = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.85f, Mathf.PerlinNoise(x * 0.13f + 3f, z * 0.13f + 19f))) * 0.2f;
            float toDry = w.x * dry, toBare = w.x * bare;
            w.x -= toDry + toBare;
            w.w += toDry;
            w.y += toBare;
            for (int l = 0; l < 4; l++) mix[b, a, l] = w[l];
        }
        UnityEngine.Object.DestroyImmediate(splat);
        data.SetAlphamaps(0, 0, mix);
        AssetDatabase.CreateAsset(data, dataPath);

        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "Terrain";
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(corner.x, lo, corner.z);
        var terrain = go.GetComponent<Terrain>();
        terrain.heightmapPixelError = 8f;      // fewer triangles far away
        terrain.basemapDistance = 80f;         // past this, a cheap pre-mixed texture
        terrain.drawInstanced = true;
        terrain.drawTreesAndFoliage = false;   // our own InstancedProps draws the plants
        go.isStatic = true;

        // The whole ground is a teleport target while the player is on it (switched on at run time
        // by PlayAreaLimit). The starter rig's pointer only accepts the "Teleport" interaction layer.
        var teleport = go.AddComponent<TeleportationArea>();
        teleport.interactionLayers = InteractionLayerMask.GetMask("Teleport");
        teleport.enabled = false;
    }

    static Texture2D LoadTexture(string path, bool normalMap)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"Night Watch: missing ground texture {path}"); return null; }
        var type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type || importer.maxTextureSize > 1024 || importer.anisoLevel != 4)
        {
            importer.textureType = type;
            importer.maxTextureSize = Mathf.Min(importer.maxTextureSize, 1024);
            importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------------ water

    // One flat mesh: the tank (an ellipse) and the flooded paddy plots (rectangles).
    static readonly Color WaterColor = new Color(0.20f, 0.45f, 0.60f);

    static void BuildWater(Transform root, Ground ground, Level level)
    {
        var points = new List<Vector3>();
        var tris = new List<int>();
        foreach (var w in level.water)
        {
            if (w.kind == "tank")
            {
                float y = w.y + ground.Shift(w.x, w.z);
                int centre = points.Count;
                points.Add(ToUnity(w.x, y, w.z));
                const int steps = 64;
                float c = Mathf.Cos(w.rot), s = Mathf.Sin(w.rot);
                for (int k = 0; k < steps; k++)
                {
                    float t = k * Mathf.PI * 2f / steps;
                    float u = Mathf.Cos(t) * w.a, v = Mathf.Sin(t) * w.b;
                    points.Add(ToUnity(w.x + u * c - v * s, y, w.z + u * s + v * c));
                    tris.AddRange(new[] { centre, centre + 1 + k, centre + 1 + (k + 1) % steps });
                }
            }
            else
            {
                var a = ToUnity(w.x0, 0f, w.z0);
                var b = ToUnity(w.x1, 0f, w.z1);
                if (FarmLayout.Drained(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z))))
                    continue;
                float y = w.y + ground.Shift((w.x0 + w.x1) / 2f, (w.z0 + w.z1) / 2f);
                int o = points.Count;
                points.Add(ToUnity(w.x0, y, w.z0));
                points.Add(ToUnity(w.x1, y, w.z0));
                points.Add(ToUnity(w.x1, y, w.z1));
                points.Add(ToUnity(w.x0, y, w.z1));
                tris.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }
        }
        // The z flip turns faces over: make every triangle face up.
        for (int k = 0; k < tris.Count; k += 3)
        {
            var n = Vector3.Cross(points[tris[k + 1]] - points[tris[k]], points[tris[k + 2]] - points[tris[k]]);
            if (n.y < 0f) { int t = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = t; }
        }

        string meshPath = Generated + "/Water.asset";
        AssetDatabase.DeleteAsset(meshPath);
        var mesh = new Mesh { name = "VillageWater" };
        mesh.SetVertices(points);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, meshPath);

        // Lake blue (the old near-black water only mirrored the sunset sky and looked brown).
        var mat = EditorKit.Mat("VillageWater", WaterColor);
        mat.SetColor("_BaseColor", WaterColor);
        mat.SetFloat("_Smoothness", 0.8f);
        EditorUtility.SetDirty(mat);

        var go = new GameObject("Water");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
    }

    // ------------------------------------------------------------------ placed models

    static void BuildPlacements(Transform root, Ground ground, Level level)
    {
        var sceneRoot = EditorKit.Group("Buildings", root);
        var groups = new List<PropSet.Group>();
        int sceneCount = 0, cleared = 0;

        foreach (var g in level.groups)
        {
            // The farm's electric fence is built from this model by ElephantSetup (FarmLayout.FenceLines);
            // the village's own runs are left out (their south fence is not wanted).
            if (g.name == FenceModel) continue;
            var model = LoadModel(g.name);
            if (model == null) continue;
            var poses = Poses(g.items, ground);
            int count = poses.Length / PropSet.PoseSize;

            if (SceneObjects.Contains(g.name))
            {
                for (int k = 0; k < count; k++)
                {
                    // The village's pose goes on a parent, so the model keeps its own size and offset
                    // (the shed's file shrinks it to 0.007, the wire fence's to 0.87).
                    int o = k * PropSet.PoseSize;
                    var placed = new GameObject(count > 1 ? $"{g.name}_{k + 1}" : g.name).transform;
                    placed.SetParent(sceneRoot, false);
                    placed.SetPositionAndRotation(new Vector3(poses[o], poses[o + 1], poses[o + 2]),
                        Quaternion.Euler(0f, poses[o + 3], 0f));
                    placed.localScale = new Vector3(poses[o + 4], poses[o + 5], poses[o + 6]);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(model, placed);
                    go.transform.localPosition = model.transform.localPosition;
                    go.transform.localRotation = model.transform.localRotation;
                    go.transform.localScale = model.transform.localScale;
                    if (g.name.StartsWith("haystack")) LightenThatch(go, g.name);
                    foreach (var t in placed.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
                    sceneCount++;
                }
                continue;
            }

            if (g.name.StartsWith("rice_"))
            {
                for (int o = 0; o < poses.Length; o += PropSet.PoseSize) poses[o + 5] *= RiceTaller;
                KeepCropRice(poses);
            }
            var rule = Rules.TryGetValue(g.name, out var r) ? r : new Rule(SmallCull, false);
            // Heavy trees (the ones with a cheap far stand-in) are drawn in full near the player.
            poses = ClearFarm(poses, rule.proxy != null ? FarmLayout.NoHeavyTrees : 0f, ref cleared);
            count = poses.Length / PropSet.PoseSize;
            if (count == 0) continue;
            float size = ModelSize(model) * MaxScale(poses);
            var parts = Parts(model, g.name);
            groups.Add(new PropSet.Group
            {
                name = g.name,
                parts = parts,
                shadows = rule.shadow,
                near = 0f,
                far = rule.lod > 0f ? rule.lod : rule.thin > 0f ? rule.thin : rule.cull,
                size = size,
                poses = poses,
            });

            // Far, thinned copies: every thinKeep-th copy, wider (not taller).
            if (rule.thin > 0f)
            {
                var thin = new List<float>();
                for (int k = 0; k < count; k += rule.thinKeep)
                {
                    for (int s = 0; s < PropSet.PoseSize; s++) thin.Add(poses[k * PropSet.PoseSize + s]);
                    int o = thin.Count - PropSet.PoseSize;
                    thin[o + 4] *= rule.thinScale;
                    thin[o + 6] *= rule.thinScale;
                }
                groups.Add(new PropSet.Group
                {
                    name = g.name + " (far, thinned)",
                    parts = parts,
                    shadows = rule.shadow,
                    near = rule.thin,
                    far = rule.cull,
                    size = size * rule.thinScale,
                    poses = thin.ToArray(),
                });
            }

            // Far stand-ins for heavy trees: the same spots, a cheap jungle tree.
            if (rule.proxy != null)
            {
                var proxy = LoadModel(rule.proxy);
                if (proxy == null) continue;
                var far = (float[])poses.Clone();
                for (int k = 0; k < count; k++)
                    for (int s = 4; s < 7; s++) far[k * PropSet.PoseSize + s] *= rule.proxyScale;
                groups.Add(new PropSet.Group
                {
                    name = g.name + " (far)",
                    parts = Parts(proxy, rule.proxy),
                    shadows = true,
                    near = rule.lod,
                    far = Always,
                    size = ModelSize(proxy) * MaxScale(far),
                    poses = far,
                });
            }
        }

        string setPath = Generated + "/VillageProps.asset";
        AssetDatabase.DeleteAsset(setPath);
        var set = ScriptableObject.CreateInstance<PropSet>();
        set.groups = groups.ToArray();
        AssetDatabase.CreateAsset(set, setPath);

        var plants = new GameObject("Plants");
        plants.transform.SetParent(root, false);
        var drawer = plants.AddComponent<InstancedProps>();
        drawer.set = set;
        drawer.cellSize = 48f;
        Debug.Log($"Night Watch: village has {sceneCount} buildings and {set.Copies} drawn copies in {groups.Count} groups " +
                  $"({cleared} copies left out of the farm's open places and crop plots).");
    }

    public static GameObject LoadModel(string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Models/{name}.glb");
        if (model == null) Debug.LogWarning($"Night Watch: no model {name}.glb in {Folder}/Models");
        return model;
    }

    // The village's 5 numbers per copy (x, y, z, turn in radians, scale) > our 7 (Unity
    // position, yaw in degrees, scale x, y, z). A negative scale stretches a fence run along x.
    static float[] Poses(float[] items, Ground ground)
    {
        int count = items.Length / 5;
        var poses = new float[count * PropSet.PoseSize];
        for (int k = 0; k < count; k++)
        {
            float x = items[k * 5], y = items[k * 5 + 1], z = items[k * 5 + 2], ry = items[k * 5 + 3], s = items[k * 5 + 4];
            var p = ToUnity(x, y + ground.Shift(x, z), z);
            int o = k * PropSet.PoseSize;
            poses[o] = p.x;
            poses[o + 1] = p.y;
            poses[o + 2] = p.z;
            // glTFast already mirrors each model from glTF to Unity; with our z flip this is a turn.
            poses[o + 3] = 180f - ry * Mathf.Rad2Deg;
            poses[o + 4] = Mathf.Abs(s);
            poses[o + 5] = s < 0f ? 1f : s;
            poses[o + 6] = s < 0f ? 1f : s;
        }
        return poses;
    }

    // Rice clumps (green or gold) standing in a crop plot, not on its paths.
    static void KeepCropRice(float[] poses)
    {
        for (int o = 0; o < poses.Length; o += PropSet.PoseSize)
        {
            var p = new Vector3(poses[o], 0f, poses[o + 2]);
            if (FarmLayout.IsOpen(p)) continue;
            foreach (var (name, area) in FarmLayout.CropPlots)
            {
                if (!area.Contains(new Vector2(p.x, p.z))) continue;
                if (!CropRice.TryGetValue(name, out var list)) CropRice[name] = list = new List<float>();
                for (int s = 0; s < PropSet.PoseSize; s++) list.Add(poses[o + s]);
            }
        }
    }

    // Leaves out the copies that stand in the farm's open places and crop plots, and any closer to
    // the hut than keepAway (m).
    static float[] ClearFarm(float[] poses, float keepAway, ref int cleared)
    {
        var kept = new List<float>(poses.Length);
        for (int o = 0; o < poses.Length; o += PropSet.PoseSize)
        {
            var p = new Vector3(poses[o], 0f, poses[o + 2]);
            if (FarmLayout.ClearOfPlants(p) || p.magnitude < keepAway)
            {
                cleared++;
                RemovedSpots.Add(new Vector2(p.x, p.z));
                continue;
            }
            for (int s = 0; s < PropSet.PoseSize; s++) kept.Add(poses[o + s]);
        }
        return kept.ToArray();
    }

    // ------------------------------------------------------------------ colliders

    // Copies left out of the farm (ClearFarm); their collision shapes are left out too.
    static readonly List<Vector2> RemovedSpots = new List<Vector2>();

    // Only near the farm: the walk map (ElephantSetup) covers x -75..65, z -75..55.
    static readonly Rect ColliderArea = Rect.MinMaxRect(-78f, -78f, 68f, 58f);
    const float ColliderHeight = 4f;

    // The village's collision shapes (world.json "colliders": circles for trunks, haystacks and
    // granaries, boxes for houses, thin segments for garden fences) as invisible static colliders,
    // so the NavMesh bake makes elephants walk round them. Left out: shapes of copies the farm
    // cleared away, and their wire fence (ours is built by ElephantSetup, FenceSection).
    static void BuildColliders(Transform root, Ground ground, Level level)
    {
        var parent = EditorKit.Group("Colliders", root);
        int made = 0, skipped = 0;
        foreach (var c in level.colliders)
        {
            GameObject go;
            if (c.t == "s")
            {
                // Their wire fence ran along x = -101 and z = 69.
                if ((Mathf.Abs(c.ax + 101f) < 0.6f && Mathf.Abs(c.bx + 101f) < 0.6f) ||
                    (Mathf.Abs(c.az - 69f) < 0.6f && Mathf.Abs(c.bz - 69f) < 0.6f)) { skipped++; continue; }
                var a = ToUnity(c.ax, 0f, c.az);
                var b = ToUnity(c.bx, 0f, c.bz);
                var mid = (a + b) / 2f;
                if (!InArea(mid)) continue;
                go = ColliderAt(parent, "Fence", mid, ground, Quaternion.LookRotation(b - a));
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, ColliderHeight / 2f, 0f);
                box.size = new Vector3(2f * c.r, ColliderHeight, Vector3.Distance(a, b) + 2f * c.r);
            }
            else if (c.t == "b")
            {
                var at = ToUnity(c.x, 0f, c.z);
                if (!InArea(at)) continue;
                // Their turn is about +y with z to the south; with our z flip it is the other way.
                go = ColliderAt(parent, "Box", at, ground, Quaternion.Euler(0f, -c.ry * Mathf.Rad2Deg, 0f));
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, ColliderHeight / 2f, 0f);
                box.size = new Vector3(2f * c.hx, ColliderHeight, 2f * c.hz);
            }
            else
            {
                var at = ToUnity(c.x, 0f, c.z);
                if (!InArea(at)) continue;
                if (WasRemoved(at)) { skipped++; continue; }
                go = ColliderAt(parent, "Round", at, ground, Quaternion.identity);
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.radius = c.r;
                capsule.height = Mathf.Max(ColliderHeight, 2f * c.r + 0.1f);
                capsule.center = new Vector3(0f, capsule.height / 2f, 0f);
            }
            made++;
        }
        Debug.Log($"Night Watch: {made} village colliders near the farm ({skipped} left out: cleared copies and their wire fence).");
    }

    static bool InArea(Vector3 p) => ColliderArea.Contains(new Vector2(p.x, p.z));

    static bool WasRemoved(Vector3 p)
    {
        var q = new Vector2(p.x, p.z);
        foreach (var r in RemovedSpots)
            if ((r - q).sqrMagnitude < 0.25f) return true;
        return false;
    }

    // An empty, static object on the village ground (at a Unity point), turned as given.
    static GameObject ColliderAt(Transform parent, string name, Vector3 at, Ground ground, Quaternion rotation)
    {
        float vx = at.x + HutX, vz = HutZ - at.z;   // back to village coordinates
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(new Vector3(at.x, ground.Levelled(vx, vz) - 0.5f, at.z), rotation);
        go.isStatic = true;
        return go;
    }

    static float MaxScale(float[] poses)
    {
        float m = 1f;
        for (int o = 0; o < poses.Length; o += PropSet.PoseSize)
            m = Mathf.Max(m, poses[o + 4], poses[o + 5], poses[o + 6]);
        return m;
    }

    // Every mesh part of a model, with its material copied so GPU instancing can be switched on.
    public static PropSet.Part[] Parts(GameObject model, string name)
    {
        var parts = new List<PropSet.Part>();
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mats = r.sharedMaterials;
            for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
            {
                var source = mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null;
                if (source == null) continue;
                parts.Add(new PropSet.Part
                {
                    mesh = mf.sharedMesh,
                    submesh = s,
                    material = InstancedCopy(source, $"{name}_{parts.Count}"),
                    // Includes the model file's own top-level size and offset.
                    local = r.transform.localToWorldMatrix,
                });
            }
        }
        return parts.ToArray();
    }

    // The haystacks' straw, lighter and more golden: from the drone shots the thatch texture
    // blurs into one colour, which was a dull brown ("plain brown eggs").
    static readonly Color ThatchTint = new Color(1.3f, 1.25f, 1.1f);

    static void LightenThatch(GameObject haystack, string model)
    {
        foreach (var r in haystack.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.StartsWith("thatch"))
                    mats[i] = TintedCopy(mats[i], $"{model}_thatch", ThatchTint);
            r.sharedMaterials = mats;
        }
    }

    // A rough copy of a model's material (glTF materials use roughnessFactor, ours _Smoothness).
    public static Material RoughCopy(Material source, string name, float roughness)
    {
        var mat = InstancedCopy(source, name);
        if (mat.HasProperty("roughnessFactor")) mat.SetFloat("roughnessFactor", roughness);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f - roughness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // A tinted copy of a village material (glTF materials use baseColorFactor, ours _BaseColor).
    public static Material TintedCopy(Material source, string name, Color tint)
    {
        var mat = InstancedCopy(source, name);
        string property = mat.HasProperty("baseColorFactor") ? "baseColorFactor" : "_BaseColor";
        if (mat.HasProperty(property)) mat.SetColor(property, source.GetColor(property) * tint);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material InstancedCopy(Material source, string name)
    {
        string path = $"{Generated}/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(source) { name = name };
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = source.shader;
            mat.CopyPropertiesFromMaterial(source);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    static float ModelSize(GameObject model)
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var b = mf.sharedMesh.bounds;
            var m = mf.transform.localToWorldMatrix;
            foreach (var corner in new[] { b.min, b.max, new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, b.min.y, b.max.z) })
            {
                var p = m.MultiplyPoint3x4(corner);
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else bounds.Encapsulate(p);
            }
        }
        return any ? bounds.extents.magnitude : 1f;
    }
}
