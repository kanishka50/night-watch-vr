using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Builds a simple shape layout (blockout) of the Night Watch level in the open scene:
// ground, the watch hut (pela) with its ladder, the forest round the edge and the
// neighbour's house. North is +Z. The forest has gaps (lanes) on the east, west and south
// where elephants come in. The fence and crop plots are built by ElephantSetup.
// Called by Night Watch > Build Full Game (GameSetup). Running it again replaces the old one.
public static class BlockoutBuilder
{
    const string RootName = "Blockout";
    const string MaterialFolder = "Assets/NightWatch/Materials";

    public const float HutFloorHeight = 2.5f;
    public const float HutFloorTop = HutFloorHeight + 0.075f;
    public const float HutSize = 3.2f;

    // Where the elephants' lanes through the forest are.
    public const float SideLaneZ = -1f;
    public const float SouthLaneX = -10f;

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

        // The VR template's 10 x 10 m plane is replaced by our own 60 x 60 m ground.
        var templatePlane = GameObject.Find("Plane");
        if (templatePlane != null)
        {
            Undo.RecordObject(templatePlane, "Hide template plane");
            templatePlane.SetActive(false);
        }

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Blockout");

        BuildGround(root.transform);
        BuildHut(root.transform);
        BuildForest(root.transform);
        BuildVillage(root.transform);

        foreach (var t in root.GetComponentsInChildren<Transform>())
            t.gameObject.isStatic = true;

        // The hut shakes and can collapse, so it must not be static (static objects cannot move).
        var hut = root.transform.Find("WatchHut");
        foreach (var t in hut.GetComponentsInChildren<Transform>())
            t.gameObject.isStatic = false;
        hut.gameObject.AddComponent<HutDamage>();

        PlacePlayerInHut();

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("Night Watch: blockout built. Save the scene with Ctrl+S.");
    }

    static void BuildGround(Transform root)
    {
        var parent = Group("Ground", root);
        Box("Ground", parent, new Vector3(0f, -0.1f, 0f), new Vector3(60f, 0.2f, 60f), Mat("Soil", new Color(0.32f, 0.25f, 0.17f)));
        Box("Path", parent, new Vector3(0f, 0.03f, -18f), new Vector3(1.5f, 0.04f, 12f), Mat("Path", new Color(0.55f, 0.45f, 0.32f)));
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

        // The hut floor is the only place the player can teleport to.
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


    // Dense trees on the north edge (where the elephant comes from) and a ring around the
    // rest of the map so nobody sees the edges. Fixed seed, so the layout is the same each run.
    static void BuildForest(Transform root)
    {
        var parent = Group("Forest", root);
        var trunk = Mat("TreeTrunk", new Color(0.30f, 0.22f, 0.14f));
        var leaves = Mat("TreeLeaves", new Color(0.12f, 0.30f, 0.12f));

        var oldState = Random.state;
        Random.InitState(42);

        // North forest band. A gap is left in the middle so the elephant has a way out.
        for (int i = 0; i < 70; i++)
        {
            float x = Random.Range(-29f, 29f);
            float z = Random.Range(16f, 29f);
            if (Mathf.Abs(x) < 3f && z < 22f) continue;
            Tree(parent, x, z, trunk, leaves);
        }

        // East and west edges, with a lane in each where elephants come in.
        for (int i = 0; i < 25; i++)
        {
            float zEast = Random.Range(-29f, 16f);
            float zWest = Random.Range(-29f, 16f);
            if (Mathf.Abs(zEast - SideLaneZ) > 3.5f) Tree(parent, Random.Range(25.5f, 29.5f), zEast, trunk, leaves);
            if (Mathf.Abs(zWest - SideLaneZ) > 3.5f) Tree(parent, Random.Range(-29.5f, -25.5f), zWest, trunk, leaves);
        }

        // South edge, leaving space for the village and a lane for elephants.
        for (int i = 0; i < 20; i++)
        {
            float x = Random.Range(-25f, 25f);
            if (x > -2f && x < 9f) continue;
            if (Mathf.Abs(x - SouthLaneX) < 3.5f) continue;
            Tree(parent, x, Random.Range(-29.5f, -27f), trunk, leaves);
        }

        Random.state = oldState;
    }

    static void BuildVillage(Transform root)
    {
        var parent = Group("NeighbourHouse", root);
        var wall = Mat("HouseWall", new Color(0.75f, 0.70f, 0.60f));
        var roof = Mat("HouseRoof", new Color(0.55f, 0.25f, 0.18f));
        var window = Mat("HouseWindow", new Color(1f, 0.85f, 0.45f));

        Box("Walls", parent, new Vector3(5f, 1.5f, -25f), new Vector3(4f, 3f, 4f), wall);
        Box("Roof", parent, new Vector3(5f, 3.2f, -25f), new Vector3(4.6f, 0.4f, 4.6f), roof);
        // The window faces the hut. It becomes the "warn your neighbour" light later.
        Box("Window", parent, new Vector3(5f, 1.6f, -22.98f), new Vector3(1f, 0.8f, 0.05f), window);
    }

    static void PlacePlayerInHut()
    {
        var rig = GameObject.Find("XR Origin (XR Rig)");
        if (rig == null)
        {
            Debug.LogWarning("Night Watch: no 'XR Origin (XR Rig)' in the scene, player not moved.");
            return;
        }
        Undo.RecordObject(rig.transform, "Move player into hut");
        rig.transform.SetPositionAndRotation(new Vector3(0f, HutFloorHeight + 0.075f, 0.3f), Quaternion.identity);

        // Keep the player's head over the hut floor, with a small margin from the edge.
        var limit = rig.GetComponent<PlayAreaLimit>();
        if (limit == null) limit = Undo.AddComponent<PlayAreaLimit>(rig);
        Undo.RecordObject(limit, "Set play area");
        var cam = rig.GetComponentInChildren<Camera>();
        limit.head = cam != null ? cam.transform : null;
        limit.areaCenter = new Vector3(0f, HutFloorHeight + 1f, 0f);
        limit.areaSize = new Vector2(HutSize - 0.4f, HutSize - 0.4f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(limit);
    }

    static void Tree(Transform parent, float x, float z, Material trunk, Material leaves)
    {
        float height = Random.Range(3f, 5f);
        var tree = Group("Tree", parent);
        tree.localPosition = new Vector3(x, 0f, z);
        Post("Trunk", tree, 0f, 0f, 0f, height, 0.35f, trunk);
        float size = Random.Range(2.5f, 4f);
        var crown = Box("Crown", tree, new Vector3(0f, height + size * 0.3f, 0f), new Vector3(size, size * 1.2f, size), leaves, PrimitiveType.Sphere);
        Object.DestroyImmediate(crown.GetComponent<Collider>());
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
    static Material Mat(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/NightWatch"))
            AssetDatabase.CreateFolder("Assets", "NightWatch");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/NightWatch", "Materials");

        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
