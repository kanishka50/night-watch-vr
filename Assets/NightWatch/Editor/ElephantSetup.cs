using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Builds the field and the elephants (called by Night Watch > Build Full Game):
//  - the electric fence on the north side, in sections that block elephants until broken
//  - three crop plots (west, centre, east) that elephants eat
//  - the NavMesh (the map of walkable ground)
//  - the four spawn points, a hidden stand-in elephant to copy, and the arrival manager
public static class ElephantSetup
{
    const float FenceZ = 12f;
    const float SectionLength = 4f;
    const string NavMeshPath = "Assets/NightWatch/NightWatchNavMesh.asset";

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

    static void BuildCropPlots(Transform root)
    {
        var parent = EditorKit.Group("CropPlots", root);
        var paddy = EditorKit.Mat("Paddy", new Color(0.36f, 0.55f, 0.20f));
        float[] edges = { -20f, -7f, 7f, 20f };
        string[] names = { "Plot_West", "Plot_Centre", "Plot_East" };

        for (int i = 0; i < 3; i++)
        {
            float width = edges[i + 1] - edges[i];
            var plot = EditorKit.Empty(names[i], parent, new Vector3((edges[i] + edges[i + 1]) / 2f, 0f, -1f));
            var area = plot.gameObject.AddComponent<BoxCollider>();
            area.isTrigger = true;
            area.center = new Vector3(0f, 0.5f, 0f);
            area.size = new Vector3(width, 1f, 22f);

            // The crop: a green layer of paddy that gets shorter and browner when eaten.
            var crop = EditorKit.Part("Crop", PrimitiveType.Cube, plot, new Vector3(0f, 0.175f, 0f),
                new Vector3(width - 0.4f, 0.35f, 21.6f), paddy, false);

            var cp = plot.gameObject.AddComponent<CropPlot>();
            cp.crop = crop.transform;
        }
    }

    static void BuildFence(Transform root)
    {
        var parent = EditorKit.Group("ElectricFence", root);
        var postMat = EditorKit.Mat("FencePost", new Color(0.35f, 0.28f, 0.20f));
        var wireMat = EditorKit.Mat("FenceWire", new Color(0.7f, 0.7f, 0.7f));

        // Full width of the map, so elephants from the north cannot walk round the ends.
        for (float x = -30f; x < 30f; x += SectionLength)
        {
            var section = EditorKit.Empty("FenceSection", parent, new Vector3(x + SectionLength / 2f, 0f, FenceZ));
            var obstacle = section.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 1.25f, 0f);
            obstacle.size = new Vector3(SectionLength, 2.5f, 0.6f);
            obstacle.carving = true;

            var post = EditorKit.Part("Post", PrimitiveType.Cylinder, section, new Vector3(-SectionLength / 2f, 0.75f, 0f),
                new Vector3(0.12f, 0.75f, 0.12f), postMat);
            var wires = EditorKit.Group("Wires", section);
            EditorKit.Part("Wire_Low", PrimitiveType.Cube, wires, new Vector3(0f, 0.6f, 0f), new Vector3(SectionLength, 0.02f, 0.02f), wireMat, false);
            EditorKit.Part("Wire_High", PrimitiveType.Cube, wires, new Vector3(0f, 1.2f, 0f), new Vector3(SectionLength, 0.02f, 0.02f), wireMat, false);

            var fs = section.gameObject.AddComponent<FenceSection>();
            fs.post = post.transform;
            fs.wires = wires.gameObject;
        }
        var end = EditorKit.Empty("EndPost", parent, new Vector3(30f, 0f, FenceZ));
        EditorKit.Part("Post", PrimitiveType.Cylinder, end, new Vector3(0f, 0.75f, 0f), new Vector3(0.12f, 0.75f, 0.12f), postMat);
    }

    public static void BakeNavMesh()
    {
        var navigation = EditorKit.Group("Navigation", null);
        var surface = navigation.gameObject.AddComponent<NavMeshSurface>();
        // Colliders, not visible meshes: tree crowns have no collider, so only trunks block the way.
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();

        // Save the result as an asset so the scene keeps it after closing.
        AssetDatabase.DeleteAsset(NavMeshPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        EditorUtility.SetDirty(surface);
    }

    public static void BuildElephants(Transform game)
    {
        // Elephants walk round the hut, never under it.
        var hutBlock = EditorKit.Empty("HutObstacle", game, Vector3.zero);
        var obstacle = hutBlock.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0f, 1.5f, 0f);
        obstacle.size = new Vector3(3.8f, 3f, 3.8f);
        obstacle.carving = true;

        var spawns = EditorKit.Group("Spawns", game);
        var north = Spawn("Spawn_North", spawns, new Vector3(0f, 0f, 26f));
        var east = Spawn("Spawn_East", spawns, new Vector3(28f, 0f, BlockoutBuilder.SideLaneZ));
        var west = Spawn("Spawn_West", spawns, new Vector3(-28f, 0f, BlockoutBuilder.SideLaneZ));
        var south = Spawn("Spawn_South", spawns, new Vector3(BlockoutBuilder.SouthLaneX, 0f, -28f));

        var template = CreateTemplate(game);

        var arrivals = new GameObject("Arrivals").AddComponent<ArrivalManager>();
        arrivals.transform.SetParent(game, false);
        arrivals.elephantTemplate = template;
        arrivals.northSpawn = north;
        arrivals.eastSpawn = east;
        arrivals.westSpawn = west;
        arrivals.southSpawn = south;
    }

    static Transform Spawn(string name, Transform parent, Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out var hit, 6f, NavMesh.AllAreas)) position = hit.position;
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
