using System.Collections.Generic;
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

    // The field: three plots of ripening paddy (west, centre, east), as in a Sri Lankan kumbura.
    //  - rice plants in rows, one mesh per plot (one draw call, light enough for this laptop)
    //  - earth ridges (niyara) round the plots and across the middle
    //  - a small canal (ela) along the south edge, with a plank where the path crosses
    // Each plot shrinks and turns brown as it is eaten (CropPlot).
    static void BuildCropPlots(Transform root)
    {
        var parent = EditorKit.Group("CropPlots", root);
        var floorMat = EditorKit.Mat("PaddyFloor", new Color(0.24f, 0.26f, 0.14f));
        var plantMat = PlantMaterial();
        float[] edges = { -20f, -7f, 7f, 20f };
        string[] names = { "Plot_West", "Plot_Centre", "Plot_East" };

        for (int i = 0; i < 3; i++)
        {
            float width = edges[i + 1] - edges[i];
            var plot = EditorKit.Empty(names[i], parent, new Vector3((edges[i] + edges[i + 1]) / 2f, 0f, FieldCenterZ));
            var area = plot.gameObject.AddComponent<BoxCollider>();
            area.isTrigger = true;
            area.center = new Vector3(0f, 0.5f, 0f);
            area.size = new Vector3(width, 1f, FieldLength);

            // Wet, dark ground under the plants.
            EditorKit.Part("Floor", PrimitiveType.Cube, plot, new Vector3(0f, 0.01f, 0f),
                new Vector3(width - 0.4f, 0.02f, FieldLength - 0.4f), floorMat, false);

            // The plants grow from y = 0, so the crop shrinks downward from the top.
            var crop = new GameObject("Crop");
            crop.transform.SetParent(plot, false);
            crop.AddComponent<MeshFilter>().sharedMesh = PlantMesh(names[i], plot.position, width - 0.6f, FieldLength - 0.6f);
            crop.AddComponent<MeshRenderer>().sharedMaterial = plantMat;

            var cp = plot.gameObject.AddComponent<CropPlot>();
            cp.crop = crop.transform;
            cp.pivotAtBase = true;
            cp.fullColor = new Color(0.50f, 0.60f, 0.22f);
        }

        BuildRidges(parent, edges);
        BuildCanal(parent, edges[0], edges[3]);
    }

    const float FieldCenterZ = -1f;
    const float FieldLength = 22f;
    const float RowSpacing = 0.45f;
    const float PlantSpacing = 0.35f;
    const string MeshFolder = "Assets/NightWatch/Meshes";

    // The yard round the hut, where nothing is planted (start spot, task board, energiser).
    static bool InYard(Vector3 world) => world.x > -3f && world.x < 3.5f && world.z > -8.5f && world.z < 3.5f;

    // Rows of rice plants. Each plant is two crossed upright quads. All normals point up, so the
    // plants are lit evenly from both sides like a soft field, not as dark flat cards.
    static Mesh PlantMesh(string name, Vector3 plotCenter, float width, float length)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        var oldState = Random.state;
        Random.InitState(name.GetHashCode());

        for (float x = -width / 2f; x <= width / 2f; x += RowSpacing)
        for (float z = -length / 2f; z <= length / 2f; z += PlantSpacing)
        {
            var p = new Vector3(x + Random.Range(-0.05f, 0.05f), 0f, z + Random.Range(-0.08f, 0.08f));
            var world = plotCenter + p;
            if (InYard(world)) continue;
            if (Mathf.Abs(world.z - FieldCenterZ) < 0.35f) continue;   // the middle ridge

            float height = Random.Range(0.6f, 0.8f);
            float half = Random.Range(0.12f, 0.16f);
            float turn = Random.Range(0f, 90f);
            var lean = new Vector3(Random.Range(-0.08f, 0.08f), 0f, Random.Range(-0.08f, 0.08f));
            for (int k = 0; k < 2; k++)
            {
                var side = Quaternion.Euler(0f, turn + k * 90f, 0f) * Vector3.right * half;
                int start = vertices.Count;
                vertices.Add(p - side);
                vertices.Add(p + side);
                vertices.Add(p + side * 1.4f + Vector3.up * height + lean);
                vertices.Add(p - side * 1.4f + Vector3.up * height + lean);
                for (int n = 0; n < 4; n++) normals.Add(Vector3.up);
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
        }
        Random.state = oldState;

        var mesh = new Mesh { name = "Paddy_" + name };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        // Saved as an asset so the scene keeps it after closing.
        if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/NightWatch", "Meshes");
        string path = $"{MeshFolder}/Paddy_{name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // Seen from both sides (each plant card is a single quad).
    static Material PlantMaterial()
    {
        var mat = EditorKit.Mat("PaddyPlant", new Color(0.50f, 0.60f, 0.22f));
        mat.SetFloat("_Cull", 0f);
        mat.doubleSidedGI = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Low earth ridges (niyara) round the plots and across the middle. No colliders: elephants
    // walk over them and the teleport pointer lands on the ground.
    static void BuildRidges(Transform parent, float[] edges)
    {
        var group = EditorKit.Group("Ridges", parent);
        var earth = EditorKit.Mat("Ridge", new Color(0.36f, 0.30f, 0.20f));
        float minX = edges[0], maxX = edges[edges.Length - 1];
        float minZ = FieldCenterZ - FieldLength / 2f, maxZ = FieldCenterZ + FieldLength / 2f;

        foreach (var x in edges)
            EditorKit.Part("Ridge", PrimitiveType.Cube, group, new Vector3(x, 0.1f, FieldCenterZ),
                new Vector3(0.4f, 0.2f, FieldLength + 0.4f), earth, false);
        foreach (var z in new[] { minZ, FieldCenterZ, maxZ })
            EditorKit.Part("Ridge", PrimitiveType.Cube, group, new Vector3((minX + maxX) / 2f, 0.1f, z),
                new Vector3(maxX - minX, 0.2f, 0.4f), earth, false);
    }

    // A small irrigation canal (ela) just south of the field, and a plank for the path.
    static void BuildCanal(Transform parent, float minX, float maxX)
    {
        float z = FieldCenterZ - FieldLength / 2f - 1f;
        var water = EditorKit.Mat("CanalWater", new Color(0.10f, 0.16f, 0.18f));
        water.SetFloat("_Smoothness", 0.9f);
        EditorUtility.SetDirty(water);
        EditorKit.Part("Canal", PrimitiveType.Cube, parent, new Vector3((minX + maxX) / 2f, 0.015f, z),
            new Vector3(maxX - minX, 0.02f, 1f), water, false);
        EditorKit.Part("Plank", PrimitiveType.Cube, parent, new Vector3(0f, 0.05f, z),
            new Vector3(1.6f, 0.06f, 1.4f), EditorKit.Mat("Wood", new Color(0.45f, 0.30f, 0.16f)), false);
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
