using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The sky's parts in the scene: a ring of low-poly clouds (one merged mesh, one draw call) and
// a realtime reflection probe that sees only the sky. DayNightCycle colours and moves them.
// Build Full Game runs this after the managers (it needs the DayNightCycle).
public static class SkySetup
{
    const string MeshPath = "Assets/NightWatch/Village/Generated/SkyClouds.asset";
    const string CloudMaterialName = "SkyCloud";

    // The ring: far beyond the fog and the village (the world is 280 m across), inside both
    // cameras' far planes (game 1000 m, film 800 m, the film's farthest shot is 120 m out).
    const int CloudCount = 22;
    const float RingNear = 320f, RingFar = 440f;
    const float LowCloud = 70f, HighCloud = 130f;

    public static void Build(Transform game)
    {
        var root = EditorKit.Group("Sky", game);
        var clouds = BuildClouds(root);
        var probe = BuildReflection(root);

        var cycle = Object.FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
        if (cycle == null)
        {
            Debug.LogWarning("Night Watch: no DayNightCycle in the scene; the sky will not change with the time of day.");
            return;
        }
        cycle.clouds = clouds;
        cycle.skyReflection = probe;
        EditorUtility.SetDirty(cycle);
    }

    static Renderer BuildClouds(Transform root)
    {
        var go = new GameObject("Clouds");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = CloudMesh();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CloudMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return renderer;
    }

    static Material CloudMaterial()
    {
        string path = $"{EditorKit.MaterialFolder}/{CloudMaterialName}.mat";
        var shader = Shader.Find("NightWatch/Cloud");
        if (shader == null)
        {
            Debug.LogWarning("Night Watch: shader NightWatch/Cloud not found (Assets/NightWatch/Shaders).");
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Sees only the sky (culling mask: nothing), small and redrawn by script only.
    static ReflectionProbe BuildReflection(Transform root)
    {
        var go = new GameObject("SkyReflection");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(0f, 5f, 0f);
        var probe = go.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.resolution = 64;
        probe.cullingMask = 0;
        probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        probe.size = new Vector3(1000f, 400f, 1000f);
        probe.boxProjection = false;
        probe.hdr = true;
        probe.importance = 1;
        return probe;
    }

    // ---------------- the cloud mesh ----------------

    // Fair-weather cumulus: each cloud is 4 to 7 lumpy, flat-bottomed puffs in a row, turned
    // so its long side faces the village. Flat shading (each face its own normal) for the
    // low-poly look. A fixed seed keeps the same sky on every build.
    static Mesh CloudMesh()
    {
        var (sphereVerts, sphereTris) = Icosphere();
        var random = new System.Random(4231);
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        for (int c = 0; c < CloudCount; c++)
        {
            float angle = (c + Range(-0.3f, 0.3f)) * 360f / CloudCount;
            float distance = Range(RingNear, RingFar);
            var turn = Quaternion.Euler(0f, angle, 0f);
            var center = turn * new Vector3(0f, Range(LowCloud, HighCloud), distance);
            // Local x along the ring (sideways to the village), so the long side faces it.
            var facing = turn;

            float width = Range(40f, 75f);
            int puffs = random.Next(4, 8);
            for (int p = 0; p < puffs; p++)
            {
                float along = puffs == 1 ? 0f : Mathf.Lerp(-0.5f, 0.5f, p / (puffs - 1f)) * width;
                // Middle puffs are the biggest and tallest.
                float middle = 1f - Mathf.Abs(along) / (width * 0.6f);
                float r = Range(9f, 13f) * Mathf.Lerp(0.6f, 1.2f, middle);
                var offset = new Vector3(along + Range(-3f, 3f), r * 0.25f * middle, Range(-5f, 5f));
                var scale = new Vector3(r * Range(1.1f, 1.5f), r * Range(0.7f, 0.95f), r * Range(0.9f, 1.2f));
                AddPuff(sphereVerts, sphereTris, center + facing * offset, facing, scale, random, vertices, normals, triangles);
            }
        }

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "SkyClouds" };
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }
        mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    static void AddPuff(List<Vector3> sphereVerts, List<int> sphereTris, Vector3 center, Quaternion turn, Vector3 scale,
        System.Random random, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
    {
        // Lumpy: each corner of the ball pushed in or out a little; a flat bottom.
        var corners = new Vector3[sphereVerts.Count];
        for (int i = 0; i < corners.Length; i++)
        {
            var v = sphereVerts[i] * (0.85f + 0.3f * (float)random.NextDouble());
            v.y = Mathf.Max(v.y, -0.3f);
            corners[i] = center + turn * Vector3.Scale(v, scale);
        }
        for (int t = 0; t < sphereTris.Count; t += 3)
        {
            var a = corners[sphereTris[t]];
            var b = corners[sphereTris[t + 1]];
            var c = corners[sphereTris[t + 2]];
            var n = Vector3.Cross(b - a, c - a).normalized;
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }

    // A ball of 80 triangles: an icosahedron with each face split in four.
    static (List<Vector3>, List<int>) Icosphere()
    {
        float g = (1f + Mathf.Sqrt(5f)) / 2f;
        var verts = new List<Vector3>
        {
            new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
            new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
            new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
        };
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
        var tris = new List<int>
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        var middles = new Dictionary<long, int>();
        int Middle(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (middles.TryGetValue(key, out int index)) return index;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            middles[key] = verts.Count - 1;
            return verts.Count - 1;
        }

        var split = new List<int>();
        for (int t = 0; t < tris.Count; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
            split.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        return (verts, split);
    }
}
