using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Our additions to the Elephant Village, round the paddy field (sketch: docs/layout-ideas.png):
//  - the dagaba moved from the far north-east to the north-west, between the tank and the field,
//    with the jacaranda moved beside it and coconut palms round it: seen from the hut against the
//    sunset over the tank (about 55 m)
//  - a footpath from the hut along the bund and on to the dagaba, and a canal (ela) from the
//    tank down to the field, as in a real tank village (CanalBuilder)
//  - a threshing floor (kamatha) on a harvested plot: packed earth, a centre post, haystacks,
//    baskets, a pot and a stool, and two village cattle tied beside it (they once trod the paddy)
//  - a big shade tree on a bund island, an agro-well in the field (common in dry-zone paddy),
//    banana plants at the field edge and three scarecrows (pambaya) with clay-pot heads
// Points are Unity metres (our hut at 0, 0, +z north). VillageBuilder calls Adjust while it places
// the village's models, and Build at the end; FarmLayout keeps plants off these places.
public static class VillageExtras
{
    public static readonly Vector3 Dagaba = new Vector3(-40f, 0f, 36f);
    public const float DagabaClear = 12f;
    public static readonly Vector3 Kamatha = new Vector3(-32.25f, 0f, -30.625f);   // the middle of one plot
    public const float KamathaRadius = 2.8f;
    public static readonly Vector3 ShadeTree = new Vector3(-18.5f, 0f, 0f);         // a bund crossing
    public static readonly Vector3 AgroWell = new Vector3(-27.75f, 0f, 8.75f);      // a bund crossing

    // The footpath: from the hut north along the bund, past the fence and on to the dagaba.
    public static readonly Vector2[] FootPath =
    {
        new Vector2(0f, 0f), new Vector2(0f, 20.5f), new Vector2(-12f, 26f), new Vector2(-28f, 32f), new Vector2(-33f, 34.5f),
    };
    public const float FootPathHalfWidth = 0.7f;

    // Copies placed round the kamatha, in its frame (x east, z north of its middle).
    static readonly (string model, float x, float z, float yaw)[] KamathaProps =
    {
        ("haystack_1", -3.4f, 3.1f, 20f), ("haystack_2", -3.6f, -3.0f, 140f), ("haystack_1", 3.5f, -3.1f, 250f),
        ("wicker_basket_01", 2.2f, -1.7f, 30f), ("wicker_basket_02", 2.6f, -0.9f, 100f),
        ("ceramic_pot", -2.3f, -0.5f, 0f), ("wooden_stool_01", 1.2f, 3.4f, 60f),
    };
    // The cattle's tethering posts (kamatha frame), just off the floor's edge. Each animal grazes
    // in a circle round its post (CattleLife), kept off the floor and the haystacks.
    static readonly (float x, float z)[] TiePosts = { (4.0f, 1.2f), (0.2f, -4.1f) };
    const float TiePostHeight = 1.1f;

    static readonly (string model, Vector3 at, float yaw)[] Copies =
    {
        ("island_tree_02", ShadeTree, 40f),
        ("well", AgroWell, 30f),
        ("palm_2", Dagaba + new Vector3(-9f, 0f, -6f), 0f),
        ("palm_1", Dagaba + new Vector3(8f, 0f, -8f), 120f),
        ("palm_3", Dagaba + new Vector3(10f, 0f, 5f), 240f),
        ("banana_1", new Vector3(21f, 0f, -24f), 40f),
        ("banana_2", new Vector3(21.8f, 0f, -27.5f), 160f),
        ("banana_1", new Vector3(20.6f, 0f, -31f), 280f),
    };

    static readonly Vector3[] Scarecrows =
    {
        new Vector3(-41.625f, 0f, -4.375f), new Vector3(-23.125f, 0f, -39.375f), new Vector3(13.875f, 0f, -21.875f),
    };

    // Where the village's own copy is moved (its first copy only): model > new place.
    static readonly Dictionary<string, Vector3> Moves = new Dictionary<string, Vector3>
    {
        { "stupa", Dagaba },
        { "jacaranda_tree", Dagaba + new Vector3(-10f, 0f, 8f) },
    };

    // Small places kept clear of plants (but not drained): round each new thing.
    public static IEnumerable<(Vector2 centre, float radius)> Clearings()
    {
        yield return (Flat(Dagaba), DagabaClear);
        yield return (Flat(ShadeTree), 2.2f);
        yield return (Flat(AgroWell), 2.2f);
        foreach (var p in KamathaProps) yield return (Flat(KamathaPoint(p.x, p.z)), 1.6f);
        foreach (var c in TiePosts) yield return (Flat(KamathaPoint(c.x, c.z)), 2.6f);
        foreach (var s in Scarecrows) yield return (Flat(s), 0.6f);
        foreach (var c in CanalBuilder.Clearings()) yield return c;
        foreach (var c in Copies) if (c.model.StartsWith("banana") || c.model.StartsWith("palm")) yield return (Flat(c.at), 1.5f);
        yield return (Flat(Moves["jacaranda_tree"]), 4f);
    }

    static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);
    static Vector3 KamathaPoint(float x, float z) => Kamatha + new Vector3(x, 0f, z);

    // ---------------- while the village's models are placed ----------------

    // Moves the village's own copy where wanted, and adds our copies of a model (same size and
    // height above the ground as the village's first copy). Poses are VillageBuilder's 7 numbers
    // per copy: x, y, z, yaw in degrees, scale x, y, z (Unity space).
    public static float[] Adjust(string model, float[] poses)
    {
        int size = PropSet.PoseSize;
        if (poses.Length < size) return poses;
        var list = new List<float>(poses);
        float lift = poses[1] - FarmLayout.OnGround(new Vector3(poses[0], 0f, poses[2])).y;

        if (Moves.TryGetValue(model, out var to))
        {
            var ground = FarmLayout.OnGround(to);
            list[0] = to.x;
            list[1] = ground.y + lift;
            list[2] = to.z;
        }

        var extras = new List<(Vector3 at, float yaw)>();
        foreach (var c in Copies) if (c.model == model) extras.Add((c.at, c.yaw));
        foreach (var p in KamathaProps) if (p.model == model) extras.Add((KamathaPoint(p.x, p.z), p.yaw));
        if (model == "reeds") extras.AddRange(CanalBuilder.Reeds());
        foreach (var (at, yaw) in extras)
        {
            var ground = FarmLayout.OnGround(at);
            list.AddRange(new[] { at.x, ground.y + lift, at.z, yaw, poses[4], poses[5], poses[6] });
        }
        return list.ToArray();
    }

    // ---------------- after the village is built ----------------

    public static void Build(Transform village)
    {
        var root = EditorKit.Group("Extras", village);
        BuildKamatha(root);
        CanalBuilder.Build(root);
        foreach (var s in Scarecrows) BuildScarecrow(root, s);
        AddColliders(root, village);
    }

    static void BuildKamatha(Transform root)
    {
        var floor = EditorKit.Mat("KamathaFloor", new Color(0.62f, 0.50f, 0.36f));
        floor.SetFloat("_Smoothness", 0.05f);
        var at = FarmLayout.OnGround(Kamatha);
        var kamatha = EditorKit.Empty("Kamatha", root, at);
        // Packed, smeared earth (cow dung and clay, as farmers prepare it), a low rim, a centre post.
        EditorKit.Part("Floor", PrimitiveType.Cylinder, kamatha, new Vector3(0f, 0.04f, 0f),
            new Vector3(KamathaRadius * 2f, 0.04f, KamathaRadius * 2f), floor, false);
        EditorKit.Part("Rim", PrimitiveType.Cylinder, kamatha, new Vector3(0f, 0.03f, 0f),
            new Vector3(KamathaRadius * 2f + 0.3f, 0.03f, KamathaRadius * 2f + 0.3f),
            EditorKit.Mat("KamathaRim", new Color(0.45f, 0.36f, 0.25f)), false);
        var post = EditorKit.Mat("HutPole", new Color(0.40f, 0.31f, 0.22f));
        EditorKit.Part("CentrePost", PrimitiveType.Cylinder, kamatha, new Vector3(0f, 0.8f, 0f), new Vector3(0.14f, 0.8f, 0.14f), post);

        // Tethering posts and the two cattle (a cow and a bull), each starting a little way out from
        // its post, side on to the floor.
        var keepOut = new List<Vector3> { new Vector3(Kamatha.x, Kamatha.z, KamathaRadius + 0.5f) };
        foreach (var p in KamathaProps)
            keepOut.Add(new Vector3(Kamatha.x + p.x, Kamatha.z + p.z, p.model.StartsWith("haystack") ? 1.7f : 0.7f));
        foreach (var c in TiePosts) keepOut.Add(new Vector3(Kamatha.x + c.x, Kamatha.z + c.z, 0.6f));
        CattleSetup.Begin();
        foreach (var c in TiePosts)
        {
            var postAt = FarmLayout.OnGround(KamathaPoint(c.x, c.z));
            EditorKit.Part("TiePost", PrimitiveType.Cylinder, kamatha, kamatha.InverseTransformPoint(postAt) + Vector3.up * (TiePostHeight / 2f),
                new Vector3(0.1f, TiePostHeight / 2f, 0.1f), post);
            var outward = new Vector3(c.x, 0f, c.z).normalized;
            var spot = FarmLayout.OnGround(postAt + outward * 1.4f);
            var facing = Quaternion.LookRotation(Vector3.Cross(Vector3.up, outward));
            CattleSetup.Place(kamatha, spot, facing, postAt, TiePostHeight, keepOut.ToArray());
        }
    }

    // A scarecrow (pambaya): a pole and a cross stick, an old shirt and sarong, a clay-pot head
    // with a face marked in white lime.
    static void BuildScarecrow(Transform root, Vector3 at)
    {
        var go = EditorKit.Empty("Scarecrow", root, FarmLayout.OnGround(at));
        go.rotation = Quaternion.Euler(0f, 165f + (at.x % 3f) * 10f, 0f);   // faces south, toward the jungle
        var stick = EditorKit.Mat("HutPole", new Color(0.40f, 0.31f, 0.22f));
        EditorKit.Part("Pole", PrimitiveType.Cylinder, go, new Vector3(0f, 0.9f, 0f), new Vector3(0.06f, 0.9f, 0.06f), stick, false);
        var arms = EditorKit.Part("Arms", PrimitiveType.Cylinder, go, new Vector3(0f, 1.35f, 0f), new Vector3(0.05f, 0.55f, 0.05f), stick, false);
        arms.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        EditorKit.Part("Shirt", PrimitiveType.Cube, go, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.45f, 0.18f),
            EditorKit.Mat("ScarecrowShirt", new Color(0.62f, 0.55f, 0.45f)), false);
        EditorKit.Part("Sarong", PrimitiveType.Cylinder, go, new Vector3(0f, 0.75f, 0f), new Vector3(0.36f, 0.25f, 0.24f),
            EditorKit.Mat("ScarecrowSarong", new Color(0.30f, 0.22f, 0.35f)), false);
        var head = EditorKit.Part("PotHead", PrimitiveType.Sphere, go, new Vector3(0f, 1.62f, 0f), new Vector3(0.28f, 0.3f, 0.28f),
            EditorKit.Mat("ClayPot", new Color(0.62f, 0.33f, 0.20f)), false);
        var lime = EditorKit.Mat("LimeMark", new Color(0.95f, 0.95f, 0.92f));
        foreach (float x in new[] { -0.06f, 0.06f })
            EditorKit.Part("Eye", PrimitiveType.Sphere, go, new Vector3(x, 1.66f, 0.13f), new Vector3(0.05f, 0.05f, 0.02f), lime, false);
        EditorKit.Part("Mouth", PrimitiveType.Cube, go, new Vector3(0f, 1.56f, 0.135f), new Vector3(0.1f, 0.015f, 0.01f), lime, false);
        head.transform.localRotation = Quaternion.identity;
    }

    // Collision shapes for what we added (the village's own come from world.json): the dagaba,
    // the agro-well, the shade tree's trunk and the haystacks round the kamatha.
    static void AddColliders(Transform root, Transform village)
    {
        var shapes = EditorKit.Group("Colliders", root);
        void Round(string name, Vector3 at, float radius, float height)
        {
            var go = EditorKit.Empty(name, shapes, FarmLayout.OnGround(at));
            var c = go.gameObject.AddComponent<CapsuleCollider>();
            c.radius = radius;
            c.height = height;
            c.center = new Vector3(0f, height / 2f, 0f);
            go.gameObject.isStatic = true;
        }
        Round("DagabaBase", Dagaba, 3.6f, 6f);
        Round("AgroWell", AgroWell, 1.3f, 2f);
        Round("ShadeTreeTrunk", ShadeTree, 0.5f, 4f);
        foreach (var p in KamathaProps)
            if (p.model.StartsWith("haystack")) Round("Haystack", KamathaPoint(p.x, p.z), 1.1f, 2.5f);
    }
}
