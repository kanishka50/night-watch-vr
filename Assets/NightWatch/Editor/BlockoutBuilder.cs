using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Builds our coded shapes (blockout) in the open scene: the watch hut (pela) with its ladder.
// The neighbour lives in a village house (FarmLayout.NeighbourHouse, NeighbourSetup). North is +Z. The ground, trees and the rest of the village come from VillageBuilder, which
// must run first. The fence and crop plots are built by ElephantSetup.
// Called by Night Watch > Build Full Game (GameSetup). Running it again replaces the old one.
public static class BlockoutBuilder
{
    const string RootName = "Blockout";

    public const float HutFloorHeight = 2.5f;
    public const float HutFloorTop = HutFloorHeight + 0.075f;
    public const float HutSize = 3.2f;
    // The ladder stands just south of the hut's open side; its poles rise this far above the floor.
    public const float LadderZ = -HutSize / 2f - 0.2f;
    public const float LadderAboveFloor = 0.9f;

    public static void Build(bool ask)
    {
        // Anything made in Play mode is lost when Play stops.
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Night Watch", "Stop Play mode first, then build the blockout.", "OK");
            return;
        }

        var old = GameObject.Find(RootName);
        if (old != null)
        {
            if (ask && !EditorUtility.DisplayDialog("Night Watch", "Replace the existing Blockout?", "Replace", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(old);
        }

        // The VR template's 10 x 10 m plane is replaced by the village ground.
        var templatePlane = GameObject.Find("Plane");
        if (templatePlane != null)
        {
            Undo.RecordObject(templatePlane, "Hide template plane");
            templatePlane.SetActive(false);
        }

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Blockout");

        BuildHut(root.transform);

        foreach (var t in root.GetComponentsInChildren<Transform>())
            t.gameObject.isStatic = true;

        // The hut shakes and can collapse, so it must not be static (static objects cannot move).
        var hut = root.transform.Find("WatchHut");
        foreach (var t in hut.GetComponentsInChildren<Transform>())
            t.gameObject.isStatic = false;
        hut.gameObject.AddComponent<HutDamage>();

        PlacePlayer();

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("Night Watch: blockout built. Save the scene with Ctrl+S.");
    }

    // A pela, the traditional Sri Lankan watch hut: rough round poles lashed together, a floor of
    // old planks, stick railings, and a two-slope roof thatched with thick illuk grass (straw-like),
    // open on the south side where the ladder is. Every pole leans a little and
    // every thatch row is a little crooked, so it looks hand-made and old, like the village's
    // houses. Sizes and part names stay as before (HutDamage, the props and the floor teleport
    // use them): Floor, Rail_North, Rail_East, Rail_West, Roof, Ladder.
    static void BuildHut(Transform root)
    {
        var parent = Group("WatchHut", root);
        var pole = Mat("HutPole", new Color(0.40f, 0.31f, 0.22f));
        var planks = Mat("HutFloor", new Color(0.45f, 0.35f, 0.25f));
        var thatch = Mat("PelaThatch", new Color(0.78f, 0.64f, 0.40f));
        var random = new System.Random(7);
        float Jitter(float amount) => (float)(random.NextDouble() * 2.0 - 1.0) * amount;

        float half = HutSize / 2f - 0.1f;
        float floorTop = HutFloorTop;
        float eave = floorTop + 2.25f;

        // Four corner poles from the ground to the roof, each leaning a little.
        foreach (var x in new[] { -half, half })
        foreach (var z in new[] { -half, half })
            Pole("Post", parent, new Vector3(x, 0f, z), new Vector3(x + Jitter(0.06f), eave, z + Jitter(0.06f)), 0.2f, pole);

        // Under the floor: two logs that carry it, and crossed braces on the east and west sides.
        foreach (var z in new[] { -half, half })
            Pole("FloorLog", parent, new Vector3(-half - 0.2f, HutFloorHeight - 0.15f, z), new Vector3(half + 0.2f, HutFloorHeight - 0.15f, z), 0.16f, pole);
        foreach (var x in new[] { -half, half })
        {
            Pole("Brace", parent, new Vector3(x, 0.4f, -half), new Vector3(x, HutFloorHeight - 0.25f, half), 0.08f, pole);
            Pole("Brace", parent, new Vector3(x, 0.4f, half), new Vector3(x, HutFloorHeight - 0.25f, -half), 0.08f, pole);
        }

        // In the hut, the floor is the only place the player can teleport to.
        var floor = Box("Floor", parent, new Vector3(0f, HutFloorHeight, 0f), new Vector3(HutSize, 0.15f, HutSize), planks);
        // The starter rig's teleport pointer only accepts surfaces on the "Teleport" interaction layer.
        var teleportArea = floor.AddComponent<TeleportationArea>();
        teleportArea.interactionLayers = InteractionLayerMask.GetMask("Teleport");

        // Stick railings on three sides: a top rail, a middle rail and a few upright sticks.
        Railing("Rail_North", parent, new Vector3(-half, 0f, half), new Vector3(half, 0f, half), floorTop, pole, Jitter);
        Railing("Rail_East", parent, new Vector3(half, 0f, -half), new Vector3(half, 0f, half), floorTop, pole, Jitter);
        Railing("Rail_West", parent, new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), floorTop, pole, Jitter);

        BuildRoof(parent, half, eave, pole, thatch, Jitter);

        // A pole ladder: two side poles and stick rungs. The poles rise about 0.9 m above the floor,
        // as on a real pela ladder (held when stepping on and off), with a last rung at the top.
        // Pointing at the ladder climbs it (HutPropsSetup: LadderTop, LadderBottom).
        var ladder = Group("Ladder", parent);
        float ladderZ = LadderZ;
        Pole("Rail_L", ladder, new Vector3(-0.27f, 0f, ladderZ), new Vector3(-0.25f, HutFloorTop + LadderAboveFloor, ladderZ), 0.08f, pole);
        Pole("Rail_R", ladder, new Vector3(0.27f, 0f, ladderZ), new Vector3(0.25f, HutFloorTop + LadderAboveFloor, ladderZ), 0.08f, pole);
        // Rungs evenly spaced (about 0.3 m) from knee height to just under the pole tops, so there
        // is no gap where the ladder passes the floor. Those above the floor are "TopRung" (they
        // light up with the poles when the player points at the ladder to climb down).
        float lowest = 0.3f, highest = HutFloorTop + LadderAboveFloor - 0.08f;
        int gaps = Mathf.Max(1, Mathf.RoundToInt((highest - lowest) / 0.3f));
        for (int i = 0; i <= gaps; i++)
        {
            float y = Mathf.Lerp(lowest, highest, i / (float)gaps);
            float tilt = Jitter(0.02f);
            Pole(y > HutFloorTop ? "TopRung" : "Rung", ladder, new Vector3(-0.3f, y + tilt, ladderZ), new Vector3(0.3f, y - tilt, ladderZ), 0.045f, pole);
        }
    }

    // One side's railing between two floor corners (their y is ignored).
    static void Railing(string name, Transform parent, Vector3 a, Vector3 b, float floorTop, Material pole, System.Func<float, float> jitter)
    {
        var rail = Group(name, parent);
        Pole("TopRail", rail, a + Vector3.up * (floorTop + 0.88f + jitter(0.03f)), b + Vector3.up * (floorTop + 0.88f + jitter(0.03f)), 0.08f, pole);
        Pole("MidRail", rail, a + Vector3.up * (floorTop + 0.45f + jitter(0.03f)), b + Vector3.up * (floorTop + 0.45f + jitter(0.03f)), 0.05f, pole);
        for (int i = 1; i <= 3; i++)
        {
            var at = Vector3.Lerp(a, b, i / 4f);
            Pole("Stick", rail, at + Vector3.up * floorTop, at + Vector3.up * (floorTop + 0.95f) + new Vector3(jitter(0.03f), 0f, jitter(0.03f)), 0.045f, pole);
        }
    }

    // The roof: eave beams and a ridge pole on the posts, and on them one thick, rounded mass of
    // illuk grass thatch (the straw-like grass farmers cut to roof a pela): about 25 to 35 cm deep,
    // lumpy, with soft rounded eaves, gentle bumps where the bundles overlap, and straw underneath
    // too (no gaps, nothing flat or wooden). On top, loose straw strands; at the eaves, straw ends
    // hanging; along the ridge, a fat straw roll tied with coir.
    // The ridge runs east-west, so the slopes face north and south (the low eaves frame the view).
    static void BuildRoof(Transform parent, float half, float eave, Material pole, Material thatch, System.Func<float, float> jitter)
    {
        var roof = Group("Roof", parent);
        // Steep and full, like a haystack: the thatch swells out in the middle of each slope.
        const float rise = 1.2f, overhang = 0.6f;
        float ridgeY = eave + rise;
        float length = HutSize + 0.9f;

        foreach (var z in new[] { -half, half })
            Pole("EaveBeam", roof, new Vector3(-half - 0.25f, eave, z), new Vector3(half + 0.25f, eave, z), 0.11f, pole);
        foreach (var x in new[] { -half, half })
        {
            Pole("TieBeam", roof, new Vector3(x, eave, -half - 0.1f), new Vector3(x, eave, half + 0.1f), 0.1f, pole);
            Pole("KingPost", roof, new Vector3(x, eave, 0f), new Vector3(x, ridgeY, 0f), 0.09f, pole);
            Pole("Rafter", roof, new Vector3(x, ridgeY, 0f), new Vector3(x, eave - 0.05f, -half - 0.1f), 0.07f, pole);
            Pole("Rafter", roof, new Vector3(x, ridgeY, 0f), new Vector3(x, eave - 0.05f, half + 0.1f), 0.07f, pole);
        }
        Pole("RidgePole", roof, new Vector3(-half - 0.3f, ridgeY, 0f), new Vector3(half + 0.3f, ridgeY, 0f), 0.11f, pole);

        var mass = new GameObject("Thatch");
        mass.transform.SetParent(roof, false);
        mass.AddComponent<MeshFilter>().sharedMesh = ThatchMesh(ridgeY, half, rise, overhang, length, out float top);
        var renderer = mass.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = thatch;

        // Loose straw on top and straw ends at the eaves, following the slopes.
        var shell = StrawShellMaterial();
        var fringe = StrawFringeMaterial(length);
        float angle = Mathf.Atan2(rise, half);
        float slope = (half + overhang) / Mathf.Cos(angle);
        foreach (var side in new[] { -1f, 1f })
        {
            var down = new Vector3(0f, -Mathf.Sin(angle), side * Mathf.Cos(angle));
            var outward = new Vector3(0f, Mathf.Cos(angle), side * Mathf.Sin(angle));
            var card = Quaternion.LookRotation(outward, -down);
            for (int band = 0; band < 6; band++)
            {
                float s = 0.35f + band * (slope - 0.6f) / 5f;
                for (int layer = 1; layer <= 2; layer++)
                    StrawCard("LooseStraw", roof, ThatchCentre(ridgeY, angle, side, s) + outward * (ThatchDepth(s, slope) / 2f + ThatchSwell(s, slope) + 0.05f + 0.025f * layer),
                        card, new Vector3(length - 1.4f, 0.6f, 1f), shell);
            }
            StrawCard("StrawEnds", roof, ThatchCentre(ridgeY, angle, side, slope) + down * 0.2f - outward * 0.05f, card,
                new Vector3(length - 1.2f, 0.36f, 1f), fringe);
        }

        // The ridge: a fat roll of straw on the top of the thatch, tied every 0.7 m.
        float roll = length / 2f - 0.7f;   // the ends round down, so the roll stops short of them
        Pole("RidgeRoll", roof, new Vector3(-roll, top - 0.06f, 0f), new Vector3(roll, top - 0.06f, 0f), 0.44f, thatch);
        var tie = Mat("RoofTie", new Color(0.30f, 0.22f, 0.14f));
        for (float x = -roll + 0.35f; x < roll; x += 0.7f)
            Pole("RidgeTie", roof, new Vector3(x - 0.02f, top - 0.06f, 0f), new Vector3(x + 0.02f, top - 0.06f, 0f), 0.46f, tie);
    }

    // The middle of the thatch's thickness, s metres down a slope from the ridge (y, z; x = 0).
    static Vector3 ThatchCentre(float ridgeY, float angle, float side, float s)
    {
        float depth = ThatchDepth(s, 9f);
        return new Vector3(0f, ridgeY + 0.08f + depth / 2f - s * Mathf.Sin(angle), side * s * Mathf.Cos(angle));
    }

    // Thick at the ridge, a little thinner at the eaves.
    static float ThatchDepth(float s, float slope) => Mathf.Lerp(0.46f, 0.26f, Mathf.Clamp01(s / slope));

    // The fullness: the thatch swells out about 20 cm in the middle of each slope (none at the
    // ridge or the eave), so the roof looks rounded and filled, not flat.
    static float ThatchSwell(float s, float slope) => 0.2f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(s / slope)), 0.8f);

    const string ThatchMeshPath = "Assets/NightWatch/Meshes/PelaThatch.asset";

    // The thatch body: a two-slope band of straw, rounded at the eaves, lumpy on top (bundles and
    // a little randomness), extruded along the ridge (x) and closed at both ends. UVs in metres for
    // the thatch photo. top = the height of its upper surface at the ridge.
    static Mesh ThatchMesh(float ridgeY, float half, float rise, float overhang, float length, out float top)
    {
        float angle = Mathf.Atan2(rise, half);
        float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
        float slope = (half + overhang) / cos;
        const int perSide = 22;      // points down each slope
        const int tip = 6;           // points round each rounded eave
        const float step = 0.2f;     // between cross-sections along the ridge
        int rings = Mathf.CeilToInt(length / step) + 1;

        // The cross-section, south eave to north eave: (side, distance down the slope).
        var profile = new List<(float side, float s)>();
        for (int i = perSide - 1; i >= 0; i--) profile.Add((-1f, slope * i / (perSide - 1)));
        for (int i = 1; i < perSide; i++) profile.Add((1f, slope * i / (perSide - 1)));
        int count = profile.Count;

        Vector3 Out(float side) => new Vector3(0f, cos, side * sin);
        Vector3 Down(float side) => new Vector3(0f, -sin, side * cos);
        Vector3 Centre(float x, float side, float s)
        {
            var c = ThatchCentre(ridgeY, angle, side, s);
            c.y += ThatchDepth(s, slope) / 2f - ThatchDepth(s, 9f) / 2f;
            c.x = x;
            return c;
        }
        // Lumps: a bump at each overlapping bundle row, plus soft randomness; none at the eave edge.
        float Bump(float x, float side, float s)
        {
            float rows = 0.035f * (0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * s / 0.42f));
            float random = (Mathf.PerlinNoise(x * 1.7f + side * 13f, s * 2.3f) - 0.5f) * 0.08f;
            return (rows + random) * Mathf.Clamp01((slope - s) / 0.3f) + ThatchSwell(s, slope);
        }
        // The eave edge is a little ragged.
        float Reach(float x, float side) => slope + (Mathf.PerlinNoise(x * 4f, side * 7f) - 0.5f) * 0.12f;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // A grid of points (rings x columns) as one smooth surface; each triangle is turned to face
        // the wanted side.
        void Surface(Vector3[,] grid, Vector3[,] want, System.Func<int, int, Vector2> uv)
        {
            int rows = grid.GetLength(0), cols = grid.GetLength(1);
            int start = verts.Count;
            for (int r = 0; r < rows; r++)
                for (int k = 0; k < cols; k++)
                {
                    verts.Add(grid[r, k]);
                    uvs.Add(uv(r, k));
                }
            for (int r = 0; r + 1 < rows; r++)
                for (int k = 0; k + 1 < cols; k++)
                {
                    int a = start + r * cols + k, b = a + 1, c = a + cols, d = c + 1;
                    Triangle(a, b, d, want[r, k]);
                    Triangle(a, d, c, want[r, k]);
                }
        }
        void Triangle(int a, int b, int c, Vector3 want)
        {
            var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, want) < 0f) tris.AddRange(new[] { a, c, b });
            else tris.AddRange(new[] { a, b, c });
        }

        float X(int r) => Mathf.Min(-length / 2f + r * step, length / 2f);
        float S(int k, float x) => k == 0 || k == count - 1 ? Reach(x, profile[k].side) : profile[k].s;

        var upper = new Vector3[rings, count];
        var lower = new Vector3[rings, count];
        var upWant = new Vector3[rings, count];
        var lowWant = new Vector3[rings, count];
        for (int r = 0; r < rings; r++)
            for (int k = 0; k < count; k++)
            {
                float x = X(r);
                var (side, _) = profile[k];
                float s = S(k, x);
                var c = Centre(x, side, s);
                float depth = ThatchDepth(s, slope);
                upper[r, k] = c + Out(side) * (depth / 2f + Bump(x, side, s));
                lower[r, k] = c - Out(side) * (depth / 2f);
                upWant[r, k] = Out(side);
                lowWant[r, k] = -Out(side);
            }
        Surface(upper, upWant, (r, k) => new Vector2(X(r), profile[k].side * profile[k].s));
        Surface(lower, lowWant, (r, k) => new Vector2(X(r), profile[k].side * profile[k].s + 3f));

        // The rounded eaves: half circles from the upper surface round to the lower one.
        foreach (int k in new[] { 0, count - 1 })
        {
            float side = profile[k].side;
            var arc = new Vector3[rings, tip];
            var arcWant = new Vector3[rings, tip];
            for (int r = 0; r < rings; r++)
            {
                float x = X(r), s = S(k, x);
                var c = Centre(x, side, s);
                float radius = ThatchDepth(s, slope) / 2f;
                for (int j = 0; j < tip; j++)
                {
                    float t = Mathf.PI * j / (tip - 1);
                    var dir = Out(side) * Mathf.Cos(t) + Down(side) * Mathf.Sin(t);
                    arc[r, j] = c + dir * radius;
                    arcWant[r, j] = dir;
                }
            }
            Surface(arc, arcWant, (r, j) => new Vector2(X(r), 5f + j * 0.06f));
        }

        // The two ends: the space between the upper and lower surfaces, closed.
        foreach (int r in new[] { 0, rings - 1 })
        {
            var want = new Vector3(r == 0 ? -1f : 1f, 0f, 0f);
            var end = new Vector3[2, count];
            var endWant = new Vector3[2, count];
            for (int k = 0; k < count; k++)
            {
                end[0, k] = upper[r, k];
                end[1, k] = lower[r, k];
                endWant[0, k] = endWant[1, k] = want;
            }
            Surface(end, endWant, (row, k) => new Vector2(end[row, k].z, end[row, k].y));
            foreach (int k in new[] { 0, count - 1 })
            {
                float side = profile[k].side, x = X(r), s = S(k, x);
                var c = Centre(x, side, s);
                float radius = ThatchDepth(s, slope) / 2f;
                int centre = verts.Count;
                verts.Add(c);
                uvs.Add(new Vector2(c.z, c.y));
                for (int j = 0; j < tip; j++)
                {
                    float t = Mathf.PI * j / (tip - 1);
                    var p = c + (Out(side) * Mathf.Cos(t) + Down(side) * Mathf.Sin(t)) * radius;
                    verts.Add(p);
                    uvs.Add(new Vector2(p.z, p.y));
                }
                for (int j = 0; j + 1 < tip; j++) Triangle(centre, centre + 1 + j, centre + 2 + j, want);
            }
        }

        top = upper[rings / 2, perSide - 1].y;

        // Rounded ends, like a loaf: over the last 0.8 m at each end the whole thatch curves down
        // toward the eave and in a little, closing smaller.
        float eaveY = Centre(0f, 1f, slope).y;
        for (int v = 0; v < verts.Count; v++)
        {
            var p = verts[v];
            float t = Mathf.Clamp01((Mathf.Abs(p.x) - (length / 2f - 0.8f)) / 0.8f);
            float f = 0.55f + 0.45f * Mathf.Sqrt(1f - t * t);
            p.y = eaveY + (p.y - eaveY) * f;
            p.z *= 0.8f + 0.2f * f;
            verts[v] = p;
        }

        var mesh = new Mesh { name = "PelaThatch" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        if (!AssetDatabase.IsValidFolder("Assets/NightWatch/Meshes")) AssetDatabase.CreateFolder("Assets/NightWatch", "Meshes");
        AssetDatabase.DeleteAsset(ThatchMeshPath);
        AssetDatabase.CreateAsset(mesh, ThatchMeshPath);
        return mesh;
    }

    // A flat card (a quad with no collider) for straw pictures.
    static void StrawCard(string name, Transform parent, Vector3 at, Quaternion turn, Vector3 size, Material mat)
    {
        var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        card.name = name;
        Object.DestroyImmediate(card.GetComponent<Collider>());
        card.transform.SetParent(parent, false);
        card.transform.localPosition = at;
        card.transform.localRotation = turn;
        card.transform.localScale = size;
        card.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // Loose straw lying on the thatch (made here, no download): short golden strands at random,
    // all running down the slope, on a see-through ground; one picture covers 0.5 m.
    const string ShellTexturePath = "Assets/NightWatch/Textures/StrawShell.png";

    static Material StrawShellMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(ShellTexturePath) == null)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) tex.SetPixel(x, y, clear);
            var random = new System.Random(23);
            var straw = new Color(0.80f, 0.66f, 0.40f);
            for (int n = 0; n < 900; n++)
            {
                // One strand: a thin line, slightly slanted, its own length and shade (wraps round).
                int x0 = random.Next(size), y0 = random.Next(size), length = 18 + random.Next(50);
                float slant = ((float)random.NextDouble() - 0.5f) * 0.35f;
                var colour = straw * (0.65f + 0.55f * (float)random.NextDouble());
                colour.a = 1f;
                for (int k = 0; k < length; k++)
                {
                    int x = ((int)(x0 + slant * k) % size + size) % size;
                    tex.SetPixel(x, (y0 + k) % size, colour * (1f - 0.3f * k / length) + new Color(0f, 0f, 0f, 0.3f * k / length));
                }
            }
            System.IO.File.WriteAllBytes(ShellTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ShellTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ShellTexturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        var mat = Mat("StrawShell", Color.white);
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ShellTexturePath));
        // The cards are about 4.1 x 0.6 m: one picture per 0.5 m each way.
        mat.SetTextureScale("_BaseMap", new Vector2(8.2f, 1.2f));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cull", 0f);
        mat.SetFloat("_Smoothness", 0.03f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // A strip of straw ends (made here, no download): golden strands of different lengths on a
    // see-through ground, cut out (alpha clip) and seen from both sides.
    const string FringeTexturePath = "Assets/NightWatch/Textures/StrawFringe.png";

    static Material StrawFringeMaterial(float stripLength)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(FringeTexturePath) == null)
        {
            const int w = 256, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var random = new System.Random(11);
            var straw = new Color(0.78f, 0.64f, 0.38f);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int x = 0; x < w; x++)
            {
                // Each column is one strand: its own length (from the top) and shade.
                float length = 0.45f + 0.55f * (float)random.NextDouble();
                float shade = 0.7f + 0.45f * (float)random.NextDouble();
                for (int y = 0; y < h; y++)
                {
                    float fromTop = 1f - (y + 0.5f) / h;
                    tex.SetPixel(x, y, fromTop < length ? straw * shade * (1f - 0.35f * fromTop) : clear);
                }
            }
            for (int x = 0; x < w; x++) tex.SetPixel(x, h - 1, straw * 0.7f);
            System.IO.File.WriteAllBytes(FringeTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(FringeTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(FringeTexturePath);
            importer.alphaIsTransparency = true;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        var mat = Mat("StrawFringe", Color.white);
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FringeTexturePath));
        // One copy of the strand picture every 40 cm along the strip.
        mat.SetTextureScale("_BaseMap", new Vector2(stripLength / 0.4f, 1f));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cull", 0f);   // both sides
        mat.SetFloat("_Smoothness", 0.03f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // A round pole (a cylinder) from a to b.
    static GameObject Pole(string name, Transform parent, Vector3 a, Vector3 b, float diameter, Material mat)
    {
        var go = Box(name, parent, (a + b) / 2f, new Vector3(diameter, Vector3.Distance(a, b) / 2f, diameter), mat, PrimitiveType.Cylinder);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        return go;
    }

    // The player starts on the ground in the afternoon (GroundPrep moves them there at run time too).
    static void PlacePlayer()
    {
        var rig = GameObject.Find("XR Origin (XR Rig)");
        if (rig == null)
        {
            Debug.LogWarning("Night Watch: no 'XR Origin (XR Rig)' in the scene, player not moved.");
            return;
        }
        // Tracking origin: Floor. A headset then measures the eye height from the real floor, so
        // a tall and a short player each see from their own height (a standing experience). With
        // no headset (the simulator) nothing changes: the camera stays at the rig's eye height.
        var origin = rig.GetComponent<XROrigin>();
        if (origin != null)
        {
            Undo.RecordObject(origin, "Tracking origin Floor");
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            PrefabUtility.RecordPrefabInstancePropertyModifications(origin);
        }

        Undo.RecordObject(rig.transform, "Move player to the start");
        rig.transform.SetPositionAndRotation(FarmLayout.OnGround(GroundPrepSetup.StartPosition), Quaternion.identity);

        // Keep the player's head over the hut floor, with a small margin from the edge.
        var limit = rig.GetComponent<PlayAreaLimit>();
        if (limit == null) limit = Undo.AddComponent<PlayAreaLimit>(rig);
        Undo.RecordObject(limit, "Set play area");
        var cam = rig.GetComponentInChildren<Camera>();
        limit.head = cam != null ? cam.transform : null;
        limit.areaCenter = new Vector3(0f, HutFloorHeight + 1f, 0f);
        limit.areaSize = new Vector2(HutSize - 0.4f, HutSize - 0.4f);
        var blockout = GameObject.Find(RootName).transform;
        limit.hutFloor = blockout.Find("WatchHut/Floor").GetComponent<TeleportationArea>();
        // On the ground, the whole village terrain is the teleport target.
        var terrain = GameObject.Find("Village/Terrain");
        limit.groundArea = terrain != null ? terrain.GetComponent<TeleportationArea>() : null;
        if (limit.groundArea == null) Debug.LogWarning("Night Watch: no Village/Terrain teleport area. Build the village first.");
        PrefabUtility.RecordPrefabInstancePropertyModifications(limit);

        // The body cannot walk through walls, posts, the table, bonfires or elephants.
        var blocker = rig.GetComponent<BodyBlocker>();
        if (blocker == null) blocker = Undo.AddComponent<BodyBlocker>(rig);
        Undo.RecordObject(blocker, "Set body blocker");
        blocker.head = limit.head;
        PrefabUtility.RecordPrefabInstancePropertyModifications(blocker);
    }

    static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat, PrimitiveType type = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // Materials are saved as assets so the scene keeps them after it is closed.
    static Material Mat(string name, Color color) => EditorKit.Mat(name, color);
}
