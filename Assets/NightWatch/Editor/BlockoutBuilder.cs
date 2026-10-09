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
    // old planks, stick railings, and a two-slope roof of overlapping old cadjan (woven, dried
    // coconut leaves), open on the south side where the ladder is. Every pole leans a little and
    // every cadjan row is a little crooked, so it looks hand-made and old, like the village's
    // houses. Sizes and part names stay as before (HutDamage, the props and the floor teleport
    // use them): Floor, Rail_North, Rail_East, Rail_West, Roof, Ladder.
    static void BuildHut(Transform root)
    {
        var parent = Group("WatchHut", root);
        var pole = Mat("HutPole", new Color(0.40f, 0.31f, 0.22f));
        var planks = Mat("HutFloor", new Color(0.45f, 0.35f, 0.25f));
        var cadjan = Mat("Cadjan", new Color(0.55f, 0.45f, 0.30f));
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

        BuildRoof(parent, half, eave, pole, cadjan, Jitter);

        // A pole ladder: two side poles and stick rungs. The poles rise about 0.9 m above the floor,
        // as on a real pela ladder (held when stepping on and off), with a last rung at the top.
        // Pointing at the ladder climbs it (HutPropsSetup: LadderTop, LadderBottom).
        var ladder = Group("Ladder", parent);
        float ladderZ = LadderZ;
        Pole("Rail_L", ladder, new Vector3(-0.27f, 0f, ladderZ), new Vector3(-0.25f, HutFloorTop + LadderAboveFloor, ladderZ), 0.08f, pole);
        Pole("Rail_R", ladder, new Vector3(0.27f, 0f, ladderZ), new Vector3(0.25f, HutFloorTop + LadderAboveFloor, ladderZ), 0.08f, pole);
        Pole("TopRung", ladder, new Vector3(-0.29f, HutFloorTop + LadderAboveFloor - 0.08f, ladderZ),
            new Vector3(0.29f, HutFloorTop + LadderAboveFloor - 0.08f, ladderZ), 0.05f, pole);
        for (float y = 0.3f; y < HutFloorHeight; y += 0.3f)
        {
            float tilt = Jitter(0.02f);
            Pole("Rung", ladder, new Vector3(-0.3f, y + tilt, ladderZ), new Vector3(0.3f, y - tilt, ladderZ), 0.045f, pole);
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

    // The roof: eave beams and a ridge pole on the posts, and on each slope six overlapping rows
    // of cadjan, the upper row over the lower one, each a little crooked; a cap along the ridge.
    // The ridge runs east-west, so the slopes face north and south (the low eaves frame the view).
    static void BuildRoof(Transform parent, float half, float eave, Material pole, Material cadjan, System.Func<float, float> jitter)
    {
        var roof = Group("Roof", parent);
        const float rise = 0.85f, overhang = 0.55f, rowWidth = 0.5f, rowStep = 0.38f, rowThickness = 0.05f;
        const int rows = 6;
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

        float angle = Mathf.Atan2(rise, half);
        foreach (var side in new[] { -1f, 1f })
        {
            // Down the slope, and out of the roof surface.
            var down = new Vector3(0f, -Mathf.Sin(angle), side * Mathf.Cos(angle));
            var outward = new Vector3(0f, Mathf.Cos(angle), side * Mathf.Sin(angle));
            float slope = (half + overhang) / Mathf.Cos(angle);
            for (int i = 0; i < rows; i++)
            {
                float along = Mathf.Min(rowWidth / 2f + i * rowStep, slope - rowWidth / 2f);
                // Upper rows lie on top of lower ones, like shingles.
                var at = new Vector3(jitter(0.08f), ridgeY + 0.06f, 0f) + down * along + outward * (0.02f * (rows - i));
                var row = Box("CadjanRow", roof, at, new Vector3(length + jitter(0.12f), rowThickness, rowWidth), cadjan);
                row.transform.localRotation = Quaternion.LookRotation(down, outward) * Quaternion.Euler(jitter(2f), jitter(1.5f), jitter(1.5f));
            }
        }
        var cap = Box("RidgeCap", roof, new Vector3(0f, ridgeY + 0.13f, 0f), new Vector3(length, rowThickness, 0.45f), cadjan);
        cap.transform.localRotation = Quaternion.Euler(0f, jitter(1f), 0f);
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
