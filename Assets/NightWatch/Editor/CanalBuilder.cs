using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The canal (ela) from the tank (wewa) to the paddy field, built the way a tank village does it:
//  - at the tank, the sluice (sorowwa) intake: two dressed-stone pillars standing in the water
//    with a wooden gate plank in their grooves, a cross beam and a lifting rod on top, and a plank
//    walk out to it from the bund;
//  - the water runs under the bund in a buried culvert and comes out at the bund's foot through a
//    stone headwall with two wing walls, falling a little onto a stone sill;
//  - the channel is dug into the ground (VillageBuilder carves the terrain with Carve), curves down
//    to the field with the slope, both banks lined with rough stones and reeds here and there;
//    two stone slabs carry the footpath over it;
//  - it ends through a gap in the field's north bund, spilling into the first flooded plot.
// The water is one strip whose UVs run along the channel, so its ripples flow toward the field.
// The splashes and the trickling sound are near only (small particle systems, 3D sound).
public static class CanalBuilder
{
    // The centre line (Unity x, z): from the headwall at the bund's foot down to the field.
    // It ends in the flooded plot at x -18.5..-9.25, between two posts of the north fence.
    static readonly Vector2[] Line =
    {
        new Vector2(-7f, 29.7f), new Vector2(-7.3f, 27.6f), new Vector2(-8.3f, 25.1f), new Vector2(-9.7f, 22.5f),
        new Vector2(-10.85f, 20.2f), new Vector2(-11.25f, 18.6f), new Vector2(-11.25f, 17.2f),
    };
    // The water surface at the headwall and at the field; the tank's water (level.json) is 0.45 and
    // the flooded plots are 0.07, so it runs downhill all the way.
    const float LevelStart = 0.36f, LevelEnd = 0.12f, PlotLevel = 0.07f, TankLevel = 0.45f;
    const float HalfWidth = 0.45f;   // the channel bed's half width
    const float Depth = 0.28f;       // water depth in the channel
    const float BankRise = 0.15f;    // bank top above the water
    const float WaterHalfWidth = 0.85f;   // the water strip reaches under the banks on both sides
    // The tank side of the sluice: the intake in the water at the bund's inner foot.
    static readonly Vector2 Intake = new Vector2(-7f, 42.7f);

    // ---------------- the line, sampled ----------------

    static List<Vector2> points;
    static List<float> along;
    static Rect box;

    static void Sample()
    {
        if (points != null) return;
        points = new List<Vector2>();
        along = new List<float>();
        // A smooth curve through the line's points (Catmull-Rom), one sample every 0.25 m or so.
        for (int i = 0; i + 1 < Line.Length; i++)
        {
            var p0 = Line[Mathf.Max(i - 1, 0)];
            var p1 = Line[i];
            var p2 = Line[i + 1];
            var p3 = Line[Mathf.Min(i + 2, Line.Length - 1)];
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 0.25f));
            for (int k = 0; k < steps; k++)
            {
                float t = k / (float)steps;
                points.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
            }
        }
        points.Add(Line[Line.Length - 1]);
        float s = 0f;
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0) s += Vector2.Distance(points[i - 1], points[i]);
            along.Add(s);
            x0 = Mathf.Min(x0, points[i].x); x1 = Mathf.Max(x1, points[i].x);
            z0 = Mathf.Min(z0, points[i].y); z1 = Mathf.Max(z1, points[i].y);
        }
        box = Rect.MinMaxRect(x0 - 3f, z0 - 3f, x1 + 3f, z1 + 3f);
    }

    static float Total => along[along.Count - 1];
    static float Level(float s) => Mathf.Lerp(LevelStart, LevelEnd, s / Total);

    // The channel bed: level minus the depth, rising to a low lip at the field end.
    static float Bed(float s) => Mathf.Lerp(Level(s) - Depth, PlotLevel - 0.04f, Smooth(Total - 0.9f, Total, s));

    // Distance from a point to the line, and how far along the line its nearest point is.
    // before: the point lies beyond the headwall end (inside the bund).
    static float Nearest(Vector2 q, out float s, out bool before)
    {
        float best = float.MaxValue;
        s = 0f;
        before = false;
        for (int i = 0; i + 1 < points.Count; i++)
        {
            var a = points[i];
            var ab = points[i + 1] - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            float d = Vector2.Distance(q, a + ab * t);
            if (d < best)
            {
                best = d;
                s = along[i] + t * ab.magnitude;
                before = i == 0 && Vector2.Dot(q - a, ab) < 0f;
            }
        }
        return best;
    }

    static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    // ---------------- the ground (called by VillageBuilder for every terrain point) ----------------

    // The ground height at Unity (x, z) with the canal dug in: a bed and stone-lined sides, raised
    // banks where the land is lower than the water, and a cut with sloping sides where it is higher.
    public static float Carve(float x, float z, float y)
    {
        Sample();
        if (!box.Contains(new Vector2(x, z))) return y;
        float d = Nearest(new Vector2(x, z), out float s, out bool before);
        if (before || d > 3f) return y;   // the headwall closes the channel's top end
        float level = Level(s), bed = Bed(s), top = level + BankRise;
        float shape = d < 0.85f ? Mathf.Lerp(bed, top, Smooth(HalfWidth - 0.05f, 0.85f, d)) : top;
        if (y < shape) return Mathf.Lerp(shape, y, Smooth(1.3f, 2.4f, d));   // built-up banks
        return Mathf.Min(y, shape + Mathf.Max(0f, d - 0.85f) * 1.8f);          // dug through higher ground
    }

    // Village plants are kept out of the channel and off its banks (VillageExtras.Clearings).
    public static IEnumerable<(Vector2 centre, float radius)> Clearings()
    {
        Sample();
        for (int i = 0; i < points.Count; i += 4) yield return (points[i], 1.5f);
        yield return (Intake, 2f);
    }

    // Reeds (the village's reeds model) on the banks and at the intake, for VillageExtras.Adjust.
    public static IEnumerable<(Vector3 at, float yaw)> Reeds()
    {
        Sample();
        var random = new System.Random(21);
        foreach (float f in new[] { 0.12f, 0.27f, 0.36f, 0.52f, 0.66f, 0.8f, 0.9f })
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(f * (points.Count - 1)), 1, points.Count - 2);
            var dir = (points[i + 1] - points[i - 1]).normalized;
            var side = new Vector2(dir.y, -dir.x) * (random.NextDouble() < 0.5 ? -1f : 1f);
            var p = points[i] + side * (1.15f + 0.2f * (float)random.NextDouble());
            yield return (new Vector3(p.x, 0f, p.y), (float)random.NextDouble() * 360f);
        }
        // Along the tank shore beside the intake.
        foreach (var o in new[] { new Vector2(-2.2f, -0.2f), new Vector2(-3f, 0.4f), new Vector2(2.1f, -0.3f), new Vector2(3.2f, 0.3f) })
            yield return (new Vector3(Intake.x + o.x, 0f, Intake.y + o.y - 0.9f), (float)random.NextDouble() * 360f);
    }

    // ---------------- the build (after the terrain, from VillageExtras.Build) ----------------

    public static void Build(Transform root)
    {
        Sample();
        var canal = EditorKit.Group("Canal", root);
        var stone = EditorKit.Mat("SluiceStone", new Color(0.62f, 0.60f, 0.56f));
        var rubble = EditorKit.Mat("CanalStone", new Color(0.55f, 0.53f, 0.50f));
        var wood = EditorKit.Mat("Wood", new Color(0.45f, 0.35f, 0.25f));

        BuildWater(canal);
        BuildStones(canal, rubble);
        BuildHeadwall(canal, stone);
        BuildIntake(canal, stone, wood);
        BuildBridge(canal, rubble);

        // The field end: a splash where it spills into the plot, and the sound of it.
        var end = points[points.Count - 1];
        var endAt = new Vector3(end.x, PlotLevel + 0.01f, end.y);
        WaterSplash.Build(canal, "EndSplash", endAt, 0.6f);
        AddSound(canal, "EndTrickle", endAt, 0.3f);
    }

    // The water strip: across from bank to bank, along the line, at the water level.
    static void BuildWater(Transform canal)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i < points.Count; i++)
        {
            var dir = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
            var side = new Vector2(dir.y, -dir.x);
            float s = along[i];
            // The last 0.6 m dips under the plot's water, so the two meet without a seam.
            float y = Mathf.Lerp(Level(s), PlotLevel - 0.02f, Smooth(Total - 0.6f, Total, s));
            foreach (float k in new[] { -1f, 1f })
            {
                var p = points[i] + side * (k * WaterHalfWidth);
                verts.Add(new Vector3(p.x, y, p.y));
                uvs.Add(new Vector2(k * WaterHalfWidth, s));
            }
            if (i > 0)
            {
                int o = verts.Count - 4;
                tris.AddRange(new[] { o, o + 2, o + 1, o + 1, o + 2, o + 3 });
            }
        }
        FaceUp(verts, tris);
        var mesh = SaveMesh("CanalWater", verts, uvs, tris);
        AddMesh(canal, "Water", mesh, WaterSetup.Canal(), false);
    }

    // Rough stones lining both banks: one row at the water's edge, one on the bank above.
    // All in one mesh (one draw), small and without shadows.
    static void BuildStones(Transform canal, Material rubble)
    {
        var random = new System.Random(8);
        var shapes = new Mesh[5];
        for (int k = 0; k < shapes.Length; k++) shapes[k] = StoneShape(random);
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        void Row(float offset, float spacing, float sizeMin, float sizeMax, float sink)
        {
            foreach (float k in new[] { -1f, 1f })
            {
                // Start past the headwall's wing walls, stop at the field's bund.
                for (float s = 1.3f + (float)random.NextDouble() * spacing; s < Total - 0.5f; s += spacing * (0.8f + 0.4f * (float)random.NextDouble()))
                {
                    var (p, dir) = PointAt(s);
                    var side = new Vector2(dir.y, -dir.x) * k;
                    var q = p + side * (offset + 0.06f * ((float)random.NextDouble() - 0.5f));
                    float size = Mathf.Lerp(sizeMin, sizeMax, (float)random.NextDouble());
                    var ground = FarmLayout.OnGround(new Vector3(q.x, 0f, q.y));
                    var at = ground + Vector3.up * (size * (0.5f - sink));
                    var turn = Quaternion.Euler((float)random.NextDouble() * 16f - 8f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 16f - 8f);
                    var scale = new Vector3(size, size * (0.55f + 0.2f * (float)random.NextDouble()), size * (0.8f + 0.4f * (float)random.NextDouble()));
                    AddStone(shapes[random.Next(shapes.Length)], Matrix4x4.TRS(at, turn, scale), verts, normals, uvs, tris);
                }
            }
        }
        Row(HalfWidth + 0.08f, 0.27f, 0.22f, 0.36f, 0.35f);
        Row(HalfWidth + 0.36f, 0.34f, 0.2f, 0.32f, 0.3f);

        var mesh = new Mesh { name = "CanalStones" };
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        mesh = SaveMesh(mesh);
        AddMesh(canal, "Stones", mesh, rubble, false);
        Debug.Log($"Night Watch: canal {Total:F1} m long, {tris.Count / 3} stone triangles.");
    }

    // A rough round stone: a low-poly ball with each point pushed in or out a little.
    static Mesh StoneShape(System.Random random)
    {
        const int around = 7, up = 5;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        verts.Add(Vector3.down * 0.5f);
        for (int j = 1; j < up; j++)
        {
            float lat = Mathf.PI * j / up - Mathf.PI / 2f;
            for (int i = 0; i < around; i++)
            {
                float lon = 2f * Mathf.PI * (i + 0.5f * (j % 2)) / around;
                float r = 0.5f * (0.82f + 0.3f * (float)random.NextDouble());
                verts.Add(new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)) * r);
            }
        }
        verts.Add(Vector3.up * 0.5f * (0.75f + 0.2f * (float)random.NextDouble()));
        int topIndex = verts.Count - 1;
        for (int i = 0; i < around; i++) tris.AddRange(new[] { 0, 1 + i, 1 + (i + 1) % around });
        for (int j = 0; j < up - 2; j++)
        {
            int a = 1 + j * around, b = a + around;
            for (int i = 0; i < around; i++)
            {
                int i1 = (i + 1) % around;
                tris.AddRange(new[] { a + i, b + i, a + i1, a + i1, b + i, b + i1 });
            }
        }
        int last = 1 + (up - 2) * around;
        for (int i = 0; i < around; i++) tris.AddRange(new[] { last + (i + 1) % around, last + i, topIndex });
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        // Make every face point outward.
        var t = mesh.triangles;
        for (int k = 0; k < t.Length; k += 3)
        {
            var c = (verts[t[k]] + verts[t[k + 1]] + verts[t[k + 2]]) / 3f;
            var n = Vector3.Cross(verts[t[k + 1]] - verts[t[k]], verts[t[k + 2]] - verts[t[k]]);
            if (Vector3.Dot(n, c) < 0f) { int s = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = s; }
        }
        mesh.triangles = t;
        mesh.RecalculateNormals();
        return mesh;
    }

    // One stone into the shared mesh, its texture laid flat on its main side (UVs in metres).
    static void AddStone(Mesh shape, Matrix4x4 m, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
    {
        int o = verts.Count;
        var sv = shape.vertices;
        var sn = shape.normals;
        var offset = new Vector2(m.m03 * 0.37f, m.m23 * 0.53f);
        for (int i = 0; i < sv.Length; i++)
        {
            var p = m.MultiplyPoint3x4(sv[i]);
            var n = m.inverse.transpose.MultiplyVector(sn[i]).normalized;
            verts.Add(p);
            normals.Add(n);
            var local = Vector3.Scale(sv[i], m.lossyScale);
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            var uv = ay >= ax && ay >= az ? new Vector2(local.x, local.z) : ax >= az ? new Vector2(local.z, local.y) : new Vector2(local.x, local.y);
            uvs.Add(uv + offset);
        }
        foreach (int t in shape.triangles) tris.Add(o + t);
    }

    // The outlet at the bund's foot: a dressed-stone headwall across the channel's top end, the
    // culvert's dark mouth near its foot with a stone lintel, two wing walls along the cut, a
    // stone sill, and a short fall of water with its splash and sound.
    static void BuildHeadwall(Transform canal, Material stone)
    {
        var (start, dir) = PointAt(0f);
        var face = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));   // looks down the channel
        float bed = Bed(0f), level = Level(0f);
        // 0.3 m downstream of the line's start, so the wall hides the terrain's step where the dig ends.
        var origin = new Vector3(start.x, bed, start.y) + face * Vector3.forward * 0.3f;
        var outlet = EditorKit.Empty("Headwall", canal, origin);
        outlet.rotation = face;

        Block("Wall", outlet, new Vector3(0f, 0.62f, -0.25f), new Vector3(2.9f, 1.3f, 0.5f), stone, true);
        Block("Coping", outlet, new Vector3(0f, 1.32f, -0.22f), new Vector3(3.1f, 0.14f, 0.62f), stone, false);
        foreach (float k in new[] { -1f, 1f })
        {
            var wing = Block("WingWall", outlet, new Vector3(k * 1.15f, 0.42f, 0.55f), new Vector3(0.38f, 0.9f, 1.5f), stone, true);
            wing.transform.localRotation = Quaternion.Euler(0f, k * -12f, 0f);
        }
        // The culvert's mouth: a dark opening (the pipe under the bund), its stone lintel above.
        float mouthBottom = level + 0.12f - bed;
        var dark = EditorKit.Mat("CulvertDark", new Color(0.02f, 0.02f, 0.02f));
        Block("CulvertMouth", outlet, new Vector3(0f, mouthBottom + 0.2f, 0.005f), new Vector3(0.55f, 0.4f, 0.02f), dark, false);
        Block("Lintel", outlet, new Vector3(0f, mouthBottom + 0.47f, 0.04f), new Vector3(0.95f, 0.16f, 0.12f), stone, false);
        Block("Sill", outlet, new Vector3(0f, mouthBottom - 0.04f, 0.12f), new Vector3(0.8f, 0.08f, 0.26f), stone, false);

        // The fall: a short strip of flowing water from the mouth down to the channel.
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        const int steps = 6;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float z = 0.02f + t * 0.4f;
            float y = Mathf.Lerp(mouthBottom + 0.01f, level - bed + 0.005f, t * t);
            foreach (float k in new[] { -1f, 1f })
            {
                verts.Add(new Vector3(k * Mathf.Lerp(0.26f, 0.4f, t), y, z));
                uvs.Add(new Vector2(k * 0.3f, t * 0.6f));
            }
            if (i > 0)
            {
                int o = verts.Count - 4;
                tris.AddRange(new[] { o, o + 2, o + 1, o + 1, o + 2, o + 3 });
            }
        }
        FaceUp(verts, tris);
        var fall = SaveMesh("CanalFall", verts, uvs, tris);
        var fallGo = AddMesh(outlet, "Fall", fall, WaterSetup.Canal(), false);
        fallGo.transform.localPosition = Vector3.zero;
        fallGo.transform.localRotation = Quaternion.identity;

        var splashAt = outlet.TransformPoint(new Vector3(0f, level - bed + 0.02f, 0.45f));
        WaterSplash.Build(canal, "OutletSplash", splashAt, 1f);
        AddSound(canal, "OutletTrickle", splashAt, 0.45f);
    }

    // The intake in the tank: two stone pillars with grooves, a wooden gate plank between them
    // (lifted a little, so water runs under it), a beam and a lifting rod with a cross handle, and
    // two planks from the bund's slope out to the beam.
    static void BuildIntake(Transform canal, Material stone, Material wood)
    {
        var ground = FarmLayout.OnGround(new Vector3(Intake.x, 0f, Intake.y));
        var intake = EditorKit.Empty("SluiceIntake", canal, ground);
        intake.rotation = Quaternion.identity;   // the gate faces the tank (north)
        float water = TankLevel - ground.y;

        foreach (float k in new[] { -1f, 1f })
            Block("Pillar", intake, new Vector3(k * 0.5f, 0.75f, 0f), new Vector3(0.36f, 1.7f, 0.42f), stone, true);
        Block("Base", intake, new Vector3(0f, 0.04f, 0f), new Vector3(1.5f, 0.16f, 0.8f), stone, false);
        Block("GatePlank", intake, new Vector3(0f, 0.2f + 0.55f, 0f), new Vector3(0.66f, 1.1f, 0.07f), wood, false);
        Block("Beam", intake, new Vector3(0f, 1.66f, 0f), new Vector3(1.5f, 0.14f, 0.18f), wood, false);
        var rod = EditorKit.Part("LiftRod", PrimitiveType.Cylinder, intake, new Vector3(0f, 1.6f, 0f), new Vector3(0.05f, 0.35f, 0.05f), wood, false);
        rod.isStatic = true;
        var handle = EditorKit.Part("Handle", PrimitiveType.Cylinder, intake, new Vector3(0f, 1.95f, 0f), new Vector3(0.04f, 0.22f, 0.04f), wood, false);
        handle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        handle.isStatic = true;

        // The plank walk from the bund (south, higher) out to the beam.
        var bund = FarmLayout.OnGround(new Vector3(Intake.x, 0f, Intake.y - 2.4f));
        var from = intake.InverseTransformPoint(bund + Vector3.up * 0.05f);
        var to = new Vector3(0f, 1.74f, -0.1f);
        foreach (float k in new[] { -0.13f, 0.13f })
        {
            var a = from + Vector3.right * k;
            var b = to + Vector3.right * k;
            var plank = Block("WalkPlank", intake, (a + b) / 2f, new Vector3(0.24f, 0.05f, Vector3.Distance(a, b) + 0.2f), wood, false);
            plank.transform.localRotation = Quaternion.LookRotation(b - a);
        }
        if (water < 0.2f) Debug.LogWarning($"Night Watch: the sluice intake stands in only {water:F2} m of water.");
    }

    // Two flat stone slabs carry the footpath over the canal.
    static void BuildBridge(Transform canal, Material rubble)
    {
        // Where the footpath (hut to dagaba) crosses the line.
        float bestGap = float.MaxValue, bestS = 0f;
        var path = VillageExtras.FootPath;
        for (float s = 1f; s < Total - 1f; s += 0.1f)
        {
            var (p, _) = PointAt(s);
            for (int i = 0; i + 1 < path.Length; i++)
            {
                var a = path[i];
                var ab = path[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                float gap = Vector2.Distance(p, a + ab * t);
                if (gap < bestGap) { bestGap = gap; bestS = s; }
            }
        }
        if (bestGap > 1f) return;
        var (at, dir) = PointAt(bestS);
        var acrossDir = new Vector3(dir.y, 0f, -dir.x);
        // Here the canal is dug through a low ridge: the slabs rest on the lower of its two edges.
        float span = 1.5f;
        var centre = new Vector3(at.x, 0f, at.y);
        float top = Mathf.Min(FarmLayout.OnGround(centre + acrossDir * span).y, FarmLayout.OnGround(centre - acrossDir * span).y);
        top = Mathf.Max(top, Level(bestS) + BankRise) + 0.04f;
        var bridge = EditorKit.Empty("SlabBridge", canal, new Vector3(at.x, top, at.y));
        bridge.rotation = Quaternion.LookRotation(acrossDir);
        foreach (float k in new[] { -0.23f, 0.23f })
        {
            var slab = Block("Slab", bridge, new Vector3(k, 0f, 0f), new Vector3(0.44f, 0.12f, span * 2f + 0.3f), rubble, false);
            slab.transform.localRotation = Quaternion.Euler(0f, k * 6f, k * 3f);
        }
    }

    // ---------------- helpers ----------------

    static (Vector2 point, Vector2 dir) PointAt(float s)
    {
        for (int i = 0; i + 1 < points.Count; i++)
        {
            if (along[i + 1] < s && i + 2 < points.Count) continue;
            float t = Mathf.InverseLerp(along[i], along[i + 1], s);
            return (Vector2.Lerp(points[i], points[i + 1], t), (points[i + 1] - points[i]).normalized);
        }
        return (points[points.Count - 1], Vector2.down);
    }

    static GameObject Block(string name, Transform parent, Vector3 local, Vector3 size, Material mat, bool collider)
    {
        var go = EditorKit.Part(name, PrimitiveType.Cube, parent, local, size, mat, collider);
        go.isStatic = true;
        return go;
    }

    static void FaceUp(List<Vector3> verts, List<int> tris)
    {
        for (int k = 0; k < tris.Count; k += 3)
        {
            var n = Vector3.Cross(verts[tris[k + 1]] - verts[tris[k]], verts[tris[k + 2]] - verts[tris[k]]);
            if (n.y < 0f) { int t = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = t; }
        }
    }

    static Mesh SaveMesh(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh);
    }

    static Mesh SaveMesh(Mesh mesh)
    {
        string path = $"Assets/NightWatch/Village/Generated/{mesh.name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static GameObject AddMesh(Transform parent, string name, Mesh mesh, Material mat, bool shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        go.isStatic = true;
        return go;
    }

    static void AddSound(Transform parent, string name, Vector3 at, float volume)
    {
        var go = EditorKit.Empty(name, parent, at);
        var source = go.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.volume = volume;
        go.gameObject.AddComponent<WaterSound>();
    }
}
