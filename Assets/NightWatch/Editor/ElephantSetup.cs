using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Builds the field and the elephants (called by Night Watch > Build Full Game):
//  - the electric fence on the north and west sides, in sections that block elephants until broken
//  - three crop plots beside the hut that elephants eat
//  - the NavMesh (the map of walkable ground)
//  - the four spawn points, a hidden stand-in elephant to copy, and the arrival manager
public static class ElephantSetup
{
    const string NavMeshPath = "Assets/NightWatch/NightWatchNavMesh.asset";
    // The walk map covers the farm and every way in: x -75..65, z -75..55 (all three spawn points,
    // the north and west fence lines, the neighbour's garden). Village houses, trees, haystacks and
    // garden fences are invisible colliders made by VillageBuilder, so elephants walk round them.
    static readonly Vector3 NavMeshCenter = new Vector3(-5f, 5f, -10f);
    static readonly Vector3 NavMeshSize = new Vector3(140f, 60f, 130f);

    public static void BuildField()
    {
        var root = EditorKit.Group("Field", null);
        // The fence and crops do not shape the baked NavMesh: the fence blocks at run time
        // (carving obstacles), and the crops are walkable.
        var modifier = root.gameObject.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;

        BuildCropPlots(root);
        BuildFence(root);
    }

    // The field: three plots of ripe paddy in the village's paddy field, beside the hut
    // (FarmLayout.CropPlots). The village's own bunds (niyara) and drained ground are round them.
    // Each plot is planted with the village's rice_gold model on the village's own clump spots
    // (VillageBuilder.CropRice; about 195 per plot), drawn by a CropDrawer. Eaten clumps lie flat
    // and turn brown, nearest to the elephant first (CropPlot).
    static void BuildCropPlots(Transform root)
    {
        var parent = EditorKit.Group("CropPlots", root);
        var model = VillageBuilder.LoadModel(VillageBuilder.CropModel);
        if (model == null) return;
        var parts = VillageBuilder.Parts(model, VillageBuilder.CropModel);
        var trampled = new Material[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            trampled[i] = VillageBuilder.TintedCopy(parts[i].material, $"{VillageBuilder.CropModel}_trampled_{i}", TrampledTint);

        foreach (var (name, area) in FarmLayout.CropPlots)
        {
            var center = FarmLayout.OnGround(new Vector3(area.center.x, 0f, area.center.y));
            var plot = EditorKit.Empty(name, parent, center);
            var trigger = plot.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.5f, 0f);
            trigger.size = new Vector3(area.width, 1f, area.height);

            var crop = new GameObject("Crop");
            crop.transform.SetParent(plot, false);
            var drawer = crop.AddComponent<CropDrawer>();
            drawer.parts = parts;
            drawer.trampledMaterials = trampled;
            drawer.poses = VillageBuilder.CropRice.TryGetValue(name, out var list) ? list.ToArray() : new float[0];
            if (drawer.Count == 0)
                Debug.LogWarning($"Night Watch: no village rice found in {name}. Build the village first (Build Full Game does).");

            var cp = plot.gameObject.AddComponent<CropPlot>();
            cp.drawer = drawer;
        }
    }

    // Trampled rice: the gold straw darkened to muddy brown.
    static readonly Color TrampledTint = new Color(0.55f, 0.42f, 0.28f);

    // The electric fence on the north and west sides (FarmLayout.FenceLines), made of the village's
    // wire_fence sections. The model runs along its own z, about 3.5 m long: one part is the wires,
    // the other the wooden posts. Each section is a FenceSection: shock, push, break, gap.
    // Not marked Static: a broken section's posts fall over.
    static void BuildFence(Transform root)
    {
        var parent = EditorKit.Group("ElectricFence", root);
        var model = VillageBuilder.LoadModel(VillageBuilder.FenceModel);
        if (model == null) return;

        foreach (var (lineName, from, to, sections, outside) in FarmLayout.FenceLines)
        {
            var line = EditorKit.Group("Fence_" + lineName, parent);
            var along = to - from;
            float length = along.magnitude / sections;
            var yaw = Quaternion.LookRotation(new Vector3(along.x, 0f, along.y));
            for (int i = 0; i < sections; i++)
            {
                var c = from + along * ((i + 0.5f) / sections);
                var section = EditorKit.Empty($"FenceSection_{lineName}_{i + 1}", line, FarmLayout.OnGround(new Vector3(c.x, 0f, c.y)));
                section.rotation = yaw;

                var obstacle = section.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = new Vector3(0f, 1.25f, 0f);
                obstacle.size = new Vector3(0.6f, 2.5f, length);
                obstacle.carving = true;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, section);
                go.transform.localPosition = model.transform.localPosition;
                go.transform.localRotation = model.transform.localRotation;
                go.transform.localScale = model.transform.localScale;

                var fs = section.gameObject.AddComponent<FenceSection>();
                fs.outside = outside;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name.ToLowerInvariant().Contains("wood")) fs.post = r.transform;
                    else fs.wires = r.gameObject;
                }
                if (fs.post == null || fs.wires == null)
                    Debug.LogWarning($"Night Watch: {section.name} could not find the wood and wire parts of {VillageBuilder.FenceModel}.");
            }
        }
    }

    public static void BakeNavMesh()
    {
        var navigation = EditorKit.Group("Navigation", null);
        var surface = navigation.gameObject.AddComponent<NavMeshSurface>();
        // Colliders, not visible meshes: tree crowns have no collider, so only trunks block the way.
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        // Only the farm and the ways in, not the whole 280 m village terrain (faster to bake).
        surface.collectObjects = CollectObjects.Volume;
        surface.center = NavMeshCenter;
        surface.size = NavMeshSize;
        surface.BuildNavMesh();

        // Save the result as an asset so the scene keeps it after closing.
        AssetDatabase.DeleteAsset(NavMeshPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        EditorUtility.SetDirty(surface);
    }

    public static void BuildElephants(Transform game)
    {
        // Elephants walk round the hut, never under it. A round keep-out zone, wide enough for the
        // long body (the head is 2.4 m in front of the elephant's centre): at the closest point
        // the trunk just touches the stilts.
        var hutBlock = EditorKit.Empty("HutObstacle", game, Vector3.zero);
        var obstacle = hutBlock.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.center = new Vector3(0f, 1.5f, 0f);
        obstacle.radius = ElephantAI.HutKeepOut;
        obstacle.height = 3f;
        obstacle.carving = true;

        var spawns = EditorKit.Group("Spawns", game);
        var north = Spawn("Spawn_North", spawns, FarmLayout.SpawnNorth);
        var south = Spawn("Spawn_South", spawns, FarmLayout.SpawnSouth);
        var village = Spawn("Spawn_Village", spawns, FarmLayout.SpawnVillage);

        var template = CreateTemplate(game);

        var arrivals = new GameObject("Arrivals").AddComponent<ArrivalManager>();
        arrivals.transform.SetParent(game, false);
        arrivals.elephantTemplate = template;
        arrivals.northSpawn = north;
        arrivals.southSpawn = south;
        arrivals.villageSpawn = village;
    }

    static Transform Spawn(string name, Transform parent, Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out var hit, 6f, NavMesh.AllAreas)) position = hit.position;
        else
        {
            position = FarmLayout.OnGround(position);
            Debug.LogWarning($"Night Watch: {name} at {position} is not on the walk map (NavMesh).");
        }
        return EditorKit.Empty(name, parent, position);
    }

    // A hidden stand-in elephant (simple grey shapes, about the size of an Asian elephant).
    // Arrivals are copies of it. Replace the shapes with a real model later.
    static GameObject CreateTemplate(Transform game)
    {
        var mat = EditorKit.Mat("ElephantGrey", new Color(0.35f, 0.33f, 0.32f));
        var tuskMat = EditorKit.Mat("Tusk", new Color(0.85f, 0.82f, 0.72f));

        var elephant = EditorKit.Empty("ElephantTemplate", game, new Vector3(0f, 0f, 40f));
        var body = EditorKit.Group("StandInBody", elephant);
        EditorKit.Part("Body", PrimitiveType.Cube, body, new Vector3(0f, 2f, 0f), new Vector3(1.8f, 1.8f, 3f), mat, false);
        EditorKit.Part("Head", PrimitiveType.Cube, body, new Vector3(0f, 2.5f, 1.9f), new Vector3(1.2f, 1.3f, 1f), mat, false);

        var earLeft = EditorKit.Group("EarPivot_L", body);
        earLeft.localPosition = new Vector3(-0.6f, 2.6f, 1.75f);
        EditorKit.Part("Ear", PrimitiveType.Cube, earLeft, new Vector3(-0.05f, 0f, -0.4f), new Vector3(0.1f, 0.9f, 0.8f), mat, false);
        var earRight = EditorKit.Group("EarPivot_R", body);
        earRight.localPosition = new Vector3(0.6f, 2.6f, 1.75f);
        EditorKit.Part("Ear", PrimitiveType.Cube, earRight, new Vector3(0.05f, 0f, -0.4f), new Vector3(0.1f, 0.9f, 0.8f), mat, false);

        var trunk = EditorKit.Group("TrunkPivot", body);
        trunk.localPosition = new Vector3(0f, 2.1f, 2.4f);
        EditorKit.Part("Trunk", PrimitiveType.Cylinder, trunk, new Vector3(0f, -0.7f, 0f), new Vector3(0.3f, 0.7f, 0.3f), mat, false);
        EditorKit.Part("Tusk_L", PrimitiveType.Cylinder, body, new Vector3(-0.3f, 1.9f, 2.45f), new Vector3(0.08f, 0.25f, 0.08f), tuskMat, false);
        EditorKit.Part("Tusk_R", PrimitiveType.Cylinder, body, new Vector3(0.3f, 1.9f, 2.45f), new Vector3(0.08f, 0.25f, 0.08f), tuskMat, false);

        foreach (var x in new[] { -0.6f, 0.6f })
        foreach (var z in new[] { -1f, 1f })
            EditorKit.Part("Leg", PrimitiveType.Cylinder, body, new Vector3(x, 0.55f, z), new Vector3(0.5f, 0.55f, 0.5f), mat, false);

        // One collider for the whole animal, so thrown crackers can hit it.
        var hitbox = elephant.gameObject.AddComponent<CapsuleCollider>();
        hitbox.direction = 2;
        hitbox.center = new Vector3(0f, 1.8f, 0.3f);
        hitbox.radius = 1f;
        hitbox.height = 4.4f;

        var agent = elephant.gameObject.AddComponent<NavMeshAgent>();
        agent.radius = 0.9f;
        agent.height = 3f;
        agent.speed = 1.3f;
        agent.angularSpeed = 120f;
        agent.acceleration = 6f;
        agent.stoppingDistance = 0.5f;

        // 3D voice: louder when near, and the player can hear which side it is on.
        var voice = elephant.gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.rolloffMode = AudioRolloffMode.Logarithmic;
        voice.minDistance = 5f;
        voice.maxDistance = 60f;

        var bodyMotion = elephant.gameObject.AddComponent<ElephantBody>();
        bodyMotion.bodyRoot = body;
        bodyMotion.earLeft = earLeft;
        bodyMotion.earRight = earRight;
        bodyMotion.trunk = trunk;

        var ai = elephant.gameObject.AddComponent<ElephantAI>();
        ai.body = bodyMotion;
        ai.voice = voice;

        elephant.gameObject.SetActive(false);
        return elephant.gameObject;
    }
}
