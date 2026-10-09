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

    // A pela: a small hut on stilts, open on the south side where the ladder is.
    static void BuildHut(Transform root)
    {
        var parent = Group("WatchHut", root);
        var wood = Mat("Wood", new Color(0.45f, 0.30f, 0.16f));
        var thatch = Mat("Thatch", new Color(0.62f, 0.52f, 0.30f));

        float half = HutSize / 2f - 0.1f;
        foreach (var x in new[] { -half, half })
        foreach (var z in new[] { -half, half })
        {
            Post("Stilt", parent, x, z, 0f, HutFloorHeight, 0.18f, wood);
            Post("RoofPost", parent, x, z, HutFloorHeight, 2.3f, 0.12f, wood);
        }

        // In the hut, the floor is the only place the player can teleport to.
        var floor = Box("Floor", parent, new Vector3(0f, HutFloorHeight, 0f), new Vector3(HutSize, 0.15f, HutSize), wood);
        // The starter rig's teleport pointer only accepts surfaces on the "Teleport" interaction layer.
        var teleportArea = floor.AddComponent<TeleportationArea>();
        teleportArea.interactionLayers = InteractionLayerMask.GetMask("Teleport");

        float railY = HutFloorHeight + 0.5f;
        Box("Rail_North", parent, new Vector3(0f, railY, half), new Vector3(HutSize, 0.9f, 0.08f), wood);
        Box("Rail_East", parent, new Vector3(half, railY, 0f), new Vector3(0.08f, 0.9f, HutSize), wood);
        Box("Rail_West", parent, new Vector3(-half, railY, 0f), new Vector3(0.08f, 0.9f, HutSize), wood);

        Box("Roof", parent, new Vector3(0f, HutFloorHeight + 2.4f, 0f), new Vector3(HutSize + 0.8f, 0.2f, HutSize + 0.8f), thatch);
        var ladder = Group("Ladder", parent);
        float ladderZ = -HutSize / 2f - 0.2f;
        Box("Rail_L", ladder, new Vector3(-0.25f, HutFloorHeight / 2f, ladderZ), new Vector3(0.06f, HutFloorHeight, 0.06f), wood);
        Box("Rail_R", ladder, new Vector3(0.25f, HutFloorHeight / 2f, ladderZ), new Vector3(0.06f, HutFloorHeight, 0.06f), wood);
        for (float y = 0.3f; y < HutFloorHeight; y += 0.3f)
            Box("Rung", ladder, new Vector3(0f, y, ladderZ), new Vector3(0.5f, 0.04f, 0.04f), wood);
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

    // Unity's cylinder is 2 m tall at scale 1, so the Y scale is half the height.
    static void Post(string name, Transform parent, float x, float z, float baseY, float height, float diameter, Material mat)
    {
        Box(name, parent, new Vector3(x, baseY + height / 2f, z), new Vector3(diameter, height / 2f, diameter), mat, PrimitiveType.Cylinder);
    }

    // Materials are saved as assets so the scene keeps them after it is closed.
    static Material Mat(string name, Color color) => EditorKit.Mat(name, color);
}
